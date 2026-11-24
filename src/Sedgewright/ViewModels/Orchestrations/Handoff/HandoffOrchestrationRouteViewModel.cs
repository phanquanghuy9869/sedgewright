using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Orchestrations.Handoff;

/// <summary>
/// Represents one editable handoff route.
/// </summary>
public sealed class HandoffOrchestrationRouteViewModel : ViewModelBase
{
    private readonly Action<HandoffOrchestrationRouteViewModel> _remove;
    private readonly Action _changed;
    private readonly RelayCommand _removeCommand;
    private AgentDefinition? _selectedSourceAgent;
    private AgentDefinition? _selectedTargetAgent;

    /// <summary>
    /// Creates a new route editor item.
    /// </summary>
    public HandoffOrchestrationRouteViewModel(
        IReadOnlyList<AgentDefinition> availableAgents,
        AgentDefinition? selectedSourceAgent,
        AgentDefinition? selectedTargetAgent,
        Action<HandoffOrchestrationRouteViewModel> remove,
        Action changed)
    {
        AvailableAgents = availableAgents;
        _selectedSourceAgent = availableAgents.ResolveSelectedAgent(selectedSourceAgent);
        _selectedTargetAgent = availableAgents.ResolveSelectedAgent(
            selectedTargetAgent ?? availableAgents.Skip(_selectedSourceAgent is null ? 0 : 1).FirstOrDefault());
        _remove = remove;
        _changed = changed;
        _removeCommand = new RelayCommand(() => _remove(this));
    }

    /// <summary>
    /// Gets the available agents for the route endpoints.
    /// </summary>
    public IReadOnlyList<AgentDefinition> AvailableAgents { get; }

    /// <summary>
    /// Gets or sets the source agent.
    /// </summary>
    public AgentDefinition? SelectedSourceAgent
    {
        get => _selectedSourceAgent;
        set
        {
            AgentDefinition? resolvedAgent = AvailableAgents.ResolveSelectedAgent(value);
            if (!SetProperty(ref _selectedSourceAgent, resolvedAgent))
            {
                return;
            }

            OnPropertyChanged(nameof(Summary));
            _changed();
        }
    }

    /// <summary>
    /// Gets or sets the target agent.
    /// </summary>
    public AgentDefinition? SelectedTargetAgent
    {
        get => _selectedTargetAgent;
        set
        {
            AgentDefinition? resolvedAgent = AvailableAgents.ResolveSelectedAgent(value);
            if (!SetProperty(ref _selectedTargetAgent, resolvedAgent))
            {
                return;
            }

            OnPropertyChanged(nameof(Summary));
            _changed();
        }
    }

    /// <summary>
    /// Gets the route summary.
    /// </summary>
    public string Summary => SelectedSourceAgent is null || SelectedTargetAgent is null
        ? "Choose source and target agents."
        : $"{SelectedSourceAgent.Name} → {SelectedTargetAgent.Name}";

    /// <summary>
    /// Gets the remove command.
    /// </summary>
    public ICommand RemoveCommand => _removeCommand;
}
