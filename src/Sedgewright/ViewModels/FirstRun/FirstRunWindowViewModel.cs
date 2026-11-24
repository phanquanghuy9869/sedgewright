using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Infras.Storage;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.FirstRun;

/// <summary>
/// Holds the minimal first-run provider key flow for Sedgewright.
/// </summary>
public sealed class FirstRunWindowViewModel : ViewModelBase
{
    private const string ValidationMessage = "Configure at least one provider and select models when required before continuing.";

    private readonly LLMProviderRepository _providerRepository;
    private readonly ISharedState _sharedState;
    private readonly AsyncRelayCommand _completeCommand;
    private string _defaultWorkingDirectory = Environment.CurrentDirectory;
    private bool _isDarkMode;
    private string _validationText = string.Empty;
    private string _saveErrorText = string.Empty;
    private bool _isSaving;

    public FirstRunWindowViewModel(LLMProviderRepository providerRepository, ISharedState sharedState)
    {
        _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
        _isDarkMode = _sharedState.ApplicationSettings?.ThemeMode is ThemeMode.Dark;
        Providers = [.. ProviderCatalog.Entries.Select(entry => new ProviderDraftViewModel(entry.ProviderKind, entry.DisplayName))];

        foreach (ProviderDraftViewModel provider in Providers)
        {
            provider.PropertyChanged += OnProviderPropertyChanged;
        }

        BrowseDefaultWorkingDirectoryCommand = new RelayCommand(RequestDefaultWorkingDirectorySelection);
        _completeCommand = new AsyncRelayCommand(CompleteAsync, () => CanContinue);
        RefreshStatusText();
    }

    public event EventHandler? Completed;

    public event EventHandler? DefaultWorkingDirectorySelectionRequested;

    public ObservableCollection<ProviderDraftViewModel> Providers { get; }

    public ICommand ContinueCommand => _completeCommand;

    public string TitleText => HeaderText;

    public string SubtitleText => SubText;

    public ICommand CompleteCommand => _completeCommand;

    public bool CanContinue => !_isSaving && Providers.Any(provider => provider.IsReadyToPersist);

    public string StatusText => !string.IsNullOrWhiteSpace(_saveErrorText)
        ? _saveErrorText
        : _validationText;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(_saveErrorText)
        || !string.IsNullOrWhiteSpace(_validationText);

    public string DefaultWorkingDirectory
    {
        get => _defaultWorkingDirectory;
        set => SetProperty(ref _defaultWorkingDirectory, value ?? string.Empty);
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
        }
    }

    public ICommand BrowseDefaultWorkingDirectoryCommand { get; }

    public static string HeaderText => "Enter provider keys for the LLMs you want to use.";

    public static string SubText => "Configure a provider, choose your defaults, and continue to Sedgewright.";

    private void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProviderDraftViewModel.ApiKey)
            or nameof(ProviderDraftViewModel.IsEnabled)
            or nameof(ProviderDraftViewModel.EnabledModelIds)
            or nameof(ProviderDraftViewModel.IsReadyToPersist))
        {
            _completeCommand.RaiseCanExecuteChanged();
            RefreshStatusText();
        }
    }

    private async Task CompleteAsync()
    {
        if (!CanContinue)
        {
            RefreshStatusText();
            return;
        }

        try
        {
            _isSaving = true;
            _completeCommand.RaiseCanExecuteChanged();
            SetSaveErrorText(string.Empty);

            List<LLMProvider> configuredProviders = Providers
                .Where(provider => provider.IsReadyToPersist)
                .Select(provider => new LLMProvider
                {
                    ProviderKind = Enum.TryParse<LlmProviderKind>(provider.ProviderKind, ignoreCase: true, out LlmProviderKind providerKind)
                        ? providerKind
                        : throw new InvalidOperationException($"Unknown provider kind '{provider.ProviderKind}'."),
                    DisplayName = provider.DisplayName,
                    BaseUrl = provider.BaseUrl,
                    ApiKey = provider.ApiKey,
                    EnabledModelsJson = JsonSerializer.Serialize(provider.EnabledModelIds),
                })
                .ToList();

            foreach (LLMProvider configuredProvider in configuredProviders)
            {
                _providerRepository.Create(configuredProvider);
            }

            await _providerRepository.SaveChangesAsync();
            await _sharedState.SaveApplicationSettingsAsync(new ApplicationSettings
            {
                Id = _sharedState.ApplicationSettings?.Id ?? ApplicationSettings.SingletonId,
                DefaultWorkingDirectory = DefaultWorkingDirectory.Trim(),
                ThemeMode = IsDarkMode ? ThemeMode.Dark : ThemeMode.Light,
            });
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SetSaveErrorText($"Couldn't finish setup: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
            _completeCommand.RaiseCanExecuteChanged();
            RefreshStatusText();
        }
    }

    private void RefreshStatusText()
    {
        SetValidationText(CanContinue ? string.Empty : ValidationMessage);
    }

    private void SetValidationText(string text)
    {
        string normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (_validationText == normalizedText)
        {
            return;
        }

        _validationText = normalizedText;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatusText));
    }

    private void SetSaveErrorText(string text)
    {
        string normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (_saveErrorText == normalizedText)
        {
            return;
        }

        _saveErrorText = normalizedText;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatusText));
    }

    internal void ApplyDefaultWorkingDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            SetSaveErrorText("The selected directory is not available as a local path.");
            return;
        }

        DefaultWorkingDirectory = directory.Trim();
        SetSaveErrorText(string.Empty);
    }

    internal void ShowStatus(string status)
        => SetSaveErrorText(status);

    private void RequestDefaultWorkingDirectorySelection()
        => DefaultWorkingDirectorySelectionRequested?.Invoke(this, EventArgs.Empty);
}
