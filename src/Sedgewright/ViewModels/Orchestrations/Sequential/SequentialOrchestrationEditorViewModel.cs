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

namespace Sedgewright.ViewModels.Orchestrations.Sequential;

/// <summary>
/// Drives the sequential orchestration editor dialog.
/// </summary>
public sealed class SequentialOrchestrationEditorViewModel : ViewModelBase
{
    private readonly ISequentialOrchestrationRepository _repository;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _addStepCommand;
    private readonly SequentialOrchestration? _orchestration;
    private readonly int? _existingId;
    private readonly IReadOnlyList<AgentDefinition> _availableAgents;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _chainOnlyAgentResponses;
    private string _validationText = string.Empty;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new sequential editor view-model.
    /// </summary>
    public SequentialOrchestrationEditorViewModel(
        SequentialOrchestration? orchestration,
        IReadOnlyList<AgentDefinition> availableAgents,
        ISequentialOrchestrationRepository repository,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _nameExistsAsync = nameExistsAsync;
        _orchestration = orchestration;
        _existingId = orchestration?.Id;
        _availableAgents = availableAgents;

        Steps = [];
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);
        _addStepCommand = new RelayCommand(AddStep, CanAddStep);

        if (orchestration is null)
        {
            WindowTitle = "Add sequential orchestration";
            SubtitleText = "Arrange agents in the order they should execute.";

            if (availableAgents.Count > 0)
            {
                Steps.Add(CreateStepViewModel(
                    selectedAgent: availableAgents[0]));
            }

            ReindexSteps();
            return;
        }

        WindowTitle = $"Edit {orchestration.Name}";
        SubtitleText = "Update the name and adjust the ordered step sequence.";
        Name = orchestration.Name;
        Description = orchestration.Description ?? string.Empty;
        ChainOnlyAgentResponses = orchestration.ChainOnlyAgentResponses;

        foreach (SequentialOrchestrationAgent step in orchestration.Steps.OrderBy(step => step.StepOrder))
        {
            Steps.Add(CreateStepViewModel(
                selectedAgent: step.AgentDefinition));
        }

        ReindexSteps();
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
    /// Gets the ordered steps.
    /// </summary>
    public ObservableCollection<SequentialOrchestrationStepViewModel> Steps { get; }

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
    /// Gets or sets whether only agent responses are passed to the next step.
    /// </summary>
    public bool ChainOnlyAgentResponses
    {
        get => _chainOnlyAgentResponses;
        set
        {
            if (!SetProperty(ref _chainOnlyAgentResponses, value))
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
    /// Gets the add-step command.
    /// </summary>
    public ICommand AddStepCommand => _addStepCommand;

    private bool CanSave()
        => !_isSaving && !string.IsNullOrWhiteSpace(Name);

    private bool CanAddStep()
        => _availableAgents.Count > 0;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        try
        {
            string normalizedName = Normalize(Name);
            if (normalizedName.Length == 0)
            {
                ValidationText = "Sequential orchestration name is required.";
                return;
            }

            if (!OrchestrationNameRules.IsValid(normalizedName))
            {
                ValidationText = OrchestrationNameRules.ValidationMessage;
                return;
            }

            if (Steps.Count == 0)
            {
                ValidationText = "Sequential orchestration must contain at least one step.";
                return;
            }

            if (Steps.Any(step => step.SelectedAgent is null))
            {
                ValidationText = "Each sequential step must select an agent.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A sequential orchestration named '{normalizedName}' already exists.";
                return;
            }

            bool isNew = _orchestration is null;
            SequentialOrchestration orchestration = _orchestration ?? new()
            {
                Name = normalizedName,
            };

            orchestration.Name = normalizedName;
            orchestration.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            orchestration.ChainOnlyAgentResponses = ChainOnlyAgentResponses;

            if (isNew)
            {
                orchestration.Steps = [.. Steps.Select((step, index) => new SequentialOrchestrationAgent
                {
                    AgentDefinitionId = step.SelectedAgent!.Id,
                    StepOrder = index + 1,
                })];
            }
            else
            {
                orchestration.Steps.Clear();
                for (int index = 0; index < Steps.Count; index++)
                {
                    orchestration.Steps.Add(new SequentialOrchestrationAgent
                    {
                        SequentialOrchestrationId = orchestration.Id,
                        AgentDefinitionId = Steps[index].SelectedAgent!.Id,
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
            ValidationText = $"Could not save the sequential orchestration: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void AddStep()
    {
        if (_availableAgents.Count == 0)
        {
            return;
        }

        Steps.Add(CreateStepViewModel(
            selectedAgent: _availableAgents[0]));
        ReindexSteps();
        OnEditorFieldChanged();
    }

    private SequentialOrchestrationStepViewModel CreateStepViewModel(AgentDefinition? selectedAgent)
        => new(
            _availableAgents,
            selectedAgent,
            MoveStepUp,
            MoveStepDown,
            RemoveStep,
            OnEditorFieldChanged);

    private void MoveStepUp(SequentialOrchestrationStepViewModel step)
    {
        int index = Steps.IndexOf(step);
        if (index <= 0)
        {
            return;
        }

        Steps.Move(index, index - 1);
        ReindexSteps();
        OnEditorFieldChanged();
    }

    private void MoveStepDown(SequentialOrchestrationStepViewModel step)
    {
        int index = Steps.IndexOf(step);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        Steps.Move(index, index + 1);
        ReindexSteps();
        OnEditorFieldChanged();
    }

    private void RemoveStep(SequentialOrchestrationStepViewModel step)
    {
        Steps.Remove(step);
        ReindexSteps();
        OnEditorFieldChanged();
    }

    private void ReindexSteps()
    {
        for (int index = 0; index < Steps.Count; index++)
        {
            Steps[index].UpdatePosition(index + 1, Steps.Count);
        }
    }

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        _saveCommand.RaiseCanExecuteChanged();
        _addStepCommand.RaiseCanExecuteChanged();
    }

    private static string Normalize(string text)
        => text.Trim();
}
