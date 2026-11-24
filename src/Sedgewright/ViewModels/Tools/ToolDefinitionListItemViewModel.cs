using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Tools;

/// <summary>
/// Represents one workflow tool definition in the list.
/// </summary>
public sealed class ToolDefinitionListItemViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a tool definition list row.
    /// </summary>
    public ToolDefinitionListItemViewModel(
        ToolDefinition definition,
        OrchestrationListItemSnapshot? workflow,
        Func<Task> editAsync,
        Func<Task> deleteAsync)
    {
        Id = definition.Id;
        Name = definition.Name;
        WorkflowLabel = workflow?.DisplayLabel ?? "Workflow target unavailable";
        CompactSummary = workflow is null
            ? WorkflowLabel
            : $"{workflow.DisplayLabel} · {workflow.Summary}";
        CanDelete = true;
        EditCommand = new AsyncRelayCommand(editAsync);
        DeleteCommand = new AsyncRelayCommand(deleteAsync, () => CanDelete);
    }

    public Guid Id { get; }

    public string Name { get; }

    public string WorkflowLabel { get; }

    public string CompactSummary { get; }

    public bool CanDelete { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }
}
