using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted handoff orchestration aggregate.
/// </summary>
public sealed class HandoffOrchestrationConfiguration : IEntityTypeConfiguration<HandoffOrchestration>
{
    public void Configure(EntityTypeBuilder<HandoffOrchestration> entity)
    {
        entity.ToTable("HandoffOrchestrations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Description).HasMaxLength(1024);
        entity.Property(x => x.StartAgentDefinitionId).IsRequired();
        entity.Property(x => x.IsAutonomousModeEnabled).IsRequired().HasDefaultValue(true);

        entity.HasOne(x => x.StartAgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.StartAgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(x => x.Participants)
            .WithOne(x => x.HandoffOrchestration)
            .HasForeignKey(x => x.HandoffOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Routes)
            .WithOne(x => x.HandoffOrchestration)
            .HasForeignKey(x => x.HandoffOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasIndex(x => x.StartAgentDefinitionId);
    }
}
