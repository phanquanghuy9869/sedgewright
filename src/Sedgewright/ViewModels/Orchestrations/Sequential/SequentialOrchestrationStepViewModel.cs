using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Orchestrations.Sequential;

/// <summary>
/// Represents one sequential orchestration step.
/// </summary>
public sealed class SequentialOrchestrationStepViewModel : ViewModelBase
{
    private readonly Action<SequentialOrchestrationStepViewModel> _moveUp;
    private readonly Action<SequentialOrchestrationStepViewModel> _moveDown;
    private readonly Action<SequentialOrchestrationStepViewModel> _remove;
    private readonly Action _changed;
    private readonly RelayCommand _moveUpCommand;
    private readonly RelayCommand _moveDownCommand;
    private readonly RelayCommand _removeCommand;
    private AgentDefinition? _selectedAgent;
    private int _stepOrder;
    private int _totalSteps;

    /// <summary>
    /// Creates a sequential step view-model.
    /// </summary>
    public SequentialOrchestrationStepViewModel(
        IReadOnlyList<AgentDefinition> availableAgents,
        AgentDefinition? selectedAgent,
        Action<SequentialOrchestrationStepViewModel> moveUp,
        Action<SequentialOrchestrationStepViewModel> moveDown,
        Action<SequentialOrchestrationStepViewModel> remove,
        Action changed)
    {
        AvailableAgents = availableAgents;
        _selectedAgent = ResolveSelectedAgent(availableAgents, selectedAgent);
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
            AgentDefinition? resolvedAgent = ResolveSelectedAgent(AvailableAgents, value);
            if (!SetProperty(ref _selectedAgent, resolvedAgent))
            {
                return;
            }

            OnPropertyChanged(nameof(StepSummary));
            _changed();
        }
    }

    /// <summary>
    /// Gets the 1-based step order.
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

            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            _moveUpCommand.RaiseCanExecuteChanged();
            _moveDownCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the step title.
    /// </summary>
    public string StepTitle => $"Step {StepOrder}";

    /// <summary>
    /// Gets the step summary.
    /// </summary>
    public string StepSummary => SelectedAgent is null
        ? "Select an agent for this step."
        : $"{SelectedAgent.Name} · {SelectedAgent.Model}";

    /// <summary>
    /// Gets whether the step can move up.
    /// </summary>
    public bool CanMoveUp => StepOrder > 1;

    /// <summary>
    /// Gets whether the step can move down.
    /// </summary>
    public bool CanMoveDown => StepOrder < _totalSteps;

    /// <summary>
    /// Gets the move up command.
    /// </summary>
    public ICommand MoveUpCommand => _moveUpCommand;

    /// <summary>
    /// Gets the move down command.
    /// </summary>
    public ICommand MoveDownCommand => _moveDownCommand;

    /// <summary>
    /// Gets the remove command.
    /// </summary>
    public ICommand RemoveCommand => _removeCommand;

    /// <summary>
    /// Updates the displayed position metadata.
    /// </summary>
    public void UpdatePosition(int stepOrder, int totalSteps)
    {
        _totalSteps = totalSteps;
        StepOrder = stepOrder;
    }

    private static AgentDefinition? ResolveSelectedAgent(
        IReadOnlyList<AgentDefinition> availableAgents,
        AgentDefinition? selectedAgent)
    {
        if (selectedAgent is null)
        {
            return availableAgents.Count > 0 ? availableAgents[0] : null;
        }

        return availableAgents.FirstOrDefault(agent => agent.Id == selectedAgent.Id)
            ?? selectedAgent;
    }
}
