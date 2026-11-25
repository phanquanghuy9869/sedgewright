using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the SQLite mapping for <see cref="ScheduledJobRun"/>.
/// </summary>
public sealed class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> entity)
    {
        entity.ToTable("ScheduledJobRuns");
        entity.HasKey(run => run.Id);
        entity.Property(run => run.Id).ValueGeneratedOnAdd();
        entity.Property(run => run.StartedAtUtc).IsRequired();
        entity.Property(run => run.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(16);

        entity.HasOne(run => run.AgentSessionRecord)
            .WithMany()
            .HasForeignKey(run => run.AgentSessionRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(run => new { run.ScheduledJobId, run.StartedAtUtc });
        entity.HasIndex(run => run.Status);
    }
}
