using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the join table between handoff orchestrations and agent definitions.
/// </summary>
public sealed class HandoffOrchestrationParticipantConfiguration : IEntityTypeConfiguration<HandoffOrchestrationParticipant>
{
    public void Configure(EntityTypeBuilder<HandoffOrchestrationParticipant> entity)
    {
        entity.ToTable("HandoffOrchestrationParticipants");
        entity.HasKey(x => new { x.HandoffOrchestrationId, x.AgentDefinitionId });

        entity.HasOne(x => x.AgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentDefinitionId);
    }
}
