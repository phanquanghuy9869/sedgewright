using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted Magentic orchestration aggregate.
/// </summary>
public sealed class MagenticOrchestrationConfiguration : IEntityTypeConfiguration<MagenticOrchestration>
{
    public void Configure(EntityTypeBuilder<MagenticOrchestration> entity)
    {
        entity.ToTable("MagenticOrchestrations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Description).HasMaxLength(1024);
        entity.Property(x => x.ManagerAgentDefinitionId).IsRequired();
        entity.Property(x => x.RequirePlanSignoff).IsRequired().HasDefaultValue(true);
        entity.Property(x => x.MaximumRoundCount).IsRequired().HasDefaultValue(10);
        entity.Property(x => x.MaximumStallCount).IsRequired().HasDefaultValue(3);
        entity.Property(x => x.MaximumResetCount).IsRequired().HasDefaultValue(2);

        entity.HasOne(x => x.ManagerAgentDefinition)
            .WithMany()
            .HasForeignKey(x => x.ManagerAgentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(x => x.Participants)
            .WithOne(x => x.MagenticOrchestration)
            .HasForeignKey(x => x.MagenticOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasIndex(x => x.ManagerAgentDefinitionId);
    }
}
