using System.Collections.Generic;

namespace Sedgewright.Entities;

/// <summary>
/// Represents a Magentic workflow orchestration that coordinates a manager agent and specialized participants.
/// </summary>
public sealed class MagenticOrchestration : IOrchestration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int ManagerAgentDefinitionId { get; set; }

    public AgentDefinition ManagerAgentDefinition { get; set; } = null!;

    /// <summary>
    /// Legacy persisted setting retained for database compatibility. Plan reviews are currently auto-approved.
    /// </summary>
    public bool RequirePlanSignoff { get; set; } = true;

    public int MaximumRoundCount { get; set; } = 10;

    public int MaximumStallCount { get; set; } = 3;

    public int MaximumResetCount { get; set; } = 2;

    public List<MagenticOrchestrationParticipant> Participants { get; set; } = [];

    public List<ToolDefinition> ToolDefinitions { get; set; } = [];
}
