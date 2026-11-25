using System;
using CSharpFunctionalExtensions;
using Quartz;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Quartz;

/// <summary>
/// Provides Quartz trigger conversion for scheduled jobs.
/// </summary>
public static class ScheduledJobQuartzExtensions
{
    extension(ScheduledJob job)
    {
        /// <summary>
        /// Builds the trigger for this job, or returns <see langword="null"/> when the schedule cannot fire after <paramref name="now"/>.
        /// Missed fire times are skipped rather than caught up.
        /// </summary>
        public Result<ITrigger?, IApplicationError> BuildTrigger(DateTimeOffset now)
        {
            DateTimeOffset? endAt = job.EndAt;
            if (endAt is DateTimeOffset end && end <= now)
            {
                return Result.Success<ITrigger?, IApplicationError>(null);
            }

            Result<(DateTimeOffset StartAt, IScheduleBuilder Schedule), IApplicationError> scheduleResult = job.ScheduleKind switch
            {
                ScheduleKind.Interval => BuildIntervalSchedule(job, now),
                ScheduleKind.Cron => BuildCronSchedule(job, now),
                ScheduleKind.Once => BuildOnceSchedule(job),
                _ => new ValidationError([$"Schedule kind '{job.ScheduleKind}' is not supported."]),
            };

            return scheduleResult.Map(schedule =>
            {
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(QuartzScheduledJobScheduler.GetTriggerKey(job.Id))
                    .ForJob(QuartzScheduledJobScheduler.GetJobKey(job.Id))
                    .StartAt(schedule.StartAt)
                    .EndAt(endAt)
                    .WithSchedule(schedule.Schedule)
                    .Build();
                return trigger.GetFireTimeAfter(now.AddTicks(-1)) is null ? null : trigger;
            });
        }
    }

    private static Result<(DateTimeOffset StartAt, IScheduleBuilder Schedule), IApplicationError> BuildIntervalSchedule(
        ScheduledJob job,
        DateTimeOffset now)
    {
        if (job.IntervalSeconds is not int seconds || seconds < ScheduledJob.MinimumIntervalSeconds)
        {
            return new ValidationError([$"Interval must be at least {ScheduledJob.MinimumIntervalSeconds} seconds."]);
        }

        TimeSpan interval = TimeSpan.FromSeconds(seconds);
        DateTimeOffset anchor = job.StartAt
            ?? (job.CreatedAt == default ? now : job.CreatedAt);
        IScheduleBuilder schedule = SimpleScheduleBuilder.Create()
            .WithInterval(interval)
            .RepeatForever()
            .WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount);
        return (GetNextAlignedTime(anchor, interval, now), schedule);
    }

    private static Result<(DateTimeOffset StartAt, IScheduleBuilder Schedule), IApplicationError> BuildCronSchedule(
        ScheduledJob job,
        DateTimeOffset now)
    {
        if (!CronExpression.TryParse(job.CronExpression ?? string.Empty, out _))
        {
            return new ValidationError([$"Cron expression '{job.CronExpression}' is not valid."]);
        }

        return ResolveTimeZone(job.TimeZoneId).Map(timeZone =>
        {
            DateTimeOffset startAt = job.StartAt is DateTimeOffset start && start > now ? start : now;
            IScheduleBuilder schedule = CronScheduleBuilder.Create(job.CronExpression!)
                .InTimeZone(timeZone)
                .WithMisfireInstruction(CronTriggerMisfireInstruction.DoNothing);
            return (startAt, schedule);
        });
    }

    private static Result<(DateTimeOffset StartAt, IScheduleBuilder Schedule), IApplicationError> BuildOnceSchedule(
        ScheduledJob job)
    {
        if (job.RunAt is not DateTimeOffset runAt)
        {
            return new ValidationError(["Run time is required for a one-time job."]);
        }

        IScheduleBuilder schedule = SimpleScheduleBuilder.Create()
            .WithRepeatCount(0)
            .WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount);
        return (runAt, schedule);
    }

    private static DateTimeOffset GetNextAlignedTime(
        DateTimeOffset anchor,
        TimeSpan interval,
        DateTimeOffset now)
    {
        if (anchor >= now)
        {
            return anchor;
        }

        long elapsedTicks = (now - anchor).Ticks;
        long steps = (elapsedTicks + interval.Ticks - 1) / interval.Ticks;
        return anchor + TimeSpan.FromTicks(steps * interval.Ticks);
    }

    private static Result<TimeZoneInfo, IApplicationError> ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new ValidationError([$"Time zone '{timeZoneId}' was not found."]);
        }
    }
}
