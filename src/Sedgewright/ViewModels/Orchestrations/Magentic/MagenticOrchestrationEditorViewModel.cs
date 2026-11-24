using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations;

namespace Sedgewright.ViewModels.Orchestrations.Magentic;

/// <summary>
/// Drives the Magentic orchestration editor dialog.
/// </summary>
public sealed class MagenticOrchestrationEditorViewModel : ViewModelBase
{
    private readonly IMagenticOrchestrationRepository _repository;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _addParticipantCommand;
    private readonly MagenticOrchestration? _orchestration;
    private readonly int? _existingId;
    private readonly IReadOnlyList<AgentDefinition> _availableAgents;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _validationText = string.Empty;
    private string _maximumRoundCount = "10";
    private string _maximumStallCount = "3";
    private string _maximumResetCount = "2";
    private AgentDefinition? _selectedManagerAgent;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new Magentic editor view-model.
    /// </summary>
    public MagenticOrchestrationEditorViewModel(
        MagenticOrchestration? orchestration,
        IReadOnlyList<AgentDefinition> availableAgents,
        IMagenticOrchestrationRepository repository,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _nameExistsAsync = nameExistsAsync;
        _orchestration = orchestration;
        _existingId = orchestration?.Id;
        _availableAgents = availableAgents;

        Participants = [];
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);
        _addParticipantCommand = new RelayCommand(AddParticipant, CanAddParticipant);

        WindowTitle = orchestration is null
            ? "Add Magentic orchestration"
            : $"Edit {orchestration.Name}";
        SubtitleText = orchestration is null
            ? "Choose the manager agent, ordered specialist participants, and planner limits."
            : "Update the manager agent, participant order, and planner settings.";

        Name = orchestration?.Name ?? string.Empty;
        Description = orchestration?.Description ?? string.Empty;
        MaximumRoundCount = (orchestration?.MaximumRoundCount ?? 10).ToString(CultureInfo.InvariantCulture);
        MaximumStallCount = (orchestration?.MaximumStallCount ?? 3).ToString(CultureInfo.InvariantCulture);
        MaximumResetCount = (orchestration?.MaximumResetCount ?? 2).ToString(CultureInfo.InvariantCulture);

        _selectedManagerAgent = availableAgents.ResolveSelectedAgent(orchestration?.ManagerAgentDefinition);

        if (orchestration is null)
        {
            AgentDefinition? initialParticipant = GetUnusedAgents().FirstOrDefault();
            if (initialParticipant is not null)
            {
                Participants.Add(CreateParticipantViewModel(initialParticipant));
            }
        }
        else
        {
            foreach (MagenticOrchestrationParticipant participant in orchestration.Participants.OrderBy(participant => participant.StepOrder))
            {
                Participants.Add(CreateParticipantViewModel(participant.AgentDefinition));
            }
        }

        ReindexParticipants();
        _addParticipantCommand.RaiseCanExecuteChanged();
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
    /// Gets the ordered participants.
    /// </summary>
    public ObservableCollection<OrderedParticipantSelectionViewModel> Participants { get; }

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
    /// Gets or sets the maximum number of Magentic rounds.
    /// </summary>
    public string MaximumRoundCount
    {
        get => _maximumRoundCount;
        set
        {
            if (!SetProperty(ref _maximumRoundCount, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of Magentic stalls.
    /// </summary>
    public string MaximumStallCount
    {
        get => _maximumStallCount;
        set
        {
            if (!SetProperty(ref _maximumStallCount, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of Magentic resets.
    /// </summary>
    public string MaximumResetCount
    {
        get => _maximumResetCount;
        set
        {
            if (!SetProperty(ref _maximumResetCount, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the manager agent.
    /// </summary>
    public AgentDefinition? SelectedManagerAgent
    {
        get => _selectedManagerAgent;
        set
        {
            AgentDefinition? resolvedAgent = AvailableAgents.ResolveSelectedAgent(value);
            if (!SetProperty(ref _selectedManagerAgent, resolvedAgent))
            {
                return;
            }

            _addParticipantCommand.RaiseCanExecuteChanged();
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
    /// Gets the add-participant command.
    /// </summary>
    public ICommand AddParticipantCommand => _addParticipantCommand;

    private bool CanSave()
        => !_isSaving
            && !string.IsNullOrWhiteSpace(Name);

    private bool CanAddParticipant()
        => !_isSaving
            && GetUnusedAgents().Count > 0;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();
        _addParticipantCommand.RaiseCanExecuteChanged();

        try
        {
            string normalizedName = Normalize(Name);
            if (normalizedName.Length == 0)
            {
                ValidationText = "Magentic orchestration name is required.";
                return;
            }

            if (!OrchestrationNameRules.IsValid(normalizedName))
            {
                ValidationText = OrchestrationNameRules.ValidationMessage;
                return;
            }

            if (SelectedManagerAgent is null)
            {
                ValidationText = "Magentic orchestration must choose a manager agent.";
                return;
            }

            if (!int.TryParse(MaximumRoundCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maximumRoundCount)
                || maximumRoundCount <= 0)
            {
                ValidationText = "Magentic orchestration must have a positive whole-number maximum round count.";
                return;
            }

            if (!int.TryParse(MaximumStallCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maximumStallCount)
                || maximumStallCount < 0)
            {
                ValidationText = "Magentic orchestration must have a non-negative whole-number maximum stall count.";
                return;
            }

            if (!int.TryParse(MaximumResetCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maximumResetCount)
                || maximumResetCount < 0)
            {
                ValidationText = "Magentic orchestration must have a non-negative whole-number maximum reset count.";
                return;
            }

            if (Participants.Any(participant => participant.SelectedAgent is null))
            {
                ValidationText = "Each Magentic participant must select an agent.";
                return;
            }

            List<OrderedParticipantSelectionViewModel> selectedParticipants = Participants
                .Where(participant => participant.SelectedAgent is not null)
                .ToList();

            if (selectedParticipants.Count == 0)
            {
                ValidationText = "Magentic orchestration must contain at least one participant.";
                return;
            }

            HashSet<int> participantIds = [];
            foreach (OrderedParticipantSelectionViewModel participant in selectedParticipants)
            {
                if (!participantIds.Add(participant.SelectedAgent!.Id))
                {
                    ValidationText = "Magentic orchestration contains duplicate participant agent definitions.";
                    return;
                }
            }

            if (participantIds.Contains(SelectedManagerAgent.Id))
            {
                ValidationText = "Magentic orchestration must use a manager agent that is distinct from its participants.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A Magentic orchestration named '{normalizedName}' already exists.";
                return;
            }

            bool isNew = _orchestration is null;
            MagenticOrchestration orchestration = _orchestration ?? new()
            {
                Name = normalizedName,
            };

            orchestration.Name = normalizedName;
            orchestration.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            orchestration.ManagerAgentDefinitionId = SelectedManagerAgent.Id;
            orchestration.MaximumRoundCount = maximumRoundCount;
            orchestration.MaximumStallCount = maximumStallCount;
            orchestration.MaximumResetCount = maximumResetCount;

            if (isNew)
            {
                orchestration.Participants = [.. selectedParticipants.Select((participant, index) => new MagenticOrchestrationParticipant
                {
                    AgentDefinitionId = participant.SelectedAgent!.Id,
                    StepOrder = index + 1,
                })];
            }
            else
            {
                orchestration.Participants.Clear();
                for (int index = 0; index < selectedParticipants.Count; index++)
                {
                    orchestration.Participants.Add(new MagenticOrchestrationParticipant
                    {
                        MagenticOrchestrationId = orchestration.Id,
                        AgentDefinitionId = selectedParticipants[index].SelectedAgent!.Id,
                        StepOrder = index + 1,
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
            ValidationText = $"Could not save the Magentic orchestration: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
            _addParticipantCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void AddParticipant()
    {
        AgentDefinition? nextAgent = GetUnusedAgents().FirstOrDefault();
        if (nextAgent is null)
        {
            return;
        }

        Participants.Add(CreateParticipantViewModel(nextAgent));
        ReindexParticipants();
        OnEditorFieldChanged();
    }

    private OrderedParticipantSelectionViewModel CreateParticipantViewModel(AgentDefinition? selectedAgent)
        => new(
            _availableAgents,
            selectedAgent,
            "Participant",
            "Select an agent for this participant.",
            MoveParticipantUp,
            MoveParticipantDown,
            RemoveParticipant,
            OnEditorFieldChanged);

    private void MoveParticipantUp(OrderedParticipantSelectionViewModel participant)
    {
        int index = Participants.IndexOf(participant);
        if (index <= 0)
        {
            return;
        }

        Participants.Move(index, index - 1);
        ReindexParticipants();
        OnEditorFieldChanged();
    }

    private void MoveParticipantDown(OrderedParticipantSelectionViewModel participant)
    {
        int index = Participants.IndexOf(participant);
        if (index < 0 || index >= Participants.Count - 1)
        {
            return;
        }

        Participants.Move(index, index + 1);
        ReindexParticipants();
        OnEditorFieldChanged();
    }

    private void RemoveParticipant(OrderedParticipantSelectionViewModel participant)
    {
        if (!Participants.Remove(participant))
        {
            return;
        }

        ReindexParticipants();
        OnEditorFieldChanged();
    }

    private void ReindexParticipants()
    {
        for (int index = 0; index < Participants.Count; index++)
        {
            Participants[index].UpdatePosition(index + 1, Participants.Count);
        }
    }

    private List<AgentDefinition> GetUnusedAgents()
    {
        HashSet<int> usedParticipantIds = Participants
            .Where(participant => participant.SelectedAgent is not null)
            .Select(participant => participant.SelectedAgent!.Id)
            .ToHashSet();

        if (SelectedManagerAgent is not null)
        {
            usedParticipantIds.Add(SelectedManagerAgent.Id);
        }

        return [.. _availableAgents.Where(agentDefinition => !usedParticipantIds.Contains(agentDefinition.Id))];
    }

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        _saveCommand.RaiseCanExecuteChanged();
        _addParticipantCommand.RaiseCanExecuteChanged();
    }

    private static string Normalize(string text)
        => text.Trim();
}
