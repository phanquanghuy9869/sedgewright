using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists scheduled jobs and their run history in SQLite.
/// </summary>
public sealed class ScheduledJobRepository(SedgewrightDbContext dbContext) : IScheduledJobRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.ScheduledJobs
            .AsNoTracking()
            .OrderBy(job => job.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<List<ScheduledJob>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => _dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(job => job.IsEnabled)
            .OrderBy(job => job.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ScheduledJob?> GetAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.ScheduledJobs.FirstOrDefaultAsync(job => job.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<List<ScheduledJobRun>> GetRunsAsync(int scheduledJobId, int take, CancellationToken cancellationToken = default)
        => _dbContext.ScheduledJobRuns
            .AsNoTracking()
            .Where(run => run.ScheduledJobId == scheduledJobId)
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Create(ScheduledJob job)
        => _dbContext.ScheduledJobs.Add(job);

    /// <inheritdoc />
    public void Update(ScheduledJob job)
        => _dbContext.ScheduledJobs.Update(job);

    /// <inheritdoc />
    public void Delete(ScheduledJob job)
        => _dbContext.ScheduledJobs.Remove(job);

    /// <inheritdoc />
    public void CreateRun(ScheduledJobRun run)
        => _dbContext.ScheduledJobRuns.Add(run);

    /// <inheritdoc />
    public Task<int> MarkRunningRunsAbandonedAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        return _dbContext.ScheduledJobRuns
            .Where(run => run.Status == ScheduledJobRunStatus.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Status, ScheduledJobRunStatus.Abandoned)
                    .SetProperty(run => run.CompletedAtUtc, now)
                    .SetProperty(run => run.Error, "The application stopped before this run completed."),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
