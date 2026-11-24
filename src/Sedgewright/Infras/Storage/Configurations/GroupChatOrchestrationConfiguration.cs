using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted group chat orchestration aggregate.
/// </summary>
public sealed class GroupChatOrchestrationConfiguration : IEntityTypeConfiguration<GroupChatOrchestration>
{
    public void Configure(EntityTypeBuilder<GroupChatOrchestration> entity)
    {
        entity.ToTable("GroupChatOrchestrations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Description).HasMaxLength(1024);
        entity.Property(x => x.MaximumIterationCount).IsRequired().HasDefaultValue(5);

        entity.HasMany(x => x.Participants)
            .WithOne(x => x.GroupChatOrchestration)
            .HasForeignKey(x => x.GroupChatOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
    }
}
