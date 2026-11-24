using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the ordered step table for sequential orchestrations.
/// </summary>
public sealed class SequentialOrchestrationAgentConfiguration : IEntityTypeConfiguration<SequentialOrchestrationAgent>
{
    public void Configure(EntityTypeBuilder<SequentialOrchestrationAgent> entity)
    {
        entity.ToTable("SequentialOrchestrationAgents");
        entity.HasKey(x => new { x.SequentialOrchestrationId, x.AgentDefinitionId });

        entity.HasOne(x => x.AgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentDefinitionId);
        entity.HasIndex(x => new { x.SequentialOrchestrationId, x.StepOrder }).IsUnique();
    }
}
