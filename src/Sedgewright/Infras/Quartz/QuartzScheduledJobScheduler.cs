using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Quartz;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Jobs;
using Sedgewright.Models;

namespace Sedgewright.Infras.Quartz;

/// <summary>
/// Mirrors persisted <see cref="ScheduledJob"/> rows into a Quartz scheduler using stable job and trigger keys.
/// </summary>
public sealed class QuartzScheduledJobScheduler(
    ISchedulerFactory schedulerFactory,
    TimeProvider timeProvider,
    ILogger<QuartzScheduledJobScheduler> logger) : IScheduledJobScheduler
{
    /// <summary>
    /// The Quartz group that owns every scheduled job registration.
    /// </summary>
    public const string Group = "sedgewright.scheduled-jobs";

    /// <summary>
    /// The job data key that carries the <see cref="ScheduledJob.Id"/>.
    /// </summary>
    public const string ScheduledJobIdKey = "ScheduledJobId";

    private const string KeyPrefix = "scheduled-job-";

    /// <summary>
    /// Builds the stable Quartz job key for a scheduled job.
    /// </summary>
    public static JobKey GetJobKey(int scheduledJobId) => new($"{KeyPrefix}{scheduledJobId}", Group);

    /// <summary>
    /// Builds the stable Quartz trigger key for a scheduled job.
    /// </summary>
    public static TriggerKey GetTriggerKey(int scheduledJobId) => new($"{KeyPrefix}{scheduledJobId}", Group);

    /// <inheritdoc />
    public UnitResult<IApplicationError> ValidateSchedule(ScheduledJob job)
    {
        if (job.ScheduleKind != ScheduleKind.Cron)
        {
            return UnitResult.Success<IApplicationError>();
        }

        List<string> errors = [];
        if (!CronExpression.TryParse(job.CronExpression ?? string.Empty, out _))
        {
            errors.Add($"Cron expression '{job.CronExpression}' is not valid.");
        }

        if (ResolveTimeZone(job.TimeZoneId).IsFailure)
        {
            errors.Add($"Time zone '{job.TimeZoneId}' was not found.");
        }

        return UnitResult.FailureIf<IApplicationError>(errors.Count > 0, new ValidationError([.. errors]));
    }

    /// <inheritdoc />
    public async Task<Result<DateTimeOffset?, IApplicationError>> ScheduleAsync(
        ScheduledJob job,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Result<ITrigger?, IApplicationError> triggerResult = job.BuildTrigger(timeProvider.GetUtcNow());
            if (triggerResult.IsFailure)
            {
                return Result.Failure<DateTimeOffset?, IApplicationError>(triggerResult.Error);
            }

            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            if (triggerResult.Value is not ITrigger trigger)
            {
                await scheduler.DeleteJob(GetJobKey(job.Id), cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Scheduled job {ScheduledJobId} has no future fire time; it was not scheduled.", job.Id);
                return Result.Success<DateTimeOffset?, IApplicationError>(null);
            }

            await scheduler
                .ScheduleJob(BuildJob(job.Id), [trigger], ScheduleJobOptions.Replacing, cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset? nextFireTime = await GetNextFireTimeAsync(job.Id, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Scheduled job {ScheduledJobId} ({ScheduleKind}) next fires at {NextFireTimeUtc:u}.",
                job.Id,
                job.ScheduleKind,
                nextFireTime);
            return Result.Success<DateTimeOffset?, IApplicationError>(nextFireTime);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<DateTimeOffset?, IApplicationError>(
                new ExceptionError([$"Could not schedule job {job.Id}: {exception.Message}"], exception));
        }
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> UnscheduleAsync(
        int scheduledJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            await scheduler.DeleteJob(GetJobKey(scheduledJobId), cancellationToken).ConfigureAwait(false);
            return UnitResult.Success<IApplicationError>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnitResult.Failure<IApplicationError>(
                new ExceptionError([$"Could not unschedule job {scheduledJobId}: {exception.Message}"], exception));
        }
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ReconcileAsync(
        IReadOnlyCollection<ScheduledJob> enabledJobs,
        CancellationToken cancellationToken = default)
    {
        List<IApplicationError> errors = [];
        foreach (ScheduledJob job in enabledJobs)
        {
            Result<DateTimeOffset?, IApplicationError> result = await ScheduleAsync(job, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                logger.LogWarning("Could not reconcile scheduled job {ScheduledJobId}: {Errors}", job.Id, result.Error.Messages);
                errors.Add(result.Error);
            }
        }

        try
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            HashSet<JobKey> expectedKeys = [.. enabledJobs.Select(job => GetJobKey(job.Id))];
            IReadOnlyCollection<JobKey> existingKeys = await scheduler
                .GetJobKeys(GroupMatcher<JobKey>.GroupEquals(Group), cancellationToken)
                .ConfigureAwait(false);
            foreach (JobKey orphan in existingKeys.Where(key => !expectedKeys.Contains(key)))
            {
                await scheduler.DeleteJob(orphan, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Removed orphaned Quartz job {JobKey}.", orphan);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            errors.Add(new ExceptionError([$"Could not remove orphaned jobs: {exception.Message}"], exception));
        }

        return UnitResult.FailureIf<IApplicationError>(
            errors.Count > 0,
            new GenericError(errors.GetMessages()));
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> TriggerNowAsync(
        int scheduledJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            JobKey jobKey = GetJobKey(scheduledJobId);
            if (await scheduler.GetJobDetail(jobKey, cancellationToken).ConfigureAwait(false) is null)
            {
                // Disabled or finished one-time jobs have no stored job, so store one durably to fire it now.
                await scheduler.AddJob(BuildJob(scheduledJobId), AddJobOptions.Replacing, cancellationToken).ConfigureAwait(false);
            }

            await scheduler.TriggerJob(jobKey, cancellationToken).ConfigureAwait(false);
            return UnitResult.Success<IApplicationError>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnitResult.Failure<IApplicationError>(
                new ExceptionError([$"Could not trigger job {scheduledJobId}: {exception.Message}"], exception));
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetNextFireTimeAsync(int scheduledJobId, CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
        ITrigger? trigger = await scheduler.GetTrigger(GetTriggerKey(scheduledJobId), cancellationToken).ConfigureAwait(false);
        return trigger?.NextFireTimeUtc;
    }

    /// <inheritdoc />
    public async Task InterruptAllAsync(CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<JobKey> jobKeys = await scheduler
            .GetJobKeys(GroupMatcher<JobKey>.GroupEquals(Group), cancellationToken)
            .ConfigureAwait(false);
        foreach (JobKey jobKey in jobKeys)
        {
            await scheduler.Interrupt(jobKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IJobDetail BuildJob(int scheduledJobId)
        => JobBuilder.Create<ScheduledJobQuartzJob>()
            .WithIdentity(GetJobKey(scheduledJobId))
            .UsingJobData(ScheduledJobIdKey, scheduledJobId.ToString(CultureInfo.InvariantCulture))
            .StoreDurably()
            .Build();

    private static Result<TimeZoneInfo, IApplicationError> ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            // Accepts both IANA and Windows identifiers on every platform.
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new ValidationError([$"Time zone '{timeZoneId}' was not found."]);
        }
    }

}
