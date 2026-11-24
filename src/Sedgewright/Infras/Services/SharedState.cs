using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Shared application state container for cross-view data that needs to stay synchronized.
/// </summary>
public sealed class SharedState(
    IAgentDefinitionRepository agentRepository,
    IOrchestrationQueryRepository queryRepository,
    ILLMProviderRepository providerRepository,
    IApplicationSettingsRepository applicationSettingsRepository) : ISharedState
{
    private readonly IAgentDefinitionRepository _agentRepository = agentRepository;
    private readonly IOrchestrationQueryRepository _queryRepository = queryRepository;
    private readonly ILLMProviderRepository _providerRepository = providerRepository;
    private readonly IApplicationSettingsRepository _applicationSettingsRepository = applicationSettingsRepository;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _agentRuntimeLock = new(1, 1);
    private readonly SharedAgentCollection _agents = [];
    private readonly SharedAgentSessionCollection _agentSessions = [];
    private readonly SharedOrchestrationCollection _orchestrations = [];
    private readonly SharedProviderCollection _providers = [];
    private ApplicationSettings? _applicationSettings;
    private AgentRuntime? _agentRuntime;
    private bool _disposed;

    public Maybe<AgentRuntime> AgentRuntime => _agentRuntime;

    /// <inheritdoc />
    public ApplicationSettings? ApplicationSettings => _applicationSettings;

    /// <inheritdoc />
    public event EventHandler? ApplicationSettingsChanged;

    // Internal accessors removed: they were unused outside this class.

    /// <inheritdoc />
    public ObservableCollection<AgentDefinitionSnapshot> Agents => _agents;

    /// <inheritdoc />
    public ObservableCollection<AgentSessionSnapshot> AgentSessions => _agentSessions;

    /// <inheritdoc />
    public ObservableCollection<OrchestrationListItemSnapshot> Orchestrations => _orchestrations;

    /// <inheritdoc />
    public ObservableCollection<LLMProvider> Providers => _providers;

    /// <inheritdoc />
    public async Task RefreshAgentsAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            List<AgentDefinition> agents = await _agentRepository.GetAllAsync(cancellationToken);

            RunOnUiThread(() => _agents.ReplaceWith(agents.Select(agent => AgentDefinitionSnapshot.From(agent, 0))));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAgentRuntimeAsync(AgentRuntime agentRuntime)
    {
        ArgumentNullException.ThrowIfNull(agentRuntime);

        await _agentRuntimeLock.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (ReferenceEquals(_agentRuntime, agentRuntime))
            {
                return;
            }

            AgentRuntime? previousRuntime = _agentRuntime;
            _agentRuntime = agentRuntime;

            if (previousRuntime is not null)
            {
                await previousRuntime.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _agentRuntimeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _agentRuntimeLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AgentRuntime? runtime = _agentRuntime;
            _agentRuntime = null;

            if (runtime is not null)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _agentRuntimeLock.Release();
        }
    }

    /// <inheritdoc />
    public void SetAgentSessions(IEnumerable<AgentSessionSnapshot> agentSessions)
        => RunOnUiThread(() => _agentSessions.ReplaceWith(agentSessions));

    /// <inheritdoc />
    public async Task RefreshOrchestrationsAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            List<OrchestrationListItemSnapshot> orchestrations = await _queryRepository.GetAllAsync(cancellationToken);

            RunOnUiThread(() => _orchestrations.ReplaceWith(orchestrations));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshProvidersAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            List<LLMProvider> providers = await _providerRepository.GetAll(cancellationToken);

            RunOnUiThread(() => _providers.ReplaceWith(providers));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshApplicationSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            ApplicationSettings settings = await _applicationSettingsRepository.GetOrCreateAsync(cancellationToken);
            PublishApplicationSettings(settings);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveApplicationSettingsAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            await _applicationSettingsRepository.SaveAsync(settings, cancellationToken);
            PublishApplicationSettings(settings);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void PublishApplicationSettings(ApplicationSettings settings)
        => RunOnUiThread(() =>
        {
            _applicationSettings = settings;
            ApplicationSettingsChanged?.Invoke(this, EventArgs.Empty);
        });

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
    }

    private sealed class SharedAgentCollection : ObservableCollection<AgentDefinitionSnapshot>
    {
        private bool _suppressNotifications;

        public void ReplaceWith(IEnumerable<AgentDefinitionSnapshot> items)
        {
            _suppressNotifications = true;

            try
            {
                ClearItems();

                foreach (AgentDefinitionSnapshot item in items)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnPropertyChanged(e);
        }
    }

    private sealed class SharedAgentSessionCollection : ObservableCollection<AgentSessionSnapshot>
    {
        private bool _suppressNotifications;

        public void ReplaceWith(IEnumerable<AgentSessionSnapshot> items)
        {
            _suppressNotifications = true;

            try
            {
                ClearItems();

                foreach (AgentSessionSnapshot item in items)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnPropertyChanged(e);
        }
    }

    private sealed class SharedOrchestrationCollection : ObservableCollection<OrchestrationListItemSnapshot>
    {
        private bool _suppressNotifications;

        public void ReplaceWith(IEnumerable<OrchestrationListItemSnapshot> items)
        {
            _suppressNotifications = true;

            try
            {
                ClearItems();

                foreach (OrchestrationListItemSnapshot item in items)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnPropertyChanged(e);
        }
    }

    private sealed class SharedProviderCollection : ObservableCollection<LLMProvider>
    {
        private bool _suppressNotifications;

        public void ReplaceWith(IEnumerable<LLMProvider> items)
        {
            _suppressNotifications = true;

            try
            {
                ClearItems();

                foreach (LLMProvider item in items)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnPropertyChanged(e);
        }
    }
}
