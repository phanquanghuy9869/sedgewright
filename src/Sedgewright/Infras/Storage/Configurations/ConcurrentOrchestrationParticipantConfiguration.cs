using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the join table between concurrent orchestrations and agent definitions.
/// </summary>
public sealed class ConcurrentOrchestrationParticipantConfiguration : IEntityTypeConfiguration<ConcurrentOrchestrationParticipant>
{
    public void Configure(EntityTypeBuilder<ConcurrentOrchestrationParticipant> entity)
    {
        entity.ToTable("ConcurrentOrchestrationParticipants");
        entity.HasKey(x => new { x.ConcurrentOrchestrationId, x.AgentDefinitionId });

        entity.HasOne(x => x.AgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentDefinitionId);
    }
}
