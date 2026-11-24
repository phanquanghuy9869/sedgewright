using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the join table between group chat orchestrations and agent definitions.
/// </summary>
public sealed class GroupChatOrchestrationParticipantConfiguration : IEntityTypeConfiguration<GroupChatOrchestrationParticipant>
{
    public void Configure(EntityTypeBuilder<GroupChatOrchestrationParticipant> entity)
    {
        entity.ToTable("GroupChatOrchestrationParticipants");
        entity.HasKey(x => new { x.GroupChatOrchestrationId, x.AgentDefinitionId });

        entity.HasOne(x => x.AgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentDefinitionId);
        entity.HasIndex(x => new { x.GroupChatOrchestrationId, x.StepOrder }).IsUnique();
    }
}
