using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Manages scheduled jobs. Every change is written to the database first (the source of truth)
/// and then mirrored into the scheduler.
/// </summary>
public sealed class ScheduledJobUseCases(
    IScheduledJobRepository repository,
    IScheduledJobScheduler scheduler,
    ScheduledJobTargetResolver targetResolver,
    TimeProvider timeProvider,
    ILogger<ScheduledJobUseCases> logger)
{
    /// <summary>
    /// The default number of runs returned by <see cref="GetRunsAsync"/>.
    /// </summary>
    public const int DefaultRunHistorySize = 50;

    /// <summary>
    /// Gets all scheduled jobs ordered by name.
    /// </summary>
    public async Task<Result<List<ScheduledJob>, IApplicationError>> GetAllAsync(CancellationToken cancellationToken = default)
        => await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gets the most recent runs of one job, newest first.
    /// </summary>
    public async Task<Result<List<ScheduledJobRun>, IApplicationError>> GetRunsAsync(
        int scheduledJobId,
        int take = DefaultRunHistorySize,
        CancellationToken cancellationToken = default)
        => await repository.GetRunsAsync(scheduledJobId, Math.Max(1, take), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Validates and stores a new job, then schedules it when it is enabled.
    /// </summary>
    public Task<Result<ScheduledJob, IApplicationError>> CreateAsync(
        ScheduledJob draft,
        CancellationToken cancellationToken = default)
        => ValidateAsync(draft, cancellationToken)
            .Map(async () =>
            {
                DateTime now = UtcNow();
                ScheduledJob job = new()
                {
                    Name = draft.Name.Trim(),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    Prompt = draft.Prompt,
                };
                CopySettings(draft, job);
                repository.Create(job);
                await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return job;
            })
            .Bind(job => SyncSchedulerAsync(job, cancellationToken));

    /// <summary>
    /// Validates and stores changes to an existing job, then reschedules or unschedules it.
    /// </summary>
    public Task<Result<ScheduledJob, IApplicationError>> UpdateAsync(
        ScheduledJob draft,
        CancellationToken cancellationToken = default)
        => ValidateAsync(draft, cancellationToken)
            .Bind(() => LoadAsync(draft.Id, cancellationToken))
            .Map(async job =>
            {
                job.Name = draft.Name.Trim();
                job.Prompt = draft.Prompt;
                CopySettings(draft, job);
                job.UpdatedAtUtc = UtcNow();
                await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return job;
            })
            .Bind(job => SyncSchedulerAsync(job, cancellationToken));

    /// <summary>
    /// Deletes a job and its run history, then removes it from the scheduler.
    /// </summary>
    public Task<UnitResult<IApplicationError>> DeleteAsync(int scheduledJobId, CancellationToken cancellationToken = default)
        => LoadAsync(scheduledJobId, cancellationToken)
            .Tap(async job =>
            {
                repository.Delete(job);
                await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            })
            .Bind(job => scheduler.UnscheduleAsync(job.Id, cancellationToken));

    /// <summary>
    /// Enables or disables a job and updates the scheduler to match.
    /// </summary>
    public Task<Result<ScheduledJob, IApplicationError>> SetEnabledAsync(
        int scheduledJobId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
        => LoadAsync(scheduledJobId, cancellationToken)
            .Bind(job => isEnabled
                ? EnsureTargetExistsAsync(job, cancellationToken).Map(() => job)
                : Task.FromResult(Result.Success<ScheduledJob, IApplicationError>(job)))
            .Map(async job =>
            {
                job.IsEnabled = isEnabled;
                job.UpdatedAtUtc = UtcNow();
                await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return job;
            })
            .Bind(job => SyncSchedulerAsync(job, cancellationToken));

    /// <summary>
    /// Fires an enabled job immediately, outside its regular schedule.
    /// </summary>
    public Task<UnitResult<IApplicationError>> RunNowAsync(int scheduledJobId, CancellationToken cancellationToken = default)
        => LoadAsync(scheduledJobId, cancellationToken)
            .Ensure(job => job.IsEnabled, (IApplicationError)new InvalidOperationError(["Enable the job before running it."]))
            .Bind(job => scheduler.TriggerNowAsync(job.Id, cancellationToken));

    /// <summary>
    /// Rebuilds the scheduler from the database. Call it once after the scheduler has started.
    /// Runs left in <see cref="ScheduledJobRunStatus.Running"/> by a previous process are marked as abandoned.
    /// </summary>
    public async Task<UnitResult<IApplicationError>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        int abandoned = await repository.MarkRunningRunsAbandonedAsync(cancellationToken).ConfigureAwait(false);
        if (abandoned > 0)
        {
            logger.LogWarning("Marked {Count} interrupted scheduled job runs as abandoned.", abandoned);
        }

        List<ScheduledJob> enabledJobs = await repository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
        return await scheduler.ReconcileAsync(enabledJobs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the next time a job fires, or <see langword="null"/> when it is not scheduled.
    /// </summary>
    public Task<DateTimeOffset?> GetNextFireTimeAsync(int scheduledJobId, CancellationToken cancellationToken = default)
        => scheduler.GetNextFireTimeAsync(scheduledJobId, cancellationToken);

    private Task<UnitResult<IApplicationError>> ValidateAsync(ScheduledJob draft, CancellationToken cancellationToken)
        => draft.IsValidDefinition()
            .Bind(() => scheduler.ValidateSchedule(draft))
            .Ensure(
                () => !draft.IsEnabled || draft.ScheduleKind != ScheduleKind.Once || ToUtc(draft.RunAtUtc) > UtcNow(),
                (IApplicationError)new ValidationError(["Run time for a one-time job must be in the future."]))
            .Bind(() => EnsureTargetExistsAsync(draft, cancellationToken));

    private async Task<UnitResult<IApplicationError>> EnsureTargetExistsAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        IChatTarget? target = await targetResolver
            .ResolveAsync(job.TargetType, job.TargetId, cancellationToken)
            .ConfigureAwait(false);
        return UnitResult.FailureIf<IApplicationError>(
            target is null,
            new ValidationError([$"{job.TargetType} {job.TargetId} does not exist."]));
    }

    private async Task<Result<ScheduledJob, IApplicationError>> LoadAsync(int scheduledJobId, CancellationToken cancellationToken)
    {
        ScheduledJob? job = await repository.GetAsync(scheduledJobId, cancellationToken).ConfigureAwait(false);
        return job is null
            ? new InvalidOperationError([$"Scheduled job {scheduledJobId} does not exist."])
            : job;
    }

    private async Task<Result<ScheduledJob, IApplicationError>> SyncSchedulerAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        if (job.IsEnabled)
        {
            Result<DateTimeOffset?, IApplicationError> scheduled = await scheduler.ScheduleAsync(job, cancellationToken).ConfigureAwait(false);
            return scheduled.Map(_ => job);
        }

        UnitResult<IApplicationError> unscheduled = await scheduler.UnscheduleAsync(job.Id, cancellationToken).ConfigureAwait(false);
        return unscheduled.Map(() => job);
    }

    private static void CopySettings(ScheduledJob source, ScheduledJob target)
    {
        target.Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim();
        target.TargetType = source.TargetType;
        target.TargetId = source.TargetId;
        target.ScheduleKind = source.ScheduleKind;
        target.IntervalSeconds = source.ScheduleKind == ScheduleKind.Interval ? source.IntervalSeconds : null;
        target.CronExpression = source.ScheduleKind == ScheduleKind.Cron ? source.CronExpression?.Trim() : null;
        target.TimeZoneId = source.ScheduleKind == ScheduleKind.Cron && !string.IsNullOrWhiteSpace(source.TimeZoneId)
            ? source.TimeZoneId.Trim()
            : null;
        target.RunAtUtc = source.ScheduleKind == ScheduleKind.Once ? ToUtc(source.RunAtUtc) : null;
        target.StartAtUtc = ToUtc(source.StartAtUtc);
        target.EndAtUtc = ToUtc(source.EndAtUtc);
        target.IsEnabled = source.IsEnabled;
    }

    private static DateTime? ToUtc(DateTime? value)
        => value is DateTime dateTime
            ? dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : null;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
