namespace Sedgewright.Entities;

/// <summary>
/// Represents an ordered participant in a persisted group chat orchestration.
/// </summary>
public sealed class GroupChatOrchestrationParticipant
{
    public int GroupChatOrchestrationId { get; set; }

    public int AgentDefinitionId { get; set; }

    public int StepOrder { get; set; }

    public AgentDefinition AgentDefinition { get; set; } = null!;

    public GroupChatOrchestration GroupChatOrchestration { get; set; } = null!;
}
