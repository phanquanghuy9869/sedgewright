using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Orchestrations.Handoff;

/// <summary>
/// Drives the handoff orchestration editor dialog.
/// </summary>
public sealed class HandoffOrchestrationEditorViewModel : ViewModelBase
{
    private readonly IHandoffOrchestrationRepository _repository;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _addRouteCommand;
    private readonly HandoffOrchestration? _orchestration;
    private readonly int? _existingId;
    private readonly IReadOnlyList<AgentDefinition> _availableAgents;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _validationText = string.Empty;
    private bool _isAutonomousModeEnabled = true;
    private AgentDefinition? _selectedStartAgent;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new handoff editor view-model.
    /// </summary>
    public HandoffOrchestrationEditorViewModel(
        HandoffOrchestration? orchestration,
        IReadOnlyList<AgentDefinition> availableAgents,
        IHandoffOrchestrationRepository repository,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _nameExistsAsync = nameExistsAsync;
        _orchestration = orchestration;
        _existingId = orchestration?.Id;
        _availableAgents = availableAgents;

        Participants = [];
        Routes = [];
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);
        _addRouteCommand = new RelayCommand(AddRoute, CanAddRoute);

        WindowTitle = orchestration is null
            ? "Add handoff orchestration"
            : $"Edit {orchestration.Name}";
        SubtitleText = orchestration is null
            ? "Choose a start agent, participant set, and optional explicit routes."
            : "Update the name, start agent, participants, autonomy mode, and routes.";

        Name = orchestration?.Name ?? string.Empty;
        Description = orchestration?.Description ?? string.Empty;
        IsAutonomousModeEnabled = orchestration?.IsAutonomousModeEnabled ?? true;

        HashSet<int> selectedAgentIds = orchestration?.Participants
            .Select(participant => participant.AgentDefinitionId)
            .ToHashSet() ?? [];

        for (int index = 0; index < availableAgents.Count; index++)
        {
            AgentDefinition agentDefinition = availableAgents[index];
            bool isSelected = orchestration is null
                ? index == 0
                : selectedAgentIds.Contains(agentDefinition.Id);

            Participants.Add(new HandoffOrchestrationParticipantSelectionItemViewModel(
                agentDefinition,
                isSelected,
                OnEditorFieldChanged));
        }

        SelectedStartAgent = availableAgents.ResolveSelectedAgent(orchestration?.StartAgentDefinition)
            ?? Participants.FirstOrDefault(participant => participant.IsSelected)?.AgentDefinition
            ?? availableAgents.FirstOrDefault();

        if (orchestration is not null)
        {
            foreach (HandoffOrchestrationRoute route in orchestration.Routes.OrderBy(route => route.SourceAgentDefinitionId).ThenBy(route => route.TargetAgentDefinitionId))
            {
                Routes.Add(CreateRouteViewModel(route.SourceAgentDefinition, route.TargetAgentDefinition));
            }
        }

        _addRouteCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Gets the editor title.
    /// </summary>
    public string WindowTitle { get; }

    /// <summary>
    /// Gets the editor subtitle.
    /// </summary>
    public string SubtitleText { get; }

    /// <summary>
    /// Gets the available agents.
    /// </summary>
    public IReadOnlyList<AgentDefinition> AvailableAgents => _availableAgents;

    /// <summary>
    /// Gets the selectable participants.
    /// </summary>
    public ObservableCollection<HandoffOrchestrationParticipantSelectionItemViewModel> Participants { get; }

    /// <summary>
    /// Gets the editable routes.
    /// </summary>
    public ObservableCollection<HandoffOrchestrationRouteViewModel> Routes { get; }

    /// <summary>
    /// Gets or sets the orchestration name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the orchestration description.
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (!SetProperty(ref _description, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether autonomous mode is enabled.
    /// </summary>
    public bool IsAutonomousModeEnabled
    {
        get => _isAutonomousModeEnabled;
        set
        {
            if (!SetProperty(ref _isAutonomousModeEnabled, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the selected start agent.
    /// </summary>
    public AgentDefinition? SelectedStartAgent
    {
        get => _selectedStartAgent;
        set
        {
            AgentDefinition? resolvedAgent = AvailableAgents.ResolveSelectedAgent(value);
            if (!SetProperty(ref _selectedStartAgent, resolvedAgent))
            {
                return;
            }

            if (value is not null)
            {
                HandoffOrchestrationParticipantSelectionItemViewModel? participant = Participants.FirstOrDefault(item => item.AgentDefinitionId == value.Id);
                if (participant is not null && !participant.IsSelected)
                {
                    participant.IsSelected = true;
                }
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets the validation text.
    /// </summary>
    public string ValidationText
    {
        get => _validationText;
        private set
        {
            if (!SetProperty(ref _validationText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasValidationText));
        }
    }

    /// <summary>
    /// Gets whether validation text is visible.
    /// </summary>
    public bool HasValidationText => !string.IsNullOrWhiteSpace(ValidationText);

    /// <summary>
    /// Gets the save command.
    /// </summary>
    public ICommand SaveCommand => _saveCommand;

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets the add-route command.
    /// </summary>
    public ICommand AddRouteCommand => _addRouteCommand;

    private bool CanSave()
        => !_isSaving
            && !string.IsNullOrWhiteSpace(Name);

    private bool CanAddRoute()
        => !_isSaving
            && GetSelectedParticipants().Count > 1;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();
        _addRouteCommand.RaiseCanExecuteChanged();

        try
        {
            string normalizedName = Normalize(Name);
            if (normalizedName.Length == 0)
            {
                ValidationText = "Handoff orchestration name is required.";
                return;
            }

            if (!OrchestrationNameRules.IsValid(normalizedName))
            {
                ValidationText = OrchestrationNameRules.ValidationMessage;
                return;
            }

            List<HandoffOrchestrationParticipantSelectionItemViewModel> selectedParticipants = GetSelectedParticipants();
            if (selectedParticipants.Count == 0)
            {
                ValidationText = "Handoff orchestration must contain at least one participant.";
                return;
            }

            AgentDefinition? startAgent = SelectedStartAgent;
            if (startAgent is null)
            {
                ValidationText = "Handoff orchestration must choose a start agent.";
                return;
            }

            HashSet<int> participantIds = selectedParticipants.Select(participant => participant.AgentDefinitionId).ToHashSet();
            if (!participantIds.Contains(startAgent.Id))
            {
                ValidationText = "Handoff orchestration must include the configured start agent as a participant.";
                return;
            }

            HashSet<(int SourceId, int TargetId)> routePairs = [];
            foreach (HandoffOrchestrationRouteViewModel route in Routes)
            {
                if (route.SelectedSourceAgent is null || route.SelectedTargetAgent is null)
                {
                    ValidationText = "Each handoff route must select both source and target agents.";
                    return;
                }

                if (!participantIds.Contains(route.SelectedSourceAgent.Id) || !participantIds.Contains(route.SelectedTargetAgent.Id))
                {
                    ValidationText = "Handoff orchestration contains a route that references an agent outside of the participant list.";
                    return;
                }

                if (route.SelectedSourceAgent.Id == route.SelectedTargetAgent.Id)
                {
                    ValidationText = $"Handoff orchestration contains a self-route for agent definition id {route.SelectedSourceAgent.Id}.";
                    return;
                }

                if (!routePairs.Add((route.SelectedSourceAgent.Id, route.SelectedTargetAgent.Id)))
                {
                    ValidationText = "Handoff orchestration contains duplicate routes.";
                    return;
                }
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A handoff orchestration named '{normalizedName}' already exists.";
                return;
            }

            bool isNew = _orchestration is null;
            HandoffOrchestration orchestration = _orchestration ?? new()
            {
                Name = normalizedName,
            };

            orchestration.Name = normalizedName;
            orchestration.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            orchestration.StartAgentDefinitionId = startAgent.Id;
            orchestration.IsAutonomousModeEnabled = IsAutonomousModeEnabled;

            if (isNew)
            {
                orchestration.Participants = [.. selectedParticipants.Select(participant => new HandoffOrchestrationParticipant
                {
                    AgentDefinitionId = participant.AgentDefinitionId,
                })];
                orchestration.Routes = [.. routePairs.Select(route => new HandoffOrchestrationRoute
                {
                    SourceAgentDefinitionId = route.SourceId,
                    TargetAgentDefinitionId = route.TargetId,
                })];
            }
            else
            {
                List<HandoffOrchestrationParticipant> participantsToRemove = orchestration.Participants
                    .Where(participant => !participantIds.Contains(participant.AgentDefinitionId))
                    .ToList();

                foreach (HandoffOrchestrationParticipant participant in participantsToRemove)
                {
                    orchestration.Participants.Remove(participant);
                }

                HashSet<int> existingParticipantIds = orchestration.Participants
                    .Select(participant => participant.AgentDefinitionId)
                    .ToHashSet();

                foreach (int agentDefinitionId in participantIds.Except(existingParticipantIds))
                {
                    orchestration.Participants.Add(new HandoffOrchestrationParticipant
                    {
                        HandoffOrchestrationId = orchestration.Id,
                        AgentDefinitionId = agentDefinitionId,
                    });
                }

                List<HandoffOrchestrationRoute> routesToRemove = orchestration.Routes
                    .Where(route => !routePairs.Contains((route.SourceAgentDefinitionId, route.TargetAgentDefinitionId)))
                    .ToList();

                foreach (HandoffOrchestrationRoute route in routesToRemove)
                {
                    orchestration.Routes.Remove(route);
                }

                HashSet<(int SourceId, int TargetId)> existingRoutePairs = orchestration.Routes
                    .Select(route => (route.SourceAgentDefinitionId, route.TargetAgentDefinitionId))
                    .ToHashSet();

                foreach ((int SourceId, int TargetId) route in routePairs.Except(existingRoutePairs))
                {
                    orchestration.Routes.Add(new HandoffOrchestrationRoute
                    {
                        HandoffOrchestrationId = orchestration.Id,
                        SourceAgentDefinitionId = route.SourceId,
                        TargetAgentDefinitionId = route.TargetId,
                    });
                }
            }

            ValidationText = string.Empty;

            if (isNew)
            {
                _repository.Create(orchestration);
            }

            await _repository.SaveChangesAsync();

            CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());
            return;
        }
        catch (DbUpdateException exception)
        {
            ValidationText = $"Could not save the handoff orchestration: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
            _addRouteCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void AddRoute()
    {
        List<HandoffOrchestrationParticipantSelectionItemViewModel> selectedParticipants = GetSelectedParticipants();
        if (selectedParticipants.Count < 2)
        {
            return;
        }

        Routes.Add(CreateRouteViewModel(
            selectedParticipants[0].AgentDefinition,
            selectedParticipants[1].AgentDefinition));

        OnEditorFieldChanged();
    }

    private HandoffOrchestrationRouteViewModel CreateRouteViewModel(AgentDefinition? selectedSourceAgent, AgentDefinition? selectedTargetAgent)
        => new(
            _availableAgents,
            selectedSourceAgent,
            selectedTargetAgent,
            RemoveRoute,
            OnEditorFieldChanged);

    private void RemoveRoute(HandoffOrchestrationRouteViewModel route)
    {
        if (!Routes.Remove(route))
        {
            return;
        }

        OnEditorFieldChanged();
    }

    private List<HandoffOrchestrationParticipantSelectionItemViewModel> GetSelectedParticipants()
        => Participants.Where(participant => participant.IsSelected).ToList();

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        _saveCommand.RaiseCanExecuteChanged();
        _addRouteCommand.RaiseCanExecuteChanged();
    }

    private static string Normalize(string text)
        => text.Trim();
}
