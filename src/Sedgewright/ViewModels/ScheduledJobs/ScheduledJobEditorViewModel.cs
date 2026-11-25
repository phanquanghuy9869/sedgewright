using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Drives the modal scheduled job editor.
/// </summary>
public sealed class ScheduledJobEditorViewModel : ViewModelBase
{
    private readonly ScheduledJobUseCases _useCases;
    private readonly ScheduledJob? _existingJob;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private bool _isSaving;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _prompt = string.Empty;
    private bool _isEnabled = true;
    private ScheduledJobTargetOptionViewModel? _selectedTarget;
    private ScheduleKindOptionViewModel? _selectedScheduleKind;
    private string _intervalMinutes = "60";
    private string _cronExpression = "0 0 9 * * ?";
    private string _timeZoneId = "UTC";
    private string _runAtText = string.Empty;
    private string _startAtText = string.Empty;
    private string _endAtText = string.Empty;
    private string _validationText = string.Empty;

    /// <summary>
    /// Creates a scheduled job editor for a new or existing job.
    /// </summary>
    public ScheduledJobEditorViewModel(
        ScheduledJob? existingJob,
        IReadOnlyList<IChatTarget> targets,
        ScheduledJobUseCases useCases)
    {
        _existingJob = existingJob;
        _useCases = useCases;

        TargetOptions = new ObservableCollection<ScheduledJobTargetOptionViewModel>(
            targets.Select(target => new ScheduledJobTargetOptionViewModel(target)));
        ScheduleKindOptions =
        [
            new ScheduleKindOptionViewModel(ScheduleKind.Interval),
            new ScheduleKindOptionViewModel(ScheduleKind.Cron),
            new ScheduleKindOptionViewModel(ScheduleKind.Once),
        ];

        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);

        WindowTitle = existingJob is null ? "Add scheduled job" : "Edit scheduled job";
        SubtitleText = existingJob is null
            ? "Choose a target and define when it should run."
            : "Update the target, prompt, and schedule.";

        if (existingJob is not null)
        {
            InitializeExistingJob(existingJob);
        }
        else
        {
            SelectedScheduleKind = ScheduleKindOptions.First();
        }
    }

    /// <summary>
    /// Raised when the editor should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle { get; }

    /// <summary>
    /// Gets the window subtitle.
    /// </summary>
    public string SubtitleText { get; }

    /// <summary>
    /// Gets the target options.
    /// </summary>
    public ObservableCollection<ScheduledJobTargetOptionViewModel> TargetOptions { get; }

    /// <summary>
    /// Gets the schedule kind options.
    /// </summary>
    public IReadOnlyList<ScheduleKindOptionViewModel> ScheduleKindOptions { get; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the prompt sent to the target.
    /// </summary>
    public string Prompt
    {
        get => _prompt;
        set
        {
            if (SetProperty(ref _prompt, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the job is active.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected target.
    /// </summary>
    public ScheduledJobTargetOptionViewModel? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected schedule kind.
    /// </summary>
    public ScheduleKindOptionViewModel? SelectedScheduleKind
    {
        get => _selectedScheduleKind;
        set
        {
            if (!SetProperty(ref _selectedScheduleKind, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsIntervalSchedule));
            OnPropertyChanged(nameof(IsCronSchedule));
            OnPropertyChanged(nameof(IsOnceSchedule));
            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets whether interval fields are visible.
    /// </summary>
    public bool IsIntervalSchedule => SelectedScheduleKind?.Kind == ScheduleKind.Interval;

    /// <summary>
    /// Gets whether cron fields are visible.
    /// </summary>
    public bool IsCronSchedule => SelectedScheduleKind?.Kind == ScheduleKind.Cron;

    /// <summary>
    /// Gets whether one-time fields are visible.
    /// </summary>
    public bool IsOnceSchedule => SelectedScheduleKind?.Kind == ScheduleKind.Once;

    /// <summary>
    /// Gets or sets the interval in minutes.
    /// </summary>
    public string IntervalMinutes
    {
        get => _intervalMinutes;
        set
        {
            if (SetProperty(ref _intervalMinutes, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Quartz cron expression.
    /// </summary>
    public string CronExpression
    {
        get => _cronExpression;
        set
        {
            if (SetProperty(ref _cronExpression, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the cron time zone identifier.
    /// </summary>
    public string TimeZoneId
    {
        get => _timeZoneId;
        set
        {
            if (SetProperty(ref _timeZoneId, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the one-time local date and time.
    /// </summary>
    public string RunAtText
    {
        get => _runAtText;
        set
        {
            if (SetProperty(ref _runAtText, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the optional local start boundary.
    /// </summary>
    public string StartAtText
    {
        get => _startAtText;
        set
        {
            if (SetProperty(ref _startAtText, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the optional local end boundary.
    /// </summary>
    public string EndAtText
    {
        get => _endAtText;
        set
        {
            if (SetProperty(ref _endAtText, value))
            {
                OnEditorFieldChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets concise validation feedback.
    /// </summary>
    public string ValidationText
    {
        get => _validationText;
        private set
        {
            if (SetProperty(ref _validationText, value))
            {
                OnPropertyChanged(nameof(HasValidationText));
            }
        }
    }

    /// <summary>
    /// Gets whether validation feedback is visible.
    /// </summary>
    public bool HasValidationText => !string.IsNullOrWhiteSpace(ValidationText);

    /// <summary>
    /// Gets the save command.
    /// </summary>
    public ICommand SaveCommand => _saveCommand;

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    private bool CanSave()
        => !_isSaving
            && !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Prompt)
            && SelectedTarget is not null
            && SelectedScheduleKind is not null;

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        try
        {
            ScheduledJob draft = BuildDraft();
            Result<ScheduledJob, IApplicationError> result = _existingJob is null
                ? await _useCases.CreateAsync(draft)
                : await _useCases.UpdateAsync(draft);

            if (result.IsFailure)
            {
                ValidationText = string.Join(Environment.NewLine, result.Error.Messages);
                return;
            }

            ValidationText = string.Empty;
            CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());
        }
        catch (Exception exception)
        {
            ValidationText = $"Could not save the scheduled job: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());

    private ScheduledJob BuildDraft()
    {
        if (SelectedTarget is null)
        {
            throw new InvalidOperationException("Select an agent or orchestration.");
        }

        if (SelectedScheduleKind is null)
        {
            throw new InvalidOperationException("Select a schedule kind.");
        }

        ScheduleKind scheduleKind = SelectedScheduleKind.Kind;
        DateTime? runAtUtc = null;
        int? intervalSeconds = null;
        string? cronExpression = null;
        string? timeZoneId = null;

        if (scheduleKind == ScheduleKind.Interval)
        {
            if (!int.TryParse(IntervalMinutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intervalMinutes)
                || intervalMinutes < 1)
            {
                throw new InvalidOperationException("Interval must be at least 1 minute.");
            }

            intervalSeconds = intervalMinutes * 60;
        }
        else if (scheduleKind == ScheduleKind.Cron)
        {
            cronExpression = CronExpression.Trim();
            timeZoneId = string.IsNullOrWhiteSpace(TimeZoneId) ? null : TimeZoneId.Trim();
        }
        else
        {
            runAtUtc = ParseLocalUtc(RunAtText, "one-time run time");
        }

        return new ScheduledJob
        {
            Id = _existingJob?.Id ?? 0,
            Name = Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            TargetType = SelectedTarget.Identity.TargetType,
            TargetId = SelectedTarget.Identity.Id,
            Prompt = Prompt.Trim(),
            ScheduleKind = scheduleKind,
            IntervalSeconds = intervalSeconds,
            CronExpression = cronExpression,
            TimeZoneId = timeZoneId,
            RunAtUtc = runAtUtc,
            StartAtUtc = ParseOptionalLocalUtc(StartAtText),
            EndAtUtc = ParseOptionalLocalUtc(EndAtText),
            IsEnabled = IsEnabled,
        };
    }

    private void InitializeExistingJob(ScheduledJob job)
    {
        Name = job.Name;
        Description = job.Description ?? string.Empty;
        Prompt = job.Prompt;
        IsEnabled = job.IsEnabled;
        SelectedScheduleKind = ScheduleKindOptions.First(option => option.Kind == job.ScheduleKind);
        SelectedTarget = TargetOptions.FirstOrDefault(option =>
            option.Identity.TargetType == job.TargetType && option.Identity.Id == job.TargetId);

        if (job.IntervalSeconds is int seconds)
        {
            IntervalMinutes = ((seconds + 59) / 60).ToString(CultureInfo.InvariantCulture);
        }

        CronExpression = job.CronExpression ?? "0 0 9 * * ?";
        TimeZoneId = job.TimeZoneId ?? "UTC";
        RunAtText = FormatLocal(job.RunAtUtc);
        StartAtText = FormatLocal(job.StartAtUtc);
        EndAtText = FormatLocal(job.EndAtUtc);
    }

    private void OnEditorFieldChanged()
    {
        if (!string.IsNullOrWhiteSpace(ValidationText))
        {
            ValidationText = string.Empty;
        }

        _saveCommand.RaiseCanExecuteChanged();
    }

    private static DateTime? ParseOptionalLocalUtc(string text)
        => string.IsNullOrWhiteSpace(text) ? null : ParseLocalUtc(text, "schedule boundary");

    private static DateTime ParseLocalUtc(string text, string fieldName)
    {
        if (!DateTime.TryParseExact(
                text.Trim(),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTime local))
        {
            throw new InvalidOperationException($"{fieldName} must use yyyy-MM-dd HH:mm.");
        }

        return local.ToUniversalTime();
    }

    private static string FormatLocal(DateTime? value)
        => value is DateTime date
            ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
}
