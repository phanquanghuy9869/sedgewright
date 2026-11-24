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

namespace Sedgewright.ViewModels.Tools;

/// <summary>
/// Drives the minimal workflow tool definition editor dialog.
/// </summary>
public sealed class ToolDefinitionEditorViewModel : ViewModelBase
{
    private readonly Func<string, Guid?, Task<bool>> _nameExistsAsync;
    private readonly IToolDefinitionRepository _repository;
    private readonly Guid? _existingId;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private ToolDefinition? _definition;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _toolDescription = string.Empty;
    private OrchestrationListItemSnapshot? _selectedWorkflow;
    private string _validationText = string.Empty;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    private ToolDefinitionEditorViewModel(
        Guid? definitionId,
        IReadOnlyList<OrchestrationListItemSnapshot> workflows,
        IToolDefinitionRepository repository,
        Func<string, Guid?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _nameExistsAsync = nameExistsAsync;
        _existingId = definitionId;
        WorkflowOptions = new ObservableCollection<OrchestrationListItemSnapshot>(workflows);
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);

        if (!definitionId.HasValue)
        {
            WindowTitle = "Add tool definition";
            SubtitleText = "Create a reusable tool backed by an existing workflow.";
            return;
        }

        WindowTitle = "Edit tool definition";
        SubtitleText = "Choose the workflow this tool should run.";
    }

    /// <summary>
    /// Loads the optional existing definition and creates a workflow tool definition editor.
    /// </summary>
    public static async Task<ToolDefinitionEditorViewModel?> CreateAsync(
        Guid? definitionId,
        IReadOnlyList<OrchestrationListItemSnapshot> workflows,
        IToolDefinitionRepository repository,
        Func<string, Guid?, Task<bool>> nameExistsAsync)
    {
        ToolDefinition? definition = definitionId.HasValue
            ? await repository.GetAsync(definitionId.Value)
            : null;

        if (definitionId.HasValue && definition is null)
        {
            return null;
        }

        ToolDefinitionEditorViewModel editor = new(
            definitionId,
            workflows,
            repository,
            nameExistsAsync);

        if (definition is not null)
        {
            editor.InitializeEdit(definition);
        }

        return editor;
    }

    public string WindowTitle { get; private set; }

    public string SubtitleText { get; }

    public ObservableCollection<OrchestrationListItemSnapshot> WorkflowOptions { get; }

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

    public string ToolDescription
    {
        get => _toolDescription;
        set
        {
            if (!SetProperty(ref _toolDescription, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    public ToolDefinitionType Type => ToolDefinitionType.Workflow;

    public OrchestrationListItemSnapshot? SelectedWorkflow
    {
        get => _selectedWorkflow;
        set
        {
            if (!SetProperty(ref _selectedWorkflow, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    public bool HasWorkflowOptions => WorkflowOptions.Count > 0;

    public string WorkflowOptionsText => HasWorkflowOptions
        ? "Select an existing workflow"
        : "Create an orchestration before adding a workflow tool.";

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

    public bool HasValidationText => !string.IsNullOrWhiteSpace(ValidationText);

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    private bool CanSave()
        => !_isSaving
            && !string.IsNullOrWhiteSpace(Name)
            && SelectedWorkflow is not null;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        string normalizedName = Normalize(Name);
        try
        {
            if (normalizedName.Length == 0)
            {
                ValidationText = "Tool name is required.";
                return;
            }

            if (SelectedWorkflow is null)
            {
                ValidationText = HasWorkflowOptions
                    ? "Select a workflow."
                    : "Create an orchestration before adding a workflow tool.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"A tool named '{normalizedName}' already exists.";
                return;
            }

            ToolDefinition tool = BuildDefinition(normalizedName, SelectedWorkflow);
            if (_existingId.HasValue)
            {
                _repository.Update(tool);
            }
            else
            {
                _repository.Create(tool);
            }

            await _repository.SaveChangesAsync();
            ValidationText = string.Empty;
            CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());
        }
        catch (DbUpdateException exception)
        {
            ValidationText = $"Could not save the tool definition: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private ToolDefinition
    BuildDefinition(string normalizedName, OrchestrationListItemSnapshot workflow)
    {
        ToolDefinition definition = _definition ?? new ToolDefinition
        {
            Id = _existingId ?? Guid.Empty,
            Name = normalizedName,
        };

        definition.Name = normalizedName;
        string normalizedDescription = Normalize(ToolDescription);
        definition.ToolDescription = normalizedDescription.Length == 0 ? null : normalizedDescription;
        definition.Type = ToolDefinitionType.Workflow;

        if (_definition is not null && !IsSameWorkflow(definition, workflow))
        {
            ClearCurrentWorkflowReference(definition);
        }

        switch (workflow.Kind)
        {
            case OrchestrationKind.Sequential:
                definition.SequentialOrchestrationId = workflow.Id;
                break;
            case OrchestrationKind.Concurrent:
                definition.ConcurrentOrchestrationId = workflow.Id;
                break;
            case OrchestrationKind.GroupChat:
                definition.GroupChatOrchestrationId = workflow.Id;
                break;
            case OrchestrationKind.Magentic:
                definition.MagenticOrchestrationId = workflow.Id;
                break;
            case OrchestrationKind.Handoff:
                definition.HandoffOrchestrationId = workflow.Id;
                break;
            default:
                throw new InvalidOperationException("The selected orchestration kind is not supported.");
        }

        return definition;
    }

    private static bool IsSameWorkflow(
        ToolDefinition definition,
        OrchestrationListItemSnapshot workflow)
        => workflow.Kind switch
        {
            OrchestrationKind.Sequential => definition.SequentialOrchestrationId == workflow.Id,
            OrchestrationKind.Concurrent => definition.ConcurrentOrchestrationId == workflow.Id,
            OrchestrationKind.GroupChat => definition.GroupChatOrchestrationId == workflow.Id,
            OrchestrationKind.Magentic => definition.MagenticOrchestrationId == workflow.Id,
            OrchestrationKind.Handoff => definition.HandoffOrchestrationId == workflow.Id,
            _ => false,
        };

    private static void ClearCurrentWorkflowReference(ToolDefinition definition)
    {
        if (definition.SequentialOrchestrationId.HasValue)
        {
            definition.SequentialOrchestrationId = null;
            definition.SequentialOrchestration = null;
            return;
        }

        if (definition.ConcurrentOrchestrationId.HasValue)
        {
            definition.ConcurrentOrchestrationId = null;
            definition.ConcurrentOrchestration = null;
            return;
        }

        if (definition.GroupChatOrchestrationId.HasValue)
        {
            definition.GroupChatOrchestrationId = null;
            definition.GroupChatOrchestration = null;
            return;
        }

        if (definition.MagenticOrchestrationId.HasValue)
        {
            definition.MagenticOrchestrationId = null;
            definition.MagenticOrchestration = null;
            return;
        }

        if (definition.HandoffOrchestrationId.HasValue)
        {
            definition.HandoffOrchestrationId = null;
            definition.HandoffOrchestration = null;
        }
    }

    private void InitializeEdit(ToolDefinition definition)
    {
        _definition = definition;
        WindowTitle = $"Edit {definition.Name}";
        Name = definition.Name;
        ToolDescription = definition.ToolDescription ?? string.Empty;
        _selectedWorkflow = FindWorkflow(definition, WorkflowOptions);
    }

    private static OrchestrationListItemSnapshot? FindWorkflow(
        ToolDefinition definition,
        IEnumerable<OrchestrationListItemSnapshot> workflows)
    {
        if (definition.SequentialOrchestrationId.HasValue)
        {
            return workflows.FirstOrDefault(workflow => workflow.Kind == OrchestrationKind.Sequential
                && workflow.Id == definition.SequentialOrchestrationId.Value);
        }

        if (definition.ConcurrentOrchestrationId.HasValue)
        {
            return workflows.FirstOrDefault(workflow => workflow.Kind == OrchestrationKind.Concurrent
                && workflow.Id == definition.ConcurrentOrchestrationId.Value);
        }

        if (definition.GroupChatOrchestrationId.HasValue)
        {
            return workflows.FirstOrDefault(workflow => workflow.Kind == OrchestrationKind.GroupChat
                && workflow.Id == definition.GroupChatOrchestrationId.Value);
        }

        if (definition.MagenticOrchestrationId.HasValue)
        {
            return workflows.FirstOrDefault(workflow => workflow.Kind == OrchestrationKind.Magentic
                && workflow.Id == definition.MagenticOrchestrationId.Value);
        }

        if (definition.HandoffOrchestrationId.HasValue)
        {
            return workflows.FirstOrDefault(workflow => workflow.Kind == OrchestrationKind.Handoff
                && workflow.Id == definition.HandoffOrchestrationId.Value);
        }

        return null;
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        _saveCommand.RaiseCanExecuteChanged();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
