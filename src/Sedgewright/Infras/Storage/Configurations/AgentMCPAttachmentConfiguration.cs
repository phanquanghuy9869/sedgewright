using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the agent-to-MCP attachment join table.
/// </summary>
public sealed class AgentMCPAttachmentConfiguration : IEntityTypeConfiguration<AgentMCPAttachment>
{
    public void Configure(EntityTypeBuilder<AgentMCPAttachment> entity)
    {
        entity.ToTable("AgentMCPAttachments");
        entity.HasKey(x => new { x.AgentId, x.McpId });
    }
}
