using System.Collections.Generic;

namespace Sedgewright.Entities;

/// <summary>
/// Represents a concurrent workflow orchestration whose participants execute the same prompt in parallel.
/// </summary>
public sealed class ConcurrentOrchestration : IOrchestration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public List<ConcurrentOrchestrationParticipant> Participants { get; set; } = [];

    public List<ToolDefinition> ToolDefinitions { get; set; } = [];
}
