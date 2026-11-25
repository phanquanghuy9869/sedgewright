using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Represents one scheduled job in the jobs list.
/// </summary>
public sealed class ScheduledJobListItemViewModel : ViewModelBase
{
    private readonly Action _editCallback;
    private readonly Action _deleteCallback;
    private readonly Action _toggleEnabledCallback;
    private readonly Action _runNowCallback;
    private readonly Action _selectCallback;
    private bool _isSelected;
    private DateTimeOffset? _nextFireTime;

    /// <summary>
    /// Creates a scheduled job list row.
    /// </summary>
    public ScheduledJobListItemViewModel(
        ScheduledJob job,
        string targetDisplay,
        Action editCallback,
        Action deleteCallback,
        Action toggleEnabledCallback,
        Action runNowCallback,
        Action selectCallback)
    {
        Job = job;
        TargetDisplay = targetDisplay;
        _editCallback = editCallback;
        _deleteCallback = deleteCallback;
        _toggleEnabledCallback = toggleEnabledCallback;
        _runNowCallback = runNowCallback;
        _selectCallback = selectCallback;

        EditCommand = new RelayCommand(_editCallback);
        DeleteCommand = new RelayCommand(_deleteCallback);
        ToggleEnabledCommand = new RelayCommand(_toggleEnabledCallback);
        RunNowCommand = new RelayCommand(_runNowCallback);
        SelectCommand = new RelayCommand(_selectCallback);
        Runs = [];
    }

    /// <summary>
    /// Gets the persisted job.
    /// </summary>
    public ScheduledJob Job { get; }

    /// <summary>
    /// Gets the row identifier.
    /// </summary>
    public int Id => Job.Id;

    /// <summary>
    /// Gets the job name.
    /// </summary>
    public string Name => Job.Name;

    /// <summary>
    /// Gets the optional description.
    /// </summary>
    public string Description => string.IsNullOrWhiteSpace(Job.Description) ? "No description" : Job.Description.Trim();

    /// <summary>
    /// Gets the target display name.
    /// </summary>
    public string TargetDisplay { get; }

    /// <summary>
    /// Gets whether the job is enabled.
    /// </summary>
    public bool IsEnabled => Job.IsEnabled;

    /// <summary>
    /// Gets the enabled-state label.
    /// </summary>
    public string EnabledText => Job.IsEnabled ? "Active" : "Paused";

    /// <summary>
    /// Gets the action label for toggling enabled state.
    /// </summary>
    public string ToggleEnabledText => Job.IsEnabled ? "Pause" : "Activate";

    /// <summary>
    /// Gets the schedule summary.
    /// </summary>
    public string ScheduleText => Job.ScheduleKind switch
    {
        ScheduleKind.Interval => $"Every {(Job.IntervalSeconds ?? 0) / 60} min",
        ScheduleKind.Cron => $"Cron  {Job.CronExpression}",
        ScheduleKind.Once => $"Once  {FormatUtc(Job.RunAtUtc)}",
        _ => Job.ScheduleKind.ToString(),
    };

    /// <summary>
    /// Gets the next fire time.
    /// </summary>
    public DateTimeOffset? NextFireTime
    {
        get => _nextFireTime;
        set
        {
            if (!SetProperty(ref _nextFireTime, value))
            {
                return;
            }

            OnPropertyChanged(nameof(NextFireText));
        }
    }

    /// <summary>
    /// Gets the compact next-fire label.
    /// </summary>
    public string NextFireText => NextFireTime is DateTimeOffset next
        ? $"Next  {next.ToLocalTime():yyyy-MM-dd HH:mm}"
        : "No future fire";

    /// <summary>
    /// Gets the last run label.
    /// </summary>
    public string LastRunText => Job.LastRunAtUtc is DateTime last
        ? $"{Job.LastRunStatus?.ToString() ?? "Unknown"}  {last.ToLocalTime():MM-dd HH:mm}"
        : "Never run";

    /// <summary>
    /// Gets whether the row is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Gets the loaded run history.
    /// </summary>
    public ObservableCollection<ScheduledJobRunViewModel> Runs { get; }

    /// <summary>
    /// Gets whether run history is available.
    /// </summary>
    public bool HasRuns => Runs.Count > 0;

    /// <summary>
    /// Gets whether no run history is available.
    /// </summary>
    public bool HasNoRuns => !HasRuns;

    /// <summary>
    /// Gets the edit command.
    /// </summary>
    public ICommand EditCommand { get; }

    /// <summary>
    /// Gets the delete command.
    /// </summary>
    public ICommand DeleteCommand { get; }

    /// <summary>
    /// Gets the enable/disable command.
    /// </summary>
    public ICommand ToggleEnabledCommand { get; }

    /// <summary>
    /// Gets the run-now command.
    /// </summary>
    public ICommand RunNowCommand { get; }

    /// <summary>
    /// Gets the select command.
    /// </summary>
    public ICommand SelectCommand { get; }

    /// <summary>
    /// Replaces the run history collection.
    /// </summary>
    public void SetRuns(System.Collections.Generic.IEnumerable<ScheduledJobRunViewModel> runs)
    {
        Runs.Clear();
        foreach (ScheduledJobRunViewModel run in runs)
        {
            Runs.Add(run);
        }

        OnPropertyChanged(nameof(HasRuns));
        OnPropertyChanged(nameof(HasNoRuns));
    }

    private static string FormatUtc(DateTime? value)
        => value is DateTime date
            ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "Not set";
}
