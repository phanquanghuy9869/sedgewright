using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted agent session record mapping.
/// </summary>
public sealed class AgentSessionRecordConfiguration : IEntityTypeConfiguration<AgentSessionRecord>
{
    public void Configure(EntityTypeBuilder<AgentSessionRecord> entity)
    {
        entity.ToTable("AgentSessions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.AgentDefinitionId).IsRequired();
        entity.Property(x => x.Summary).IsRequired().HasMaxLength(1000);
        entity.Property(x => x.SerializedSessionJson).IsRequired();
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.LastUsedAtUtc).IsRequired();

        entity.HasOne(x => x.AgentDefinition)
            .WithMany(x => x.AgentSessions)
            .HasForeignKey(x => x.AgentDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.AgentDefinitionId);
        entity.HasIndex(x => x.LastUsedAtUtc);
    }
}
