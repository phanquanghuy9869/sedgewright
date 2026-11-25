using System;
using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Entities;

/// <summary>
/// Defines a prompt that runs against an agent or orchestration on an interval, a cron schedule, or once.
/// This row is the source of truth; the Quartz scheduler is rebuilt from it.
/// </summary>
public sealed class ScheduledJob
{
    /// <summary>
    /// The smallest interval accepted for <see cref="ScheduleKind.Interval"/> jobs.
    /// </summary>
    public const int MinimumIntervalSeconds = 60;

    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of target this job runs. Together with <see cref="TargetId"/> it identifies the target.
    /// </summary>
    public ChatTargetType TargetType { get; set; }

    public int TargetId { get; set; }

    public required string Prompt { get; set; }

    public ScheduleKind ScheduleKind { get; set; }

    public int? IntervalSeconds { get; set; }

    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the time zone used to evaluate <see cref="CronExpression"/>. Uses UTC when empty.
    /// </summary>
    public string? TimeZoneId { get; set; }

    public DateTime? RunAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the earliest time the job may fire. For interval jobs it is also the alignment anchor.
    /// </summary>
    public DateTime? StartAtUtc { get; set; }

    public DateTime? EndAtUtc { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    public ScheduledJobRunStatus? LastRunStatus { get; set; }

    public List<ScheduledJobRun> Runs { get; set; } = [];

    /// <summary>
    /// Validates the fields required by the job and its selected <see cref="ScheduleKind"/>.
    /// Cron syntax and time zone resolution are validated by the scheduler.
    /// </summary>
    public UnitResult<IApplicationError> IsValidDefinition()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Job name is required.");
        }

        if (string.IsNullOrWhiteSpace(Prompt))
        {
            errors.Add("Job prompt is required.");
        }

        if (!Enum.IsDefined(TargetType))
        {
            errors.Add("Job target type is not supported.");
        }

        if (TargetId <= 0)
        {
            errors.Add("Job target is required.");
        }

        switch (ScheduleKind)
        {
            case ScheduleKind.Interval:
                if (IntervalSeconds is null || IntervalSeconds < MinimumIntervalSeconds)
                {
                    errors.Add($"Interval must be at least {MinimumIntervalSeconds} seconds.");
                }

                break;
            case ScheduleKind.Cron:
                if (string.IsNullOrWhiteSpace(CronExpression))
                {
                    errors.Add("Cron expression is required.");
                }

                break;
            case ScheduleKind.Once:
                if (RunAtUtc is null)
                {
                    errors.Add("Run time is required for a one-time job.");
                }

                break;
            default:
                errors.Add("Schedule kind is not supported.");
                break;
        }

        if (StartAtUtc is DateTime start && EndAtUtc is DateTime end && end <= start)
        {
            errors.Add("End time must be after start time.");
        }

        return UnitResult.FailureIf<IApplicationError>(errors.Count > 0, new ValidationError([.. errors]));
    }
}
