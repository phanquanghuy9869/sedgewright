using System.Collections.Generic;

namespace Sedgewright.Entities;

public sealed class SequentialOrchestration : IOrchestration
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether each step receives only the preceding agent responses.
    /// </summary>
    public bool ChainOnlyAgentResponses { get; set; } = false;

    public List<SequentialOrchestrationAgent> Steps { get; set; } = [];

    public List<ToolDefinition> ToolDefinitions { get; set; } = [];
}
