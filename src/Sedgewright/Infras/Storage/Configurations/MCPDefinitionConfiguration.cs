using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the MCP server definition mapping.
/// </summary>
public sealed class MCPDefinitionConfiguration : IEntityTypeConfiguration<McpDefinition>
{
    public void Configure(EntityTypeBuilder<McpDefinition> entity)
    {
        entity.ToTable("MCPDefinitions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired();
        entity.Property(x => x.Description).IsRequired();
        entity.Property(x => x.ConfigJson).IsRequired();

        entity.HasMany<AgentMCPAttachment>()
            .WithOne(x => x.McpDefinition)
            .HasForeignKey(x => x.McpId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
    }
}
