using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Drives the modal chat target picker window.
/// </summary>
public sealed class ChatTargetPickerViewModel : ViewModelBase
{
    private readonly IChatTargetCatalogService _catalogService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly List<IChatTarget> _allTargets = [];
    private string _filterText = string.Empty;
    private bool _isLoading;
    private string _statusText = string.Empty;
    private IChatTarget? _selectedTarget;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<IChatTarget?>? CloseRequested;

    /// <summary>
    /// Creates a new picker view-model.
    /// </summary>
    public ChatTargetPickerViewModel(
        IChatTargetCatalogService catalogService,
        IChatTarget? selectedTarget)
    {
        _catalogService = catalogService;
        _selectedTarget = selectedTarget;

        VisibleAgents = [];
        VisibleOrchestrations = [];

        _refreshCommand = new AsyncRelayCommand(LoadAsync, CanRefresh);
        _cancelCommand = new RelayCommand(CancelSelection);
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle => "Choose chat target";

    /// <summary>
    /// Gets the picker subtitle.
    /// </summary>
    public string SubtitleText => "Search agents and orchestrations, then choose the target for the next prompt. Agents continue with an explicit session follow-up.";

    /// <summary>
    /// Gets the filtered agent rows.
    /// </summary>
    public ObservableCollection<ChatTargetPickerRowViewModel> VisibleAgents { get; }

    /// <summary>
    /// Gets the filtered orchestration rows.
    /// </summary>
    public ObservableCollection<ChatTargetPickerRowViewModel> VisibleOrchestrations { get; }

    /// <summary>
    /// Gets whether any agent rows are visible.
    /// </summary>
    public bool HasVisibleAgents => VisibleAgents.Count > 0;

    /// <summary>
    /// Gets whether any orchestration rows are visible.
    /// </summary>
    public bool HasVisibleOrchestrations => VisibleOrchestrations.Count > 0;

    /// <summary>
    /// Gets whether no targets are visible.
    /// </summary>
    public bool HasNoVisibleTargets => !HasVisibleAgents && !HasVisibleOrchestrations;

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText
    {
        get => _filterText;
        set
        {
            if (!SetProperty(ref _filterText, NormalizeText(value)))
            {
                return;
            }

            RefreshVisibleTargets();
        }
    }

    /// <summary>
    /// Gets the currently selected target.
    /// </summary>
    public IChatTarget? SelectedTarget
    {
        get => _selectedTarget;
        private set
        {
            if (!SetProperty(ref _selectedTarget, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedTargetSummary));
        }
    }

    /// <summary>
    /// Gets a compact summary for the current selection.
    /// </summary>
    public string SelectedTargetSummary => SelectedTarget is null
        ? "No target selected"
        : ChatTargetDisplayInfo.From(SelectedTarget).Summary;

    /// <summary>
    /// Gets the loading status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    /// <summary>
    /// Gets whether a status message is visible.
    /// </summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>
    /// Gets whether the picker is currently loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            _refreshCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Loads the raw catalog and rebuilds the visible rows.
    /// </summary>
    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        ChatTargetIdentity? selectedIdentity = SelectedTarget is null ? null : ChatTargetIdentity.From(SelectedTarget);

        IsLoading = true;
        StatusText = "Loading chat targets...";

        try
        {
            IReadOnlyList<IChatTarget> targets = await _catalogService.GetTargetsAsync();
            _allTargets.Clear();
            _allTargets.AddRange(targets);

            RefreshVisibleTargets();

            if (selectedIdentity is not null)
            {
                SelectedTarget = _allTargets.FirstOrDefault(target => selectedIdentity.Matches(target));
            }
            else if (SelectedTarget is not null && !_allTargets.Any(target => ChatTargetIdentity.From(target).Matches(SelectedTarget)))
            {
                SelectedTarget = null;
            }

            StatusText = _allTargets.Count == 0 ? "No chat targets are available yet." : string.Empty;
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load chat targets: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRefresh() => !IsLoading;

    private void CancelSelection()
        => CloseRequested?.Invoke(null);

    private void RefreshVisibleTargets()
    {
        string filterText = NormalizeText(SearchText);
        ChatTargetIdentity? selectedIdentity = SelectedTarget is null ? null : ChatTargetIdentity.From(SelectedTarget);

        List<ChatTargetPickerRowViewModel> agentRows = [];
        List<ChatTargetPickerRowViewModel> orchestrationRows = [];

        foreach (IChatTarget target in _allTargets)
        {
            ChatTargetDisplayInfo displayInfo = ChatTargetDisplayInfo.From(target);
            string searchText = $"{displayInfo.Label} {displayInfo.Subtitle}";
            if (!string.IsNullOrWhiteSpace(filterText) && !searchText.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ChatTargetPickerRowViewModel row = new(target, displayInfo.Label, displayInfo.Subtitle, SelectTarget);
            if (target is AgentDefinition)
            {
                agentRows.Add(row);
            }
            else
            {
                orchestrationRows.Add(row);
            }
        }

        ReplaceCollection(VisibleAgents, agentRows);
        ReplaceCollection(VisibleOrchestrations, orchestrationRows);

        if (selectedIdentity is not null && SelectedTarget is not null && !selectedIdentity.Matches(SelectedTarget))
        {
            SelectedTarget = _allTargets.FirstOrDefault(target => selectedIdentity.Matches(target));
        }

        OnPropertyChanged(nameof(HasVisibleAgents));
        OnPropertyChanged(nameof(HasVisibleOrchestrations));
        OnPropertyChanged(nameof(HasNoVisibleTargets));
        OnPropertyChanged(nameof(SelectedTargetSummary));
    }

    private void SelectTarget(IChatTarget target)
    {
        SelectedTarget = target;
        CloseRequested?.Invoke(target);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();

        foreach (T item in items)
        {
            collection.Add(item);
        }
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
