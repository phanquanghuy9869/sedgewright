namespace Sedgewright.Entities;

/// <summary>
/// Represents a direct handoff rule between two agents in a handoff orchestration.
/// </summary>
public sealed class HandoffOrchestrationRoute
{
    public int HandoffOrchestrationId { get; set; }

    public int SourceAgentDefinitionId { get; set; }

    public int TargetAgentDefinitionId { get; set; }

    public AgentDefinition SourceAgentDefinition { get; set; } = null!;

    public AgentDefinition TargetAgentDefinition { get; set; } = null!;

    public HandoffOrchestration HandoffOrchestration { get; set; } = null!;
}
