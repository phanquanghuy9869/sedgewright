using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the SQLite mapping for <see cref="ScheduledJob"/>.
/// </summary>
public sealed class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> entity)
    {
        entity.ToTable("ScheduledJobs");
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Id).ValueGeneratedOnAdd();
        entity.Property(job => job.Name).IsRequired().HasMaxLength(200);
        entity.Property(job => job.Description).HasMaxLength(2000);
        entity.Property(job => job.TargetType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32);
        entity.Property(job => job.TargetId).IsRequired();
        entity.Property(job => job.Prompt).IsRequired();
        entity.Property(job => job.ScheduleKind)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(16);
        entity.Property(job => job.CronExpression).HasMaxLength(256);
        entity.Property(job => job.TimeZoneId).HasMaxLength(128);
        entity.Property(job => job.LastRunStatus)
            .HasConversion<string?>()
            .HasMaxLength(16);
        entity.Property(job => job.CreatedAt).IsRequired();
        entity.Property(job => job.UpdatedAt).IsRequired();

        entity.HasMany(job => job.Runs)
            .WithOne(run => run.ScheduledJob)
            .HasForeignKey(run => run.ScheduledJobId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(job => job.IsEnabled);
        entity.HasIndex(job => new { job.TargetType, job.TargetId });
    }
}
