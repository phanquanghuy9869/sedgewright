using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models.Updates;
using Sedgewright.ViewModels.FirstRun;

namespace Sedgewright.ViewModels.Settings;

    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly LLMProviderRepository _providerRepository;
        private readonly ISharedState _sharedState;
        private readonly IUpdateService _updateService;
        private readonly AsyncRelayCommand _refreshCommand;
        private readonly AsyncRelayCommand _saveAllCommand;
        private readonly AsyncRelayCommand _checkForUpdatesCommand;
        private readonly AsyncRelayCommand _updateNowCommand;
        private readonly RelayCommand _browseDefaultWorkingDirectoryCommand;
        private string _defaultWorkingDirectory = Environment.CurrentDirectory;
        private bool _isDarkMode;
        private string _statusText = string.Empty;
        private bool _isApplicationSettingsDirty;
        private bool _isSaving;
        private bool _isCheckingForUpdates;
        private bool _isApplyingUpdate;
        private string _availableVersion = string.Empty;
        private string _updateStatusText = string.Empty;

    public SettingsViewModel(
        LLMProviderRepository providerRepository,
        ISharedState sharedState,
        IUpdateService updateService)
    {
        _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        CurrentVersion = _updateService.CurrentVersion;
        Providers = [];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _saveAllCommand = new AsyncRelayCommand(SaveAllAsync, () => CanSaveAll);
        _checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdateBusy);
        _updateNowCommand = new AsyncRelayCommand(UpdateNowAsync, () => CanUpdateNow);
        _browseDefaultWorkingDirectoryCommand = new RelayCommand(RequestDefaultWorkingDirectorySelection);

        InitializeProviders(Array.Empty<LLMProvider>());
        _ = InitializeAsync();
    }

    public ObservableCollection<ProviderSettingsItemViewModel> Providers { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SaveAllCommand => _saveAllCommand;

    public ICommand CheckForUpdatesCommand => _checkForUpdatesCommand;

    public ICommand UpdateNowCommand => _updateNowCommand;

    public event EventHandler? DefaultWorkingDirectorySelectionRequested;

    public string DefaultWorkingDirectory
    {
        get => _defaultWorkingDirectory;
        set
        {
            if (!SetProperty(ref _defaultWorkingDirectory, value ?? string.Empty))
            {
                return;
            }

            _isApplicationSettingsDirty = true;
            _saveAllCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (!SetProperty(ref _isDarkMode, value))
            {
                return;
            }

            if (Application.Current is { } application)
            {
                application.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            _isApplicationSettingsDirty = true;
            _saveAllCommand.RaiseCanExecuteChanged();
        }
    }

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

    public string CurrentVersion { get; }

    public string AvailableVersion
    {
        get => _availableVersion;
        private set
        {
            if (!SetProperty(ref _availableVersion, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(HasAvailableUpdate));
            OnPropertyChanged(nameof(CanUpdateNow));
            _updateNowCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasAvailableUpdate => !string.IsNullOrWhiteSpace(AvailableVersion);

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (!SetProperty(ref _isCheckingForUpdates, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsUpdateBusy));
            OnPropertyChanged(nameof(CanUpdateNow));
            _checkForUpdatesCommand.RaiseCanExecuteChanged();
            _updateNowCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsApplyingUpdate
    {
        get => _isApplyingUpdate;
        private set
        {
            if (!SetProperty(ref _isApplyingUpdate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsUpdateBusy));
            OnPropertyChanged(nameof(CanUpdateNow));
            _checkForUpdatesCommand.RaiseCanExecuteChanged();
            _updateNowCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsUpdateBusy => IsCheckingForUpdates || IsApplyingUpdate;

    public bool CanUpdateNow => HasAvailableUpdate && !IsUpdateBusy;

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set
        {
            if (!SetProperty(ref _updateStatusText, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(HasUpdateStatusText));
        }
    }

    public bool HasUpdateStatusText => !string.IsNullOrWhiteSpace(UpdateStatusText);

    public ICommand BrowseDefaultWorkingDirectoryCommand => _browseDefaultWorkingDirectoryCommand;

    public bool CanSaveAll => !_isSaving
        && (_isApplicationSettingsDirty || Providers.Any(p => p.IsDirty));

    private static LlmProviderKind ParseProviderKind(string providerKind)
        => Enum.TryParse(providerKind, ignoreCase: true, out LlmProviderKind parsedKind)
            ? parsedKind
            : throw new InvalidOperationException($"Unknown provider kind '{providerKind}'.");

    private void OnProviderItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProviderSettingsItemViewModel.IsDirty))
        {
            _saveAllCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnModelSelectionRequested(object? sender, ModelSelectionRequestedEventArgs e)
    {
        // handled by the view code-behind where the window/dialog can be shown
    }

    private void AttachProviderItem(ProviderSettingsItemViewModel providerItem)
    {
        providerItem.PropertyChanged += OnProviderItemPropertyChanged;
        providerItem.ModelSelectionRequested += OnModelSelectionRequested;
    }

    private void DetachProviderItem(ProviderSettingsItemViewModel providerItem)
    {
        providerItem.PropertyChanged -= OnProviderItemPropertyChanged;
        providerItem.ModelSelectionRequested -= OnModelSelectionRequested;
    }

    private void InitializeProviders(IEnumerable<LLMProvider> persistedProviders)
    {
        ReplaceProviders(persistedProviders);
    }

    private void ReplaceProviders(IEnumerable<LLMProvider> persistedProviders)
    {
        foreach (ProviderSettingsItemViewModel providerItem in Providers)
        {
            DetachProviderItem(providerItem);
        }

        Providers.Clear();

        foreach (ProviderSettingsItemViewModel providerItem in BuildProviderItems(persistedProviders))
        {
            AttachProviderItem(providerItem);
            Providers.Add(providerItem);
        }

        _saveAllCommand.RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<ProviderSettingsItemViewModel> BuildProviderItems(IEnumerable<LLMProvider> persistedProviders)
    {
        Dictionary<LlmProviderKind, LLMProvider> persistedByKind = persistedProviders
            .GroupBy(provider => provider.ProviderKind)
            .ToDictionary(group => group.Key, group => group.First());

        List<ProviderSettingsItemViewModel> providerItems = [];
        foreach (ProviderCatalogEntry entry in ProviderCatalog.Entries)
        {
            LlmProviderKind providerKind = ParseProviderKind(entry.ProviderKind);
            persistedByKind.TryGetValue(providerKind, out LLMProvider? persistedProvider);

            providerItems.Add(new ProviderSettingsItemViewModel(CreateProviderSnapshot(entry, providerKind, persistedProvider)));
        }

        return providerItems;
    }

    private static LLMProvider CreateProviderSnapshot(ProviderCatalogEntry entry, LlmProviderKind providerKind, LLMProvider? persistedProvider)
    {
        return new LLMProvider
        {
            Id = persistedProvider?.Id ?? 0,
            ProviderKind = providerKind,
            DisplayName = string.IsNullOrWhiteSpace(persistedProvider?.DisplayName) ? entry.DisplayName : persistedProvider.DisplayName,
            BaseUrl = string.IsNullOrWhiteSpace(persistedProvider?.BaseUrl) ? entry.DefaultBaseUrl : persistedProvider.BaseUrl,
            ApiKey = persistedProvider?.ApiKey ?? string.Empty,
            EnabledModelsJson = string.IsNullOrWhiteSpace(persistedProvider?.EnabledModelsJson) ? "[]" : persistedProvider.EnabledModelsJson,
        };
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _sharedState.RefreshProvidersAsync();
            ReplaceProviders(_sharedState.Providers);
        }
        catch
        {
            ReplaceProviders(Array.Empty<LLMProvider>());
        }

        try
        {
            await _sharedState.RefreshApplicationSettingsAsync();
            if (_sharedState.ApplicationSettings is ApplicationSettings settings)
            {
                ApplyApplicationSettings(settings);
            }
        }
        catch
        {
            ApplyDefaultApplicationSettings();
        }
    }

    private async Task RefreshAsync()
    {
        await _sharedState.RefreshProvidersAsync();
        ReplaceProviders(_sharedState.Providers);
        await _sharedState.RefreshApplicationSettingsAsync();

        if (_sharedState.ApplicationSettings is ApplicationSettings settings)
        {
            ApplyApplicationSettings(settings);
        }
    }

    private async Task SaveAllAsync()
    {
        if (!CanSaveAll)
        {
            return;
        }

        _isSaving = true;
        _saveAllCommand.RaiseCanExecuteChanged();

        bool hasProviderChanges = Providers.Any(item => item.IsDirty);

        try
        {
            foreach (ProviderSettingsItemViewModel item in Providers.Where(item => item.IsDirty))
            {
                item.CommitToEntity();
                LLMProvider entity = item.GetEntity();
                if (entity.Id == 0)
                {
                    _providerRepository.Create(entity);
                }
                else
                {
                    _providerRepository.Update(entity);
                }
            }

            await _providerRepository.SaveChangesAsync();
            if (hasProviderChanges)
            {
                await _sharedState.RefreshProvidersAsync();
            }

            if (_isApplicationSettingsDirty)
            {
                await _sharedState.SaveApplicationSettingsAsync(CreateApplicationSettings());
            }

            foreach (ProviderSettingsItemViewModel item in Providers)
            {
                item.ClearDirty();
            }

            _isApplicationSettingsDirty = false;

            _saveAllCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            _isSaving = false;
            _saveAllCommand.RaiseCanExecuteChanged();
        }
    }

    internal void ApplyDefaultWorkingDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            ShowStatus("The selected directory is not available as a local path.");
            return;
        }

        DefaultWorkingDirectory = directory.Trim();
        StatusText = string.Empty;
    }

    internal void ShowStatus(string status)
        => StatusText = status;

    internal Task CheckForUpdatesSilentlyAsync()
        => CheckForUpdatesAsync(showStatus: false);

    private void RequestDefaultWorkingDirectorySelection()
        => DefaultWorkingDirectorySelectionRequested?.Invoke(this, EventArgs.Empty);

    public Task CheckForUpdatesAsync()
        => CheckForUpdatesAsync(showStatus: true);

    private async Task CheckForUpdatesAsync(bool showStatus)
    {
        if (IsUpdateBusy)
        {
            return;
        }

        IsCheckingForUpdates = true;

        try
        {
            UpdateCheckResult result = await _updateService.CheckForUpdatesAsync();
            AvailableVersion = result.AvailableVersion ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                if (showStatus)
                {
                    UpdateStatusText = "Unable to check for updates right now.";
                }

                return;
            }

            UpdateStatusText = result.IsUpdateAvailable && result.AvailableVersion is not null
                ? $"Version {result.AvailableVersion} is available."
                : showStatus
                    ? "You're up to date."
                    : string.Empty;
        }
        catch
        {
            AvailableVersion = string.Empty;
            if (showStatus)
            {
                UpdateStatusText = "Unable to check for updates right now.";
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public async Task UpdateNowAsync()
    {
        if (!CanUpdateNow)
        {
            return;
        }

        IsApplyingUpdate = true;
        UpdateStatusText = "Downloading update...";

        try
        {
            await _updateService.ApplyUpdateAsync();
            UpdateStatusText = "Update downloaded. Restart Sedgewright to finish installing it.";
        }
        catch
        {
            UpdateStatusText = "Unable to install the update right now.";
        }
        finally
        {
            IsApplyingUpdate = false;
        }
    }

    private void ApplyApplicationSettings(ApplicationSettings settings)
    {
        _defaultWorkingDirectory = string.IsNullOrWhiteSpace(settings.DefaultWorkingDirectory)
            ? Environment.CurrentDirectory
            : settings.DefaultWorkingDirectory;
        _isDarkMode = settings.ThemeMode is ThemeMode.Dark;
        _isApplicationSettingsDirty = false;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(DefaultWorkingDirectory));
        OnPropertyChanged(nameof(IsDarkMode));
        _saveAllCommand.RaiseCanExecuteChanged();
    }

    private void ApplyDefaultApplicationSettings()
    {
        _defaultWorkingDirectory = Environment.CurrentDirectory;
        _isDarkMode = false;
        _isApplicationSettingsDirty = false;
        OnPropertyChanged(nameof(DefaultWorkingDirectory));
        OnPropertyChanged(nameof(IsDarkMode));
        _saveAllCommand.RaiseCanExecuteChanged();
    }

    private ApplicationSettings CreateApplicationSettings()
        => new()
        {
            Id = _sharedState.ApplicationSettings?.Id ?? ApplicationSettings.SingletonId,
            DefaultWorkingDirectory = DefaultWorkingDirectory.Trim(),
            ThemeMode = IsDarkMode ? ThemeMode.Dark : ThemeMode.Light,
            LastChatTargetType = _sharedState.ApplicationSettings?.LastChatTargetType,
            LastChatTargetId = _sharedState.ApplicationSettings?.LastChatTargetId,
            LastAgentSessionId = _sharedState.ApplicationSettings?.LastAgentSessionId,
        };
}
