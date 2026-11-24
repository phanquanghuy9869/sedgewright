namespace Sedgewright.Entities;

/// <summary>
/// Represents a persisted participant in a concurrent orchestration.
/// </summary>
public sealed class ConcurrentOrchestrationParticipant
{
    public int ConcurrentOrchestrationId { get; set; }

    public int AgentDefinitionId { get; set; }

    public AgentDefinition AgentDefinition { get; set; } = null!;

    public ConcurrentOrchestration ConcurrentOrchestration { get; set; } = null!;
}
