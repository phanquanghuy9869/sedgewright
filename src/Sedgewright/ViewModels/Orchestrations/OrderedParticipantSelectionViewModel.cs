using System;
using System.Collections.Generic;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Represents one ordered selectable agent row used by orchestration editors.
/// </summary>
public sealed class OrderedParticipantSelectionViewModel : ViewModelBase
{
    private readonly Action<OrderedParticipantSelectionViewModel> _moveUp;
    private readonly Action<OrderedParticipantSelectionViewModel> _moveDown;
    private readonly Action<OrderedParticipantSelectionViewModel> _remove;
    private readonly Action _changed;
    private readonly RelayCommand _moveUpCommand;
    private readonly RelayCommand _moveDownCommand;
    private readonly RelayCommand _removeCommand;
    private readonly string _itemLabel;
    private readonly string _emptySummaryText;
    private AgentDefinition? _selectedAgent;
    private int _stepOrder;
    private int _totalItems;

    /// <summary>
    /// Creates a new ordered participant row.
    /// </summary>
    public OrderedParticipantSelectionViewModel(
        IReadOnlyList<AgentDefinition> availableAgents,
        AgentDefinition? selectedAgent,
        string itemLabel,
        string emptySummaryText,
        Action<OrderedParticipantSelectionViewModel> moveUp,
        Action<OrderedParticipantSelectionViewModel> moveDown,
        Action<OrderedParticipantSelectionViewModel> remove,
        Action changed)
    {
        AvailableAgents = availableAgents;
        _selectedAgent = availableAgents.ResolveSelectedAgent(selectedAgent);
        _itemLabel = itemLabel;
        _emptySummaryText = emptySummaryText;
        _moveUp = moveUp;
        _moveDown = moveDown;
        _remove = remove;
        _changed = changed;
        _moveUpCommand = new RelayCommand(() => _moveUp(this), () => CanMoveUp);
        _moveDownCommand = new RelayCommand(() => _moveDown(this), () => CanMoveDown);
        _removeCommand = new RelayCommand(() => _remove(this));
    }

    /// <summary>
    /// Gets the available agents for selection.
    /// </summary>
    public IReadOnlyList<AgentDefinition> AvailableAgents { get; }

    /// <summary>
    /// Gets or sets the selected agent.
    /// </summary>
    public AgentDefinition? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            AgentDefinition? resolvedAgent = AvailableAgents.ResolveSelectedAgent(value);
            if (!SetProperty(ref _selectedAgent, resolvedAgent))
            {
                return;
            }

            OnPropertyChanged(nameof(ItemSummary));
            _changed();
        }
    }

    /// <summary>
    /// Gets the 1-based order for the row.
    /// </summary>
    public int StepOrder
    {
        get => _stepOrder;
        private set
        {
            if (!SetProperty(ref _stepOrder, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ItemTitle));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            _moveUpCommand.RaiseCanExecuteChanged();
            _moveDownCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the display title.
    /// </summary>
    public string ItemTitle => $"{_itemLabel} {StepOrder}";

    /// <summary>
    /// Gets the row summary.
    /// </summary>
    public string ItemSummary => SelectedAgent is null
        ? _emptySummaryText
        : $"{SelectedAgent.Name} · {SelectedAgent.Model}";

    /// <summary>
    /// Gets whether the row can move up.
    /// </summary>
    public bool CanMoveUp => StepOrder > 1;

    /// <summary>
    /// Gets whether the row can move down.
    /// </summary>
    public bool CanMoveDown => StepOrder < _totalItems;

    /// <summary>
    /// Gets the move-up command.
    /// </summary>
    public ICommand MoveUpCommand => _moveUpCommand;

    /// <summary>
    /// Gets the move-down command.
    /// </summary>
    public ICommand MoveDownCommand => _moveDownCommand;

    /// <summary>
    /// Gets the remove command.
    /// </summary>
    public ICommand RemoveCommand => _removeCommand;

    /// <summary>
    /// Updates the displayed order metadata.
    /// </summary>
    public void UpdatePosition(int stepOrder, int totalItems)
    {
        _totalItems = totalItems;
        StepOrder = stepOrder;
    }
}
