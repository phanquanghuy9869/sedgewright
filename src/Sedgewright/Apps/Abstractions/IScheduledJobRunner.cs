using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Executes one scheduled job against its agent or orchestration target and records the outcome.
/// </summary>
public interface IScheduledJobRunner
{
    /// <summary>
    /// Runs the scheduled job identified by <paramref name="scheduledJobId"/>.
    /// </summary>
    /// <returns>
    /// The recorded run, or <see langword="null"/> when the job no longer exists or is disabled and nothing ran.
    /// The result is a failure only when the run could not be recorded.
    /// </returns>
    Task<Result<ScheduledJobRun?, IApplicationError>> RunAsync(int scheduledJobId, CancellationToken cancellationToken = default);
}
