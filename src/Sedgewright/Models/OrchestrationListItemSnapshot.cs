namespace Sedgewright.Models;

/// <summary>
/// Lightweight orchestration data for list rendering.
/// </summary>
public sealed record OrchestrationListItemSnapshot(
    int Id,
    OrchestrationKind Kind,
    string Name,
    string Description,
    string Summary)
{
    public string KindLabel => Kind switch
    {
        OrchestrationKind.Sequential => "Sequential",
        OrchestrationKind.Concurrent => "Concurrent",
        OrchestrationKind.GroupChat => "Group chat",
        OrchestrationKind.Magentic => "Magentic",
        OrchestrationKind.Handoff => "Handoff",
        _ => Kind.ToString(),
    };

    public string DisplayLabel => $"{KindLabel} · {Name}";
}
