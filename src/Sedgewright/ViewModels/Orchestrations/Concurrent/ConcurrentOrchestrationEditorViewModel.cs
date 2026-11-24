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
using Sedgewright.ViewModels.Shared;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Orchestrations.Concurrent;

/// <summary>
/// Drives the concurrent orchestration editor dialog.
/// </summary>
public sealed class ConcurrentOrchestrationEditorViewModel : ViewModelBase
{
    private readonly IConcurrentOrchestrationRepository _repository;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly ConcurrentOrchestration? _orchestration;
    private readonly int? _existingId;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _validationText = string.Empty;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new concurrent orchestration editor view-model.
    /// </summary>
    public ConcurrentOrchestrationEditorViewModel(
        ConcurrentOrchestration? orchestration,
        IReadOnlyList<AgentDefinition> availableAgents,
        IConcurrentOrchestrationRepository repository,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _nameExistsAsync = nameExistsAsync;
        _orchestration = orchestration;
        _existingId = orchestration?.Id;

        Participants = [];
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);

        WindowTitle = orchestration is null
            ? "Add concurrent orchestration"
            : $"Edit {orchestration.Name}";
        SubtitleText = orchestration is null
            ? "Choose the agents that should run the same prompt in parallel."
            : "Update the name, description, and participant list for this concurrent workflow.";

        Name = orchestration?.Name ?? string.Empty;
        Description = orchestration?.Description ?? string.Empty;

        HashSet<int> selectedAgentIds = orchestration?.Participants
            .Select(participant => participant.AgentDefinitionId)
            .ToHashSet() ?? [];

        foreach (AgentDefinition agentDefinition in availableAgents)
        {
            Participants.Add(new ConcurrentOrchestrationParticipantSelectionItemViewModel(
                agentDefinition,
                selectedAgentIds.Contains(agentDefinition.Id),
                OnEditorFieldChanged));
        }
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
    /// Gets the selectable participants.
    /// </summary>
    public ObservableCollection<ConcurrentOrchestrationParticipantSelectionItemViewModel> Participants { get; }

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

    private bool CanSave()
        => !_isSaving
            && !string.IsNullOrWhiteSpace(Name);

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        try
        {
            string normalizedName = Normalize(Name);
            if (normalizedName.Length == 0)
            {
                ValidationText = "Concurrent orchestration name is required.";
                return;
            }

            if (!OrchestrationNameRules.IsValid(normalizedName))
            {
                ValidationText = OrchestrationNameRules.ValidationMessage;
                return;
            }

            List<ConcurrentOrchestrationParticipantSelectionItemViewModel> selectedParticipants = Participants
                .Where(participant => participant.IsSelected)
                .ToList();

            if (selectedParticipants.Count == 0)
            {
                ValidationText = "Concurrent orchestration must contain at least one participant.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A concurrent orchestration named '{normalizedName}' already exists.";
                return;
            }

            bool isNew = _orchestration is null;
            ConcurrentOrchestration orchestration = _orchestration ?? new()
            {
                Name = normalizedName,
            };

            orchestration.Name = normalizedName;
            orchestration.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

            HashSet<int> selectedAgentIds = selectedParticipants
                .Select(participant => participant.AgentDefinitionId)
                .ToHashSet();

            if (isNew)
            {
                orchestration.Participants = [.. selectedAgentIds.Select(agentDefinitionId => new ConcurrentOrchestrationParticipant
                {
                    AgentDefinitionId = agentDefinitionId,
                })];
            }
            else
            {
                List<ConcurrentOrchestrationParticipant> participantsToRemove = orchestration.Participants
                    .Where(participant => !selectedAgentIds.Contains(participant.AgentDefinitionId))
                    .ToList();

                foreach (ConcurrentOrchestrationParticipant participant in participantsToRemove)
                {
                    orchestration.Participants.Remove(participant);
                }

                HashSet<int> existingAgentIds = orchestration.Participants
                    .Select(participant => participant.AgentDefinitionId)
                    .ToHashSet();

                foreach (int agentDefinitionId in selectedAgentIds.Except(existingAgentIds))
                {
                    orchestration.Participants.Add(new ConcurrentOrchestrationParticipant
                    {
                        AgentDefinitionId = agentDefinitionId,
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
            ValidationText = $"Could not save the concurrent orchestration: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        _saveCommand.RaiseCanExecuteChanged();
    }

    private static string Normalize(string text)
        => text.Trim();
}
