using System;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Represents one orchestration row in the list.
/// </summary>
public sealed class OrchestrationListItemViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a row view-model.
    /// </summary>
    public OrchestrationListItemViewModel(OrchestrationListItemSnapshot snapshot)
        : this(snapshot, false, false, () => System.Threading.Tasks.Task.CompletedTask, () => System.Threading.Tasks.Task.CompletedTask)
    {
    }

    /// <summary>
    /// Creates a row view-model.
    /// </summary>
    public OrchestrationListItemViewModel(
        OrchestrationListItemSnapshot snapshot,
        bool canEdit,
        bool canDelete,
        Func<System.Threading.Tasks.Task> editAsync,
        Func<System.Threading.Tasks.Task> deleteAsync)
    {
        Id = snapshot.Id;
        Kind = snapshot.Kind;
        Name = snapshot.Name;
        Description = snapshot.Description;
        Summary = snapshot.Summary;
        CompactSummary = $"Kind: {KindLabel} · {Summary}";
        CanEdit = canEdit;
        CanDelete = canDelete;

        EditCommand = new AsyncRelayCommand(editAsync, () => CanEdit);
        DeleteCommand = new AsyncRelayCommand(deleteAsync, () => CanDelete);
    }

    /// <summary>
    /// Gets the identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the orchestration kind label.
    /// </summary>
    public OrchestrationKind Kind { get; }

    /// <summary>
    /// Gets the orchestration kind label.
    /// </summary>
    public string KindLabel => Kind switch
    {
        OrchestrationKind.Sequential => "Sequential",
        OrchestrationKind.Concurrent => "Concurrent",
        OrchestrationKind.GroupChat => "Group chat",
        OrchestrationKind.Magentic => "Magentic",
        OrchestrationKind.Handoff => "Handoff",
        _ => Kind.ToString(),
    };

    /// <summary>
    /// Gets the orchestration name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the orchestration description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the concise orchestration summary.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the compact metadata summary shown in the list row.
    /// </summary>
    public string CompactSummary { get; }

    /// <summary>
    /// Gets whether the row can be edited.
    /// </summary>
    public bool CanEdit { get; }

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
