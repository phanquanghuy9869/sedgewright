using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Quartz;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Quartz;
using Sedgewright.Models;

namespace Sedgewright.Infras.Jobs;

/// <summary>
/// Quartz entry point for persisted scheduled jobs. It carries only the <see cref="ScheduledJob.Id"/>
/// and delegates the actual work to <see cref="IScheduledJobRunner"/>, which reloads the job from storage.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ScheduledJobQuartzJob(
    IScheduledJobRunner runner,
    ILogger<ScheduledJobQuartzJob> logger) : IJob
{
    /// <inheritdoc />
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        JobKey jobKey = context.JobDetail.Key;
        if (!TryGetScheduledJobId(context.MergedJobDataMap, out int scheduledJobId))
        {
            logger.LogError("Quartz job {JobKey} has no valid {DataKey}; skipping.", jobKey, QuartzScheduledJobScheduler.ScheduledJobIdKey);
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        logger.LogInformation("Scheduled job {ScheduledJobId} ({JobKey}) started.", scheduledJobId, jobKey);

        Result<ScheduledJobRun?, IApplicationError> result = await runner
            .RunAsync(scheduledJobId, cancellationToken)
            .ConfigureAwait(false);

        TimeSpan duration = Stopwatch.GetElapsedTime(startedAt);
        if (result.IsFailure)
        {
            logger.LogError(
                "Scheduled job {ScheduledJobId} ({JobKey}) could not be recorded after {Duration}: {Errors}",
                scheduledJobId,
                jobKey,
                duration,
                result.Error.Messages);
            return;
        }

        logger.LogInformation(
            "Scheduled job {ScheduledJobId} ({JobKey}) finished with {Status} after {Duration}.",
            scheduledJobId,
            jobKey,
            result.Value?.Status.ToString() ?? "Skipped",
            duration);
    }

    private static bool TryGetScheduledJobId(JobDataMap dataMap, out int scheduledJobId)
    {
        scheduledJobId = 0;
        return dataMap.TryGetValue(QuartzScheduledJobScheduler.ScheduledJobIdKey, out object? value)
            && value switch
            {
                int id => (scheduledJobId = id) > 0,
                string text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out scheduledJobId) && scheduledJobId > 0,
                _ => false,
            };
    }
}
