using System.Collections.Generic;

namespace Sedgewright.Entities;

/// <summary>
/// Represents a handoff workflow orchestration that routes the conversation between specialized agents.
/// </summary>
public sealed class HandoffOrchestration : IOrchestration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int StartAgentDefinitionId { get; set; }

    public AgentDefinition StartAgentDefinition { get; set; } = null!;

    public bool IsAutonomousModeEnabled { get; set; } = true;

    public List<HandoffOrchestrationParticipant> Participants { get; set; } = [];

    public List<HandoffOrchestrationRoute> Routes { get; set; } = [];

    public List<ToolDefinition> ToolDefinitions { get; set; } = [];
}
