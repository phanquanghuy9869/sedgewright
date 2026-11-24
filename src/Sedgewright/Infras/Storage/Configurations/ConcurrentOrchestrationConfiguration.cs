using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted concurrent orchestration aggregate.
/// </summary>
public sealed class ConcurrentOrchestrationConfiguration : IEntityTypeConfiguration<ConcurrentOrchestration>
{
    public void Configure(EntityTypeBuilder<ConcurrentOrchestration> entity)
    {
        entity.ToTable("ConcurrentOrchestrations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Description).HasMaxLength(1024);

        entity.HasMany(x => x.Participants)
            .WithOne(x => x.ConcurrentOrchestration)
            .HasForeignKey(x => x.ConcurrentOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
    }
}
