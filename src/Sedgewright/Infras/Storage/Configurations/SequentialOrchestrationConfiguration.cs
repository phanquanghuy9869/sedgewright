using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted sequential orchestration aggregate.
/// </summary>
public sealed class SequentialOrchestrationConfiguration : IEntityTypeConfiguration<SequentialOrchestration>
{
    public void Configure(EntityTypeBuilder<SequentialOrchestration> entity)
    {
        entity.ToTable("SequentialOrchestrations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Description).HasMaxLength(1024);
        entity.Property(x => x.ChainOnlyAgentResponses).IsRequired().HasDefaultValue(false);

        entity.HasMany(x => x.Steps)
            .WithOne(x => x.SequentialOrchestration)
            .HasForeignKey(x => x.SequentialOrchestrationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.Name).IsUnique();
    }
}
