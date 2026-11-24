using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Tools;

/// <summary>
/// Manages the workflow tool definition list page.
/// </summary>
public sealed class ToolDefinitionsViewModel : ViewModelBase
{
    private readonly IToolDefinitionRepository _repository;
    private readonly IOrchestrationQueryRepository _orchestrationQueryRepository;
    private readonly IToolDefinitionDialogService _dialogService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _addCommand;
    private string _statusText = string.Empty;

    /// <summary>
    /// Creates the workflow tool definition list view-model.
    /// </summary>
    public ToolDefinitionsViewModel(
        IToolDefinitionRepository repository,
        IOrchestrationQueryRepository orchestrationQueryRepository,
        IToolDefinitionDialogService dialogService)
    {
        _repository = repository;
        _orchestrationQueryRepository = orchestrationQueryRepository;
        _dialogService = dialogService;
        Items = [];
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _addCommand = new AsyncRelayCommand(AddAsync);

        _refreshCommand.Execute(null);
    }

    public ObservableCollection<ToolDefinitionListItemViewModel> Items { get; }

    public bool HasItems => Items.Count > 0;

    public bool HasNoItems => !HasItems;

    public string TitleText => "Tool definitions";

    public string SubtitleText => "Manage reusable tools backed by existing agent workflows.";

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand AddCommand => _addCommand;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public string EmptyStateTitle => "No workflow tools yet.";

    public string EmptyStateText => "Add a tool definition and connect it to an existing orchestration.";

    private async Task RefreshAsync()
    {
        try
        {
            List<ToolDefinition> definitions = await _repository.GetAllAsync();
            List<OrchestrationListItemSnapshot> workflows = await _orchestrationQueryRepository.GetAllAsync();
            Items.Clear();

            foreach (ToolDefinition definition in definitions.Where(definition => definition.Type == ToolDefinitionType.Workflow))
            {
                Items.Add(CreateRow(definition, workflows));
            }

            StatusText = string.Empty;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load tool definitions: {exception.Message}";
        }
    }

    private Task AddAsync()
        => EditAsync(null);

    private async Task EditAsync(Guid? id)
    {
        List<OrchestrationListItemSnapshot> workflows = await _orchestrationQueryRepository.GetAllAsync();
        ToolDefinitionEditorViewModel? editor = await ToolDefinitionEditorViewModel.CreateAsync(
            id,
            workflows,
            _repository,
            (name, excludeId) => _repository.NameExistsAsync(name, excludeId));

        if (editor is null)
        {
            StatusText = "The selected tool definition no longer exists.";
            return;
        }

        _ = await _dialogService.ShowAsync(editor);
        await RefreshAsync();
    }

    private async Task DeleteAsync(Guid id)
    {
        ToolDefinition? definition = await _repository.GetAsync(id);
        if (definition is null)
        {
            StatusText = "The selected tool definition no longer exists.";
            return;
        }

        List<string> assignedAgentNames = await _repository.GetAssignedAgentNamesAsync(id);
        if (assignedAgentNames.Count > 0)
        {
            StatusText = $"Cannot delete \"{definition.Name}\" because it is assigned to these agents: "
                + $"{string.Join(", ", assignedAgentNames)}. Remove it from those agents first.";
            return;
        }

        try
        {
            _repository.Delete(definition);
            await _repository.SaveChangesAsync();
            await RefreshAsync();
        }
        catch (DbUpdateException exception)
        {
            StatusText = $"Could not delete the tool definition: {exception.Message}";
        }
    }

    private ToolDefinitionListItemViewModel CreateRow(
        ToolDefinition definition,
        IReadOnlyList<OrchestrationListItemSnapshot> workflows)
        => new(
            definition,
            FindWorkflow(definition, workflows),
            () => EditAsync(definition.Id),
            () => DeleteAsync(definition.Id));

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
}
