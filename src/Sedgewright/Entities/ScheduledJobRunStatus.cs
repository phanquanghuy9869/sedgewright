namespace Sedgewright.Entities;

/// <summary>
/// Describes the outcome of one scheduled job execution.
/// </summary>
public enum ScheduledJobRunStatus
{
    /// <summary>
    /// The run has started and has not completed yet.
    /// </summary>
    Running = 0,

    /// <summary>
    /// The target finished without errors.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// The target or the run setup reported an error.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The run was cancelled, usually because the application was shutting down.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The run was still marked as running when the application started again, so it never completed.
    /// </summary>
    Abandoned = 4,
}
