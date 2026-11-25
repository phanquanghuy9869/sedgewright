using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Synchronizes persisted <see cref="ScheduledJob"/> rows with the background scheduling engine.
/// The database stays the source of truth; implementations only mirror it.
/// </summary>
public interface IScheduledJobScheduler
{
    /// <summary>
    /// Validates the schedule details the engine owns, such as cron syntax and time zone identifiers.
    /// </summary>
    UnitResult<IApplicationError> ValidateSchedule(ScheduledJob job);

    /// <summary>
    /// Creates or replaces the engine registration for one job. This call is idempotent.
    /// </summary>
    /// <returns>The next fire time, or <see langword="null"/> when the schedule will not fire again.</returns>
    Task<Result<DateTimeOffset?, IApplicationError>> ScheduleAsync(ScheduledJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the engine registration for one job if it exists.
    /// </summary>
    Task<UnitResult<IApplicationError>> UnscheduleAsync(int scheduledJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the engine match the supplied enabled jobs: schedules each one and removes registrations for any other job.
    /// </summary>
    Task<UnitResult<IApplicationError>> ReconcileAsync(IReadOnlyCollection<ScheduledJob> enabledJobs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fires the job immediately, outside its regular schedule.
    /// </summary>
    Task<UnitResult<IApplicationError>> TriggerNowAsync(int scheduledJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next fire time for one job, or <see langword="null"/> when it is not scheduled.
    /// </summary>
    Task<DateTimeOffset?> GetNextFireTimeAsync(int scheduledJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of every scheduled job execution that is currently running.
    /// </summary>
    Task InterruptAllAsync(CancellationToken cancellationToken = default);
}
