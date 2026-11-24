using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.ViewModels.FirstRun;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Settings;

public sealed class ProviderSettingsItemViewModel : ViewModelBase
{
    private readonly ProviderDiscoveryService _providerDiscoveryService = new();
    private readonly RelayCommand _discoverModelsCommand;
    private readonly RelayCommand _enableCopilotCommand;
    private readonly LLMProvider _provider;
    private readonly List<string> _enabledModels = [];
    private string _baseUrl;
    private string _apiKey;
    private string _statusText = string.Empty;
    private bool _isDirty;
    private bool _isApiKeyVisible;
    private bool _isCopilotEnabled;

    public ProviderSettingsItemViewModel(LLMProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        DisplayName = provider.DisplayName;
        _baseUrl = provider.BaseUrl;
        _apiKey = provider.ApiKey;
        _isCopilotEnabled = provider.ProviderKind == LlmProviderKind.Copilot && provider.Id != 0;
        LoadingFeedback = new LoadingFeedbackViewModel();

        try
        {
            var models = JsonSerializer.Deserialize<List<string>>(provider.EnabledModelsJson) ?? new List<string>();
            _enabledModels.AddRange(models);
        }
        catch
        {
            // ignore malformed JSON
        }

        _discoverModelsCommand = new RelayCommand(() => _ = DiscoverModelsAsync(), () => CanDiscoverModels);
        DiscoverModelsCommand = _discoverModelsCommand;
        ToggleApiKeyVisibilityCommand = new RelayCommand(ToggleApiKeyVisibility);
        _enableCopilotCommand = new RelayCommand(EnableCopilot, () => ShowCopilotEnableButton);
        EnableCopilotCommand = _enableCopilotCommand;
        LoadingFeedback.PropertyChanged += OnLoadingFeedbackPropertyChanged;
    }

    /// <summary>
    /// Raised when models are discovered and the UI should open the chooser.
    /// </summary>
    public event EventHandler<ModelSelectionRequestedEventArgs>? ModelSelectionRequested;

    public string DisplayName { get; }

    public LoadingFeedbackViewModel LoadingFeedback { get; }

    public bool IsCopilot => _provider.ProviderKind == LlmProviderKind.Copilot;

    public bool ShowApiKeyInput => !IsCopilot;

    public bool ShowDiscoverModels => ProviderDiscoveryService.CanDiscoverModels(_provider.ProviderKind.ToString());

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (!SetProperty(ref _baseUrl, value ?? string.Empty))
            {
                return;
            }

            IsDirty = true;
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

            IsDirty = true;
            _discoverModelsCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasApiKeyValue));
            OnPropertyChanged(nameof(IsApiKeyEditorVisible));
            OnPropertyChanged(nameof(IsApiKeyMaskedVisible));
            OnPropertyChanged(nameof(ApiKeyMasked));
        }
    }

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

    public string DiscoverModelsButtonText => IsDiscoveringModels ? "Discovering..." : "Check models";

    public bool ShowBaseUrl => _provider.ProviderKind is LlmProviderKind.Azure or LlmProviderKind.OpenAICompatible;

    public bool IsCopilotEnabled => _isCopilotEnabled;

    public bool ShowCopilotEnableButton => IsCopilot && !IsCopilotEnabled;

    public bool ShowCopilotEnabledStatus => IsCopilot && IsCopilotEnabled;

    public ICommand EnableCopilotCommand { get; }

    public bool HasApiKeyValue => !string.IsNullOrWhiteSpace(ApiKey);

    public bool IsApiKeyEditorVisible => ShowApiKeyInput && (IsApiKeyVisible || !HasApiKeyValue);

    public bool IsApiKeyMaskedVisible => ShowApiKeyInput && HasApiKeyValue && !IsApiKeyVisible;

    public bool HasEnabledModelsSummary => _enabledModels.Count > 0;

    public string EnabledModelsSummaryText => BuildEnabledModelsSummary();

    public ICommand DiscoverModelsCommand { get; }

    public bool CanDiscoverModels => !IsDiscoveringModels
        && ShowDiscoverModels
        && (IsCopilot
            ? IsCopilotEnabled
            : !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl));

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public bool IsApiKeyVisible
    {
        get => _isApiKeyVisible;
        set => SetProperty(ref _isApiKeyVisible, value);
    }

    public bool IsApiKeyHidden => !IsApiKeyVisible;

    public string ApiKeyMasked => string.IsNullOrEmpty(ApiKey) ? string.Empty : new string('•', Math.Min(8, ApiKey.Length));

    public ICommand ToggleApiKeyVisibilityCommand { get; }

    public IReadOnlyList<string> EnabledModelIds => _enabledModels.ToArray();

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
        IsDirty = true;
    }

    public void CommitToEntity()
    {
        _provider.BaseUrl = BaseUrl;
        _provider.ApiKey = ApiKey;
        _provider.EnabledModelsJson = JsonSerializer.Serialize(_enabledModels);
    }

    public LLMProvider GetEntity() => _provider;

    public void ClearDirty()
        => IsDirty = false;

    private async Task DiscoverModelsAsync()
    {
        if (!CanDiscoverModels)
        {
            StatusText = string.IsNullOrWhiteSpace(ApiKey)
                ? IsCopilot && !IsCopilotEnabled
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
                _provider.ProviderKind.ToString(),
                BaseUrl,
                ApiKey,
                cancellationToken);

            if (discoveredModels.Count == 0)
            {
                StatusText = "No models were returned by the provider.";
                return;
            }

            ModelSelectionRequested?.Invoke(this, new ModelSelectionRequestedEventArgs(discoveredModels, EnabledModelIds));

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

    private void ToggleApiKeyVisibility()
    {
        IsApiKeyVisible = !IsApiKeyVisible;
        OnPropertyChanged(nameof(IsApiKeyEditorVisible));
        OnPropertyChanged(nameof(IsApiKeyMaskedVisible));
        OnPropertyChanged(nameof(IsApiKeyVisible));
        OnPropertyChanged(nameof(ApiKeyMasked));
        OnPropertyChanged(nameof(IsApiKeyHidden));
    }

    private void EnableCopilot()
    {
        if (!ShowCopilotEnableButton)
        {
            return;
        }

        _isCopilotEnabled = true;
        OnPropertyChanged(nameof(IsCopilotEnabled));
        OnPropertyChanged(nameof(ShowCopilotEnableButton));
        OnPropertyChanged(nameof(ShowCopilotEnabledStatus));
        IsDirty = true;
        _discoverModelsCommand.RaiseCanExecuteChanged();
        _enableCopilotCommand.RaiseCanExecuteChanged();
    }
}
