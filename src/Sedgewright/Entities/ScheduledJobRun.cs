using System;

namespace Sedgewright.Entities;

/// <summary>
/// Records one execution of a <see cref="ScheduledJob"/>, including its outcome and produced output.
/// </summary>
public sealed class ScheduledJobRun
{
    public int Id { get; set; }

    public int ScheduledJobId { get; set; }

    public ScheduledJob ScheduledJob { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ScheduledJobRunStatus Status { get; set; } = ScheduledJobRunStatus.Running;

    /// <summary>
    /// Gets or sets the aggregated final output produced by the target.
    /// </summary>
    public string? Output { get; set; }

    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the persisted agent session created by this run when the target is an agent.
    /// </summary>
    public int? AgentSessionRecordId { get; set; }

    public AgentSessionRecord? AgentSessionRecord { get; set; }
}
