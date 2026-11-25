using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes scheduled jobs and their run history.
/// </summary>
public interface IScheduledJobRepository
{
    /// <summary>
    /// Gets all scheduled jobs ordered by name, without tracking.
    /// </summary>
    Task<List<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets only the enabled scheduled jobs, without tracking.
    /// </summary>
    Task<List<ScheduledJob>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one tracked scheduled job by identifier.
    /// </summary>
    Task<ScheduledJob?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent runs of a job, newest first.
    /// </summary>
    Task<List<ScheduledJobRun>> GetRunsAsync(int scheduledJobId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new scheduled job.
    /// </summary>
    void Create(ScheduledJob job);

    /// <summary>
    /// Updates an existing scheduled job.
    /// </summary>
    void Update(ScheduledJob job);

    /// <summary>
    /// Removes a scheduled job together with its run history.
    /// </summary>
    void Delete(ScheduledJob job);

    /// <summary>
    /// Adds a new run record.
    /// </summary>
    void CreateRun(ScheduledJobRun run);

    /// <summary>
    /// Marks every run still in <see cref="ScheduledJobRunStatus.Running"/> as <see cref="ScheduledJobRunStatus.Abandoned"/>.
    /// Call this at startup to clean up runs interrupted by a crash.
    /// </summary>
    /// <returns>The number of runs that were updated.</returns>
    Task<int> MarkRunningRunsAbandonedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
