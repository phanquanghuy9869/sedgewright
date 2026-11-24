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
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Orchestrations.GroupChat;

/// <summary>
/// Drives the group chat orchestration editor dialog.
/// </summary>
public sealed class GroupChatOrchestrationEditorViewModel : ViewModelBase
{
    private readonly IGroupChatOrchestrationRepository _repository;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _addParticipantCommand;
    private readonly GroupChatOrchestration? _orchestration;
    private readonly int? _existingId;
    private readonly IReadOnlyList<AgentDefinition> _availableAgents;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _validationText = string.Empty;
    private int _maximumIterationCount = 5;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new group chat editor view-model.
    /// </summary>
    public GroupChatOrchestrationEditorViewModel(
        GroupChatOrchestration? orchestration,
        IReadOnlyList<AgentDefinition> availableAgents,
        IGroupChatOrchestrationRepository repository,
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
            ? "Add group chat orchestration"
            : $"Edit {orchestration.Name}";
        SubtitleText = orchestration is null
            ? "Arrange the participant agents and set the round-robin iteration limit."
            : "Update the name, description, ordered participants, and iteration limit.";

        Name = orchestration?.Name ?? string.Empty;
        Description = orchestration?.Description ?? string.Empty;
        MaximumIterationCount = orchestration?.MaximumIterationCount ?? 5;

        if (orchestration is null)
        {
            if (availableAgents.Count > 0)
            {
                Participants.Add(CreateParticipantViewModel(availableAgents[0]));
            }
        }
        else
        {
            foreach (GroupChatOrchestrationParticipant participant in orchestration.Participants.OrderBy(participant => participant.StepOrder))
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
    /// Gets or sets the maximum number of group chat iterations.
    /// </summary>
    public int MaximumIterationCount
    {
        get => _maximumIterationCount;
        set
        {
            if (!SetProperty(ref _maximumIterationCount, value))
            {
                return;
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
                ValidationText = "Group chat orchestration name is required.";
                return;
            }

            if (!OrchestrationNameRules.IsValid(normalizedName))
            {
                ValidationText = OrchestrationNameRules.ValidationMessage;
                return;
            }

            if (MaximumIterationCount <= 0)
            {
                ValidationText = "Group chat orchestration must have a positive maximum iteration count.";
                return;
            }

            if (Participants.Any(participant => participant.SelectedAgent is null))
            {
                ValidationText = "Each group chat participant must select an agent.";
                return;
            }

            List<OrderedParticipantSelectionViewModel> selectedParticipants = Participants
                .Where(participant => participant.SelectedAgent is not null)
                .ToList();

            if (selectedParticipants.Count == 0)
            {
                ValidationText = "Group chat orchestration must contain at least one participant.";
                return;
            }

            HashSet<int> participantIds = [];
            foreach (OrderedParticipantSelectionViewModel participant in selectedParticipants)
            {
                if (!participantIds.Add(participant.SelectedAgent!.Id))
                {
                    ValidationText = "Group chat orchestration contains duplicate participant agent definitions.";
                    return;
                }
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A group chat orchestration named '{normalizedName}' already exists.";
                return;
            }

            bool isNew = _orchestration is null;
            GroupChatOrchestration orchestration = _orchestration ?? new()
            {
                Name = normalizedName,
            };

            orchestration.Name = normalizedName;
            orchestration.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            orchestration.MaximumIterationCount = MaximumIterationCount;

            if (isNew)
            {
                orchestration.Participants = [.. selectedParticipants.Select((participant, index) => new GroupChatOrchestrationParticipant
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
                    orchestration.Participants.Add(new GroupChatOrchestrationParticipant
                    {
                        GroupChatOrchestrationId = orchestration.Id,
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
            ValidationText = $"Could not save the group chat orchestration: {exception.Message}";
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
        HashSet<int> selectedAgentIds = Participants
            .Where(participant => participant.SelectedAgent is not null)
            .Select(participant => participant.SelectedAgent!.Id)
            .ToHashSet();

        return [.. _availableAgents.Where(agentDefinition => !selectedAgentIds.Contains(agentDefinition.Id))];
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
