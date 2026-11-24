using System;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Orchestrations.Handoff;

/// <summary>
/// Represents one selectable handoff orchestration participant.
/// </summary>
public sealed class HandoffOrchestrationParticipantSelectionItemViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    /// <summary>
    /// Creates a new selectable participant item.
    /// </summary>
    public HandoffOrchestrationParticipantSelectionItemViewModel(
        AgentDefinition agentDefinition,
        bool isSelected,
        Action selectionChanged)
    {
        AgentDefinition = agentDefinition;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    /// <summary>
    /// Gets the agent definition.
    /// </summary>
    public AgentDefinition AgentDefinition { get; }

    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public int AgentDefinitionId => AgentDefinition.Id;

    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public string Name => AgentDefinition.Name;

    /// <summary>
    /// Gets the agent model.
    /// </summary>
    public string Model => AgentDefinition.Model;

    /// <summary>
    /// Gets the participant summary.
    /// </summary>
    public string Summary => $"{Name} · {Model}";

    /// <summary>
    /// Gets or sets whether the agent is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            _selectionChanged();
        }
    }
}
