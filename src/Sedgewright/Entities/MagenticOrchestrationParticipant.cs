namespace Sedgewright.Entities;

/// <summary>
/// Represents an ordered participant in a persisted Magentic orchestration.
/// </summary>
public sealed class MagenticOrchestrationParticipant
{
    public int MagenticOrchestrationId { get; set; }

    public int AgentDefinitionId { get; set; }

    public int StepOrder { get; set; }

    public AgentDefinition AgentDefinition { get; set; } = null!;

    public MagenticOrchestration MagenticOrchestration { get; set; } = null!;
}
