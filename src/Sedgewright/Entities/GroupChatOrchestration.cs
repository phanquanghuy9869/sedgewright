using System.Collections.Generic;

namespace Sedgewright.Entities;

/// <summary>
/// Represents a group chat workflow orchestration that coordinates multiple agents through a round-robin manager.
/// </summary>
public sealed class GroupChatOrchestration : IOrchestration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int MaximumIterationCount { get; set; } = 5;

    public List<GroupChatOrchestrationParticipant> Participants { get; set; } = [];

    public List<ToolDefinition> ToolDefinitions { get; set; } = [];
}
