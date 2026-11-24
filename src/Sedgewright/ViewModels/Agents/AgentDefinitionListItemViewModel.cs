using System;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Represents one agent row in the list.
/// </summary>
public sealed class AgentDefinitionListItemViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a row view-model.
    /// </summary>
    public AgentDefinitionListItemViewModel(
        AgentDefinitionSnapshot snapshot,
        bool canDelete,
        Func<System.Threading.Tasks.Task> editAsync,
        Func<System.Threading.Tasks.Task> deleteAsync)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        Description = snapshot.Description;
        IsReadOnly = snapshot.IsReadOnly;
        Model = snapshot.Model;
        ProviderDisplayName = snapshot.ProviderDisplayName;
        McpAttachmentSummary = snapshot.McpAttachmentCount == 0
            ? "No MCP attached"
            : snapshot.McpAttachmentCount == 1
                ? "1 MCP attached"
                : $"{snapshot.McpAttachmentCount} MCPs attached";
        WorkflowUsageSummary = canDelete
            ? "Unused"
            : "In use";
        CompactSummary = $"Provider: {ProviderDisplayName} · Model: {Model} · MCP: {McpAttachmentSummary} · Workflow: {WorkflowUsageSummary}";
        CanDelete = canDelete;

        EditCommand = new AsyncRelayCommand(editAsync);
        DeleteCommand = new AsyncRelayCommand(deleteAsync, () => CanDelete);
    }

    /// <summary>
    /// Gets the identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the agent description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets whether the agent is read only.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets the agent model.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    public string ProviderDisplayName { get; }

    /// <summary>
    /// Gets the MCP attachment summary.
    /// </summary>
    public string McpAttachmentSummary { get; }

    /// <summary>
    /// Gets whether this agent is referenced by a workflow.
    /// </summary>
    public string WorkflowUsageSummary { get; }

    /// <summary>
    /// Gets the compact metadata summary shown in the list row.
    /// </summary>
    public string CompactSummary { get; }

    /// <summary>
    /// Gets whether the row can be deleted.
    /// </summary>
    public bool CanDelete { get; }

    /// <summary>
    /// Gets the edit command.
    /// </summary>
    public ICommand EditCommand { get; }

    /// <summary>
    /// Gets the delete command.
    /// </summary>
    public ICommand DeleteCommand { get; }
}
