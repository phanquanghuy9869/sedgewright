namespace Sedgewright.Entities;

/// <summary>
/// Identifies how a scheduled job decides when to fire.
/// </summary>
public enum ScheduleKind
{
    /// <summary>
    /// Fires repeatedly every <see cref="ScheduledJob.IntervalSeconds"/> seconds.
    /// </summary>
    Interval = 0,

    /// <summary>
    /// Fires according to <see cref="ScheduledJob.CronExpression"/> in <see cref="ScheduledJob.TimeZoneId"/>.
    /// </summary>
    Cron = 1,

    /// <summary>
    /// Fires a single time at <see cref="ScheduledJob.RunAtUtc"/>.
    /// </summary>
    Once = 2,
}
