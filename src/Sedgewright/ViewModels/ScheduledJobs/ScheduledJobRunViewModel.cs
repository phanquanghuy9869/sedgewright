using System;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Represents one scheduled job run in the history list.
/// </summary>
public sealed class ScheduledJobRunViewModel(ScheduledJobRun run)
{
    /// <summary>
    /// Gets the run status.
    /// </summary>
    public ScheduledJobRunStatus Status { get; } = run.Status;

    /// <summary>
    /// Gets the compact status label.
    /// </summary>
    public string StatusText => Status.ToString();

    /// <summary>
    /// Gets the start time.
    /// </summary>
    public DateTimeOffset StartedAt { get; } = run.StartedAt;

    /// <summary>
    /// Gets the completion time.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; } = run.CompletedAt;

    /// <summary>
    /// Gets the duration text.
    /// </summary>
    public string DurationText
    {
        get
        {
            if (CompletedAt is not DateTimeOffset completed)
            {
                return "In progress";
            }

            TimeSpan duration = completed - StartedAt;
            return duration.TotalSeconds < 60
                ? $"{Math.Max(0, (int)duration.TotalSeconds)}s"
                : $"{duration:mm\\:ss}";
        }
    }

    /// <summary>
    /// Gets produced output.
    /// </summary>
    public string? Output { get; } = run.Output;

    /// <summary>
    /// Gets the error text.
    /// </summary>
    public string? Error { get; } = run.Error;

    /// <summary>
    /// Gets concise text to show in the history row.
    /// </summary>
    public string DisplayText
        => !string.IsNullOrWhiteSpace(Error)
            ? Error
            : string.IsNullOrWhiteSpace(Output)
                ? "No output"
                : Output;

    /// <summary>
    /// Gets whether the run created an agent session.
    /// </summary>
    public bool HasAgentSession => run.AgentSessionRecordId.HasValue;
}
