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
    /// Gets the UTC start time.
    /// </summary>
    public DateTime StartedAtUtc { get; } = run.StartedAtUtc;

    /// <summary>
    /// Gets the UTC completion time.
    /// </summary>
    public DateTime? CompletedAtUtc { get; } = run.CompletedAtUtc;

    /// <summary>
    /// Gets the duration text.
    /// </summary>
    public string DurationText
    {
        get
        {
            if (CompletedAtUtc is not DateTime completed)
            {
                return "In progress";
            }

            TimeSpan duration = completed - StartedAtUtc;
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
