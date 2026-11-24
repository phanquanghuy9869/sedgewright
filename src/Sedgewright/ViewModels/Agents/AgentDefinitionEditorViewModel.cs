using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Coordinates the agent editor dialog lifecycle, validation, and persistence.
/// </summary>
public sealed class AgentDefinitionEditorViewModel : ViewModelBase
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly IAgentSkillDiscoveryService _skillDiscoveryService;
    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly AgentDefinitionDraftMapper _draftMapper = new();
    private readonly AgentDefinition _definition;
    private readonly int? _existingId;
    private bool _isSaving;
    private string _validationText = string.Empty;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new editor view-model.
    /// </summary>
    public AgentDefinitionEditorViewModel(
        AgentDefinition? definition,
        IAgentDefinitionRepository repository,
        IAgentSkillDiscoveryService skillDiscoveryService,
        ISharedState sharedState,
        IReadOnlyList<McpDefinitionSnapshot> mcpDefinitions,
        IReadOnlyList<ToolDefinition> toolDefinitions,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _repository = repository;
        _skillDiscoveryService = skillDiscoveryService;
        _nameExistsAsync = nameExistsAsync;
        _definition = definition ?? new AgentDefinition
        {
            Name = string.Empty,
            Description = string.Empty,
            Model = string.Empty,
        };
        _existingId = definition?.Id;

        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(
            definition,
            sharedState.ApplicationSettings?.DefaultWorkingDirectory);
        Information = new AgentDefinitionInformationViewModel(draft, OnDraftChanged);
        ProviderModel = new AgentDefinitionProviderModelViewModel(draft, sharedState.Providers, OnDraftChanged);
        McpAttachments = new AgentDefinitionMcpAttachmentsViewModel(draft.SelectedMcpIds, mcpDefinitions, OnDraftChanged);
        Tools = new AgentDefinitionToolsViewModel(draft.SelectedToolDefinitionIds, toolDefinitions, OnDraftChanged);
        Skills = new AgentDefinitionSkillsViewModel(
            draft.SkillAttachments,
            skillDiscoveryService.GetCommonSkillSources(),
            sharedState.ApplicationSettings?.DefaultWorkingDirectory,
            OnDraftChanged,
            SetValidationText);
        Shell = new AgentDefinitionShellViewModel(draft, OnDraftChanged, SetValidationText);
        HarnessOptions = new AgentDefinitionHarnessOptionsViewModel(draft, CanToggleHarnessAgentType, OnDraftChanged);

        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);

        if (definition is null)
        {
            WindowTitle = "Add agent definition";
            SubtitleText = "Keep the core agent identity compact. Enable Harness to reveal the extra runtime options.";
            return;
        }

        WindowTitle = $"Edit {definition.Name}";
        SubtitleText = "Update the core agent profile and attached MCPs and tools. The agent type is fixed after creation.";
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle { get; }

    /// <summary>
    /// Gets the subtitle text.
    /// </summary>
    public string SubtitleText { get; }

    public AgentDefinitionInformationViewModel Information { get; }

    public AgentDefinitionProviderModelViewModel ProviderModel { get; }

    public AgentDefinitionMcpAttachmentsViewModel McpAttachments { get; }

    public AgentDefinitionToolsViewModel Tools { get; }

    public AgentDefinitionSkillsViewModel Skills { get; }

    public AgentDefinitionShellViewModel Shell { get; }

    public AgentDefinitionHarnessOptionsViewModel HarnessOptions { get; }

    internal IAgentSkillDiscoveryService SkillDiscoveryService => _skillDiscoveryService;

    public bool CanToggleHarnessAgentType => _existingId is null;

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
            && !string.IsNullOrWhiteSpace(Information.Name)
            && !string.IsNullOrWhiteSpace(Information.Description)
            && !string.IsNullOrWhiteSpace(ProviderModel.Model)
            && ProviderModel.SelectedProvider is not null;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        try
        {
            AgentDefinitionEditorDraft draft = CreateDraft();
            if (draft.IsHarnessAgent
                && draft.EnableFileAccess
                && string.IsNullOrWhiteSpace(draft.ShellWorkingDirectory))
            {
                ValidationText = "A working directory is required when file access is enabled.";
                return;
            }

            AgentDefinition candidateDefinition = _draftMapper.CreateCandidateDefinition(draft, _definition);

            UnitResult<IApplicationError> validation = candidateDefinition.IsValidDefinition();
            if (validation.IsFailure)
            {
                ValidationText = string.Join(Environment.NewLine, validation.Error.Messages);
                return;
            }

            string normalizedName = candidateDefinition.Name;
            if (normalizedName.Length == 0)
            {
                ValidationText = "Agent name is required.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"An agent named '{normalizedName}' already exists.";
                return;
            }

            ValidationText = string.Empty;

            AgentDefinition definitionToSave = candidateDefinition;
            if (candidateDefinition.GetType() == _definition.GetType())
            {
                _draftMapper.ApplyDraft(_definition, draft);
                definitionToSave = _definition;
            }

            if (definitionToSave.Id == 0)
            {
                _repository.Create(definitionToSave);
            }
            else
            {
                _repository.Update(definitionToSave);
            }

            await _repository.SaveChangesAsync();
            CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());
        }
        catch (DbUpdateException exception)
        {
            ValidationText = $"Could not save the agent definition: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private AgentDefinitionEditorDraft CreateDraft()
        => new(
            _definition.Id,
            Information.Name,
            Information.Description,
            Information.SystemPrompt,
            Information.UserPrompt,
            ProviderModel.Model,
            Information.IsReadOnly,
            ProviderModel.SelectedProvider,
            Shell.ShellExecutor,
            Shell.ShellWorkingDirectory,
            Shell.IsShellActive,
            HarnessOptions.IsHarnessAgent,
            HarnessOptions.EnableFileAccess,
            HarnessOptions.DisableTodoProvider,
            HarnessOptions.DisableAgentModeProvider,
            HarnessOptions.DisableFileMemory,
            HarnessOptions.DisableWebSearch,
            HarnessOptions.DisableToolAutoApproval,
            HarnessOptions.DisableAgentSkillsProvider,
            HarnessOptions.DisableOpenTelemetry,
            HarnessOptions.MaxContextWindowTokens,
            HarnessOptions.MaxOutputTokens,
            HarnessOptions.EnableCodeExecution,
            McpAttachments.SelectedMcpIds,
            Skills.SkillAttachments,
            Tools.SelectedToolDefinitionIds);

    private void OnDraftChanged()
    {
        ValidationText = string.Empty;
        ProviderModel.ClearModelStatus();
        _saveCommand.RaiseCanExecuteChanged();
    }

    private void SetValidationText(string status)
        => ValidationText = status;
}
