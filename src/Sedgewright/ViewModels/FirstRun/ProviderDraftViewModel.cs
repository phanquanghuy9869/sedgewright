using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.FirstRun;

/// <summary>
/// Holds the first-run provider discovery state for Sedgewright.
/// </summary>
public sealed class ProviderDraftViewModel : ViewModelBase
{
    private readonly ProviderDiscoveryService _providerDiscoveryService = new();
    private readonly RelayCommand _discoverModelsCommand;
    private readonly List<string> _enabledModels = [];
    private string _apiKey = string.Empty;
    private string _statusText = string.Empty;
    private bool _isEnabled;

    public ProviderDraftViewModel(string providerKind, string displayName)
    {
        ProviderKind = providerKind;
        DisplayName = displayName;
        BaseUrl = ProviderDiscoveryService.GetDefaultBaseUrl(providerKind);
        LoadingFeedback = new LoadingFeedbackViewModel();

        _discoverModelsCommand = new RelayCommand(() => _ = DiscoverModelsAsync(), () => CanDiscoverModels);
        DiscoverModelsCommand = _discoverModelsCommand;
        LoadingFeedback.PropertyChanged += OnLoadingFeedbackPropertyChanged;
    }

    /// <summary>
    /// Raised after models are discovered so the UI can present the chooser window.
    /// </summary>
    public event EventHandler<ModelSelectionRequestedEventArgs>? ModelSelectionRequested;

    public string ProviderKind { get; }

    public string DisplayName { get; }

    public string BaseUrl { get; }

    public LoadingFeedbackViewModel LoadingFeedback { get; }

    public bool IsCopilot => string.Equals(ProviderKind, nameof(LlmProviderKind.Copilot), StringComparison.Ordinal);

    public bool ShowApiKeyInput => !IsCopilot;

    public bool ShowDiscoverModels => ProviderDiscoveryService.CanDiscoverModels(ProviderKind);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanDiscoverModels));
            OnPropertyChanged(nameof(IsReadyToPersist));
            _discoverModelsCommand.RaiseCanExecuteChanged();
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (!SetProperty(ref _apiKey, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(CanDiscoverModels));
            OnPropertyChanged(nameof(DiscoverModelsButtonText));
            OnPropertyChanged(nameof(IsReadyToPersist));
            _discoverModelsCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets whether provider models are currently being discovered.
    /// </summary>
    public bool IsDiscoveringModels
        => LoadingFeedback.IsLoading;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public string DiscoverModelsButtonText => IsDiscoveringModels
        ? "Discovering..."
        : "Discover models";

    public bool HasEnabledModelsSummary => _enabledModels.Count > 0;

    /// <summary>
    /// Gets whether this provider is ready to be saved to SQLite.
    /// </summary>
    public bool IsReadyToPersist => IsCopilot
        ? IsEnabled
        : !string.IsNullOrWhiteSpace(ApiKey) && _enabledModels.Count > 0;

    /// <summary>
    /// Gets the enabled model ids for persistence.
    /// </summary>
    public IReadOnlyList<string> EnabledModelIds => _enabledModels.ToArray();

    public string EnabledModelsSummaryText => BuildEnabledModelsSummary();

    public ICommand DiscoverModelsCommand { get; }

    public bool CanDiscoverModels => !IsDiscoveringModels
        && ProviderDiscoveryService.CanDiscoverModels(ProviderKind)
        && (IsCopilot ? IsEnabled : !string.IsNullOrWhiteSpace(ApiKey));

    /// <summary>
    /// Replaces the selected models that are shown in the compact badge.
    /// </summary>
    /// <param name="enabledModels">The selected model ids.</param>
    public void SetEnabledModels(IReadOnlyList<string> enabledModels)
    {
        _enabledModels.Clear();
        foreach (string modelId in enabledModels
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _enabledModels.Add(modelId);
        }

        OnPropertyChanged(nameof(HasEnabledModelsSummary));
        OnPropertyChanged(nameof(EnabledModelsSummaryText));
        OnPropertyChanged(nameof(IsReadyToPersist));
    }

    private async Task DiscoverModelsAsync()
    {
        if (!CanDiscoverModels)
        {
            StatusText = string.IsNullOrWhiteSpace(ApiKey)
                ? IsCopilot && !IsEnabled
                    ? "Enable GitHub Copilot first."
                    : "Enter a provider key first."
                : string.Empty;
            return;
        }

        CancellationToken cancellationToken = LoadingFeedback.Begin("Discovering models...");
        try
        {
            StatusText = string.Empty;

            IReadOnlyList<string> discoveredModels = await _providerDiscoveryService.DiscoverModelsAsync(
                ProviderKind,
                BaseUrl,
                ApiKey,
                cancellationToken);

            if (discoveredModels.Count == 0)
            {
                StatusText = "No models were returned by the provider.";
                return;
            }

            ModelSelectionRequested?.Invoke(
                this,
                new ModelSelectionRequestedEventArgs(discoveredModels, _enabledModels.ToArray()));

            StatusText = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Model discovery canceled.";
        }
        catch (Exception ex)
        {
            StatusText = IsCopilot
                ? $"Couldn't connect to GitHub Copilot. Check that the Copilot runtime is installed and signed in. Details: {ex.Message}"
                : $"Couldn't refresh provider models: {ex.Message}";
        }
        finally
        {
            LoadingFeedback.Complete();
        }
    }

    private void OnLoadingFeedbackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LoadingFeedbackViewModel.IsLoading))
        {
            return;
        }

        OnPropertyChanged(nameof(IsDiscoveringModels));
        OnPropertyChanged(nameof(CanDiscoverModels));
        OnPropertyChanged(nameof(DiscoverModelsButtonText));
        _discoverModelsCommand.RaiseCanExecuteChanged();
    }

    private string BuildEnabledModelsSummary()
    {
        if (_enabledModels.Count == 0)
        {
            return string.Empty;
        }

        if (_enabledModels.Count > 2)
        {
            return $"Enabled {_enabledModels.Count} models";
        }

        return $"Enabled {string.Join(", ", _enabledModels)}";
    }
}

/// <summary>
/// Provides discovered models and the current selection to the chooser window.
/// </summary>
/// <remarks>
/// Creates a new model selection request.
/// </remarks>
/// <param name="discoveredModels">The discovered model ids.</param>
/// <param name="initiallyEnabledModels">The currently selected model ids.</param>
public sealed class ModelSelectionRequestedEventArgs(IReadOnlyList<string> discoveredModels, IReadOnlyList<string> initiallyEnabledModels) : EventArgs
{

    /// <summary>
    /// Gets the discovered model ids.
    /// </summary>
    public IReadOnlyList<string> DiscoveredModels { get; } = discoveredModels;

    /// <summary>
    /// Gets the currently selected model ids.
    /// </summary>
    public IReadOnlyList<string> InitiallyEnabledModels { get; } = initiallyEnabledModels;
}
