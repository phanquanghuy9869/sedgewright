using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the handoff routing table between source and target agents.
/// </summary>
public sealed class HandoffOrchestrationRouteConfiguration : IEntityTypeConfiguration<HandoffOrchestrationRoute>
{
    public void Configure(EntityTypeBuilder<HandoffOrchestrationRoute> entity)
    {
        entity.ToTable("HandoffOrchestrationRoutes");
        entity.HasKey(x => new { x.HandoffOrchestrationId, x.SourceAgentDefinitionId, x.TargetAgentDefinitionId });

        entity.HasOne(x => x.SourceAgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.SourceAgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.TargetAgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.TargetAgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.SourceAgentDefinitionId);
        entity.HasIndex(x => x.TargetAgentDefinitionId);
    }
}
