using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the join table between agents and tools.
/// </summary>
public sealed class AgentToolConfiguration : IEntityTypeConfiguration<AgentTool>
{
    public void Configure(EntityTypeBuilder<AgentTool> entity)
    {
        entity.ToTable("AgentTools");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();

        entity.HasOne(x => x.AgentDefinition)
            .WithMany(x => x.Tools)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.ToolDefinition)
            .WithMany()
            .HasForeignKey(x => x.ToolDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentId);
        entity.HasIndex(x => new { x.AgentId, x.ToolDefinitionId }).IsUnique();
    }
}
