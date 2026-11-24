using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the join table between Magentic orchestrations and agent definitions.
/// </summary>
public sealed class MagenticOrchestrationParticipantConfiguration : IEntityTypeConfiguration<MagenticOrchestrationParticipant>
{
    public void Configure(EntityTypeBuilder<MagenticOrchestrationParticipant> entity)
    {
        entity.ToTable("MagenticOrchestrationParticipants");
        entity.HasKey(x => new { x.MagenticOrchestrationId, x.AgentDefinitionId });

        entity.HasOne(x => x.AgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentDefinitionId);
        entity.HasIndex(x => new { x.MagenticOrchestrationId, x.StepOrder }).IsUnique();
    }
}
