namespace Sedgewright.Models;

/// <summary>
/// Identifies the persisted orchestration shape.
/// </summary>
public enum OrchestrationKind
{
    Sequential = 0,
    Concurrent = 1,
    GroupChat = 2,
    Magentic = 3,
    Handoff = 4,
}
