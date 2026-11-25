using Sedgewright.Entities;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Represents a schedule kind in editor and list controls.
/// </summary>
public sealed class ScheduleKindOptionViewModel(ScheduleKind kind)
{
    /// <summary>
    /// Gets the schedule kind.
    /// </summary>
    public ScheduleKind Kind { get; } = kind;

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public string Label { get; } = kind switch
    {
        ScheduleKind.Interval => "Interval",
        ScheduleKind.Cron => "Cron",
        ScheduleKind.Once => "One time",
        _ => kind.ToString(),
    };
}
