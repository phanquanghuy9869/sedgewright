using System;

namespace Sedgewright.Entities;

public sealed class AgentTool
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public Guid ToolDefinitionId { get; set; }
    public required ToolDefinition ToolDefinition { get; set; }
    public AgentDefinition AgentDefinition { get; set; } = null!;
}
