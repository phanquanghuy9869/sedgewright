using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.ProviderModelSelection;

/// <summary>
/// Holds the selectable model state for the discovery chooser window.
/// </summary>
public sealed class ProviderModelSelectionViewModel : ViewModelBase
{
    private readonly RelayCommand _enableAllCommand;
    private readonly RelayCommand _disableAllCommand;
    private readonly RelayCommand _acceptCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly List<ProviderModelSelectionItemViewModel> _allModelItems = [];
    private bool _suppressSelectionNotifications;
    private string _filterText = string.Empty;

    /// <summary>
    /// Raised when the chooser should be closed.
    /// </summary>
    public event EventHandler<ProviderModelSelectionCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// Creates a new chooser view-model.
    /// </summary>
    /// <param name="providerDisplayName">The provider display name.</param>
    /// <param name="discoveredModels">The discovered model ids.</param>
    /// <param name="initiallyEnabledModels">The currently enabled model ids.</param>
    public ProviderModelSelectionViewModel(
        string providerDisplayName,
        IReadOnlyList<string> discoveredModels,
        IReadOnlyList<string> initiallyEnabledModels)
    {
        ProviderDisplayName = providerDisplayName;
        VisibleModelItems = [];

        _enableAllCommand = new RelayCommand(() => ApplyVisibleModelSelection(true), CanEnableVisibleModels);
        _disableAllCommand = new RelayCommand(() => ApplyVisibleModelSelection(false), CanDisableVisibleModels);
        _acceptCommand = new RelayCommand(ConfirmSelection);
        _cancelCommand = new RelayCommand(CancelSelection);

        InitializeModelItems(discoveredModels, initiallyEnabledModels);
    }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    public string ProviderDisplayName { get; }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle => $"Choose enabled models for {ProviderDisplayName}";

    /// <summary>
    /// Gets the filtered model items.
    /// </summary>
    public ObservableCollection<ProviderModelSelectionItemViewModel> VisibleModelItems { get; }

    /// <summary>
    /// Gets whether any model items are visible.
    /// </summary>
    public bool HasVisibleModelItems => VisibleModelItems.Count > 0;

    /// <summary>
    /// Gets whether no model items are visible.
    /// </summary>
    public bool HasNoVisibleModelItems => !HasVisibleModelItems;

    /// <summary>
    /// Gets or sets the current filter text.
    /// </summary>
    public string ModelFilterText
    {
        get => _filterText;
        set
        {
            if (!SetProperty(ref _filterText, NormalizeText(value)))
            {
                return;
            }

            RefreshVisibleModelItems();
        }
    }

    /// <summary>
    /// Gets the command that enables all visible models.
    /// </summary>
    public ICommand EnableAllCommand => _enableAllCommand;

    /// <summary>
    /// Gets the command that disables all visible models.
    /// </summary>
    public ICommand DisableAllCommand => _disableAllCommand;

    /// <summary>
    /// Gets the command that accepts the selection.
    /// </summary>
    public ICommand AcceptCommand => _acceptCommand;

    /// <summary>
    /// Gets the command that cancels the selection.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets the currently selected model ids.
    /// </summary>
    public IReadOnlyList<string> SelectedModelIds => _allModelItems
        .Where(model => model.IsEnabled)
        .Select(model => model.ModelId)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void InitializeModelItems(IReadOnlyList<string> discoveredModels, IReadOnlyList<string> initiallyEnabledModels)
    {
        HashSet<string> selectedModelIds = [.. initiallyEnabledModels
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId.Trim())];

        _allModelItems.Clear();
        foreach (string modelId in discoveredModels
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase))
        {
            bool isEnabled = selectedModelIds.Contains(modelId);
            _allModelItems.Add(new ProviderModelSelectionItemViewModel(modelId, isEnabled, OnSelectionChanged));
        }

        RefreshVisibleModelItems();
    }

    private void ApplyVisibleModelSelection(bool isEnabled)
    {
        _suppressSelectionNotifications = true;
        try
        {
            foreach (ProviderModelSelectionItemViewModel model in VisibleModelItems)
            {
                model.IsEnabled = isEnabled;
            }
        }
        finally
        {
            _suppressSelectionNotifications = false;
        }

        NotifySelectionStateChanged();
    }

    private bool CanEnableVisibleModels()
        => VisibleModelItems.Any(model => !model.IsEnabled);

    private bool CanDisableVisibleModels()
        => VisibleModelItems.Any(model => model.IsEnabled);

    private void ConfirmSelection()
        => CloseRequested?.Invoke(this, new ProviderModelSelectionCloseRequestedEventArgs(true));

    private void CancelSelection()
        => CloseRequested?.Invoke(this, new ProviderModelSelectionCloseRequestedEventArgs(false));

    private void OnSelectionChanged()
    {
        if (_suppressSelectionNotifications)
        {
            return;
        }

        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectedModelIds));
        _enableAllCommand.RaiseCanExecuteChanged();
        _disableAllCommand.RaiseCanExecuteChanged();
    }

    private void RefreshVisibleModelItems()
    {
        string filterText = NormalizeText(ModelFilterText);
        IEnumerable<ProviderModelSelectionItemViewModel> filteredItems = string.IsNullOrWhiteSpace(filterText)
            ? _allModelItems
            : _allModelItems.Where(model => model.ModelId.Contains(filterText, StringComparison.OrdinalIgnoreCase));

        VisibleModelItems.Clear();
        foreach (ProviderModelSelectionItemViewModel modelItem in filteredItems)
        {
            VisibleModelItems.Add(modelItem);
        }

        OnPropertyChanged(nameof(HasVisibleModelItems));
        OnPropertyChanged(nameof(HasNoVisibleModelItems));
        _enableAllCommand.RaiseCanExecuteChanged();
        _disableAllCommand.RaiseCanExecuteChanged();
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
