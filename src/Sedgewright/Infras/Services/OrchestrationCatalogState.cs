using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Keeps the orchestration picker list in sync across views.
/// </summary>
public sealed class OrchestrationCatalogState(IOrchestrationQueryRepository queryRepository) : IOrchestrationCatalogState
{
    private readonly IOrchestrationQueryRepository _queryRepository = queryRepository;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly OrchestrationCatalogCollection _orchestrations = [];

    /// <inheritdoc />
    public ObservableCollection<OrchestrationListItemSnapshot> Orchestrations => _orchestrations;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
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

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
    }

    private sealed class OrchestrationCatalogCollection : ObservableCollection<OrchestrationListItemSnapshot>
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
}