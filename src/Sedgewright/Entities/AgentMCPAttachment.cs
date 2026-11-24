namespace Sedgewright.Entities;

public sealed class AgentMCPAttachment
{
    public AgentMCPAttachment()
    {
    }

    public int AgentId { get; set; }
    public int McpId { get; set; }
    public AgentDefinition AgentDefinition { get; set; } = null!;
    public McpDefinition McpDefinition { get; set; } = null!;
}
