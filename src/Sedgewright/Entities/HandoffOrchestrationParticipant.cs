namespace Sedgewright.Entities;

/// <summary>
/// Represents a persisted participant in a handoff orchestration.
/// </summary>
public sealed class HandoffOrchestrationParticipant
{
    public int HandoffOrchestrationId { get; set; }

    public int AgentDefinitionId { get; set; }

    public AgentDefinition AgentDefinition { get; set; } = null!;

    public HandoffOrchestration HandoffOrchestration { get; set; } = null!;
}
