using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Chat;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Manages the scheduled jobs list and run-history view.
/// </summary>
public sealed class ScheduledJobsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatTargetCatalogService _targetCatalog;
    private readonly IScheduledJobDialogService _dialogService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _addCommand;
    private ScheduledJobListItemViewModel? _selectedItem;
    private string _statusText = string.Empty;

    /// <summary>
    /// Creates the scheduled jobs page view-model.
    /// </summary>
    public ScheduledJobsViewModel(
        IServiceScopeFactory scopeFactory,
        IChatTargetCatalogService targetCatalog,
        IScheduledJobDialogService dialogService)
    {
        _scopeFactory = scopeFactory;
        _targetCatalog = targetCatalog;
        _dialogService = dialogService;
        Items = [];
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _addCommand = new AsyncRelayCommand(AddAsync);

        _refreshCommand.Execute(null);
    }

    /// <summary>
    /// Gets the scheduled job rows.
    /// </summary>
    public ObservableCollection<ScheduledJobListItemViewModel> Items { get; }

    /// <summary>
    /// Gets whether any jobs exist.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets whether no jobs exist.
    /// </summary>
    public bool HasNoItems => !HasItems;

    /// <summary>
    /// Gets the page title.
    /// </summary>
    public string TitleText => "Scheduled jobs";

    /// <summary>
    /// Gets the page subtitle.
    /// </summary>
    public string SubtitleText => "Run agents and orchestrations on an interval, cron schedule, or one-time date.";

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the add-job command.
    /// </summary>
    public ICommand AddCommand => _addCommand;

    /// <summary>
    /// Gets the selected job row.
    /// </summary>
    public ScheduledJobListItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (!SetProperty(ref _selectedItem, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedItem));
        }
    }

    /// <summary>
    /// Gets whether one job is selected.
    /// </summary>
    public bool HasSelectedItem => SelectedItem is not null;

    /// <summary>
    /// Gets status feedback for the page.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    /// <summary>
    /// Gets whether status feedback is visible.
    /// </summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>
    /// Gets the empty-state title.
    /// </summary>
    public string EmptyStateTitle => "No scheduled jobs.";

    /// <summary>
    /// Gets the empty-state description.
    /// </summary>
    public string EmptyStateText => "Add a job to run an agent or orchestration automatically.";

    private async Task RefreshAsync()
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
            IReadOnlyList<IChatTarget> targets = await _targetCatalog.GetTargetsAsync();
            Result<List<ScheduledJob>, IApplicationError> result = await useCases.GetAllAsync();
            if (result.IsFailure)
            {
                StatusText = string.Join(" | ", result.Error.Messages);
                return;
            }

            Dictionary<(ChatTargetType TargetType, int TargetId), IChatTarget> targetByIdentity = targets
                .ToDictionary(
                    target => (ChatTargetIdentity.From(target).TargetType, ChatTargetIdentity.From(target).Id),
                    target => target);

            List<ScheduledJobListItemViewModel> rows = [];
            foreach (ScheduledJob job in result.Value)
            {
                IChatTarget? target = targetByIdentity.TryGetValue((job.TargetType, job.TargetId), out IChatTarget? found)
                    ? found
                    : null;
                ScheduledJobListItemViewModel row = new(
                    job,
                    target is null ? $"{job.TargetType} {job.TargetId}" : ChatTargetDisplayInfo.From(target).Summary,
                    () => _ = EditAsync(job.Id),
                    () => _ = DeleteAsync(job.Id),
                    () => _ = ToggleEnabledAsync(job.Id),
                    () => _ = RunNowAsync(job.Id),
                    () => _ = SelectAsync(job.Id));
                rows.Add(row);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Items.Clear();
                foreach (ScheduledJobListItemViewModel row in rows)
                {
                    Items.Add(row);
                }

                SelectedItem = null;
                StatusText = string.Empty;
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(HasNoItems));
            });

            await PopulateNextFireTimesAsync(rows);
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load scheduled jobs: {exception.Message}";
        }
    }

    private async Task AddAsync()
        => await EditAsync(null);

    private async Task EditAsync(int? jobId)
    {
        try
        {
            IReadOnlyList<IChatTarget> targets = await _targetCatalog.GetTargetsAsync();
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
            ScheduledJob? job = null;
            if (jobId.HasValue)
            {
                Result<List<ScheduledJob>, IApplicationError> allResult = await useCases.GetAllAsync();
                if (allResult.IsFailure)
                {
                    StatusText = string.Join(" | ", allResult.Error.Messages);
                    return;
                }

                job = allResult.Value.FirstOrDefault(candidate => candidate.Id == jobId.Value);
                if (job is null)
                {
                    StatusText = "The selected scheduled job no longer exists.";
                    return;
                }
            }

            ScheduledJobEditorViewModel editor = new(job, targets, useCases);
            UnitResult<IApplicationError> result = await _dialogService.ShowAsync(editor);
            if (result.IsFailure)
            {
                StatusText = string.Join(" | ", result.Error.Messages);
            }

            await RefreshAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not open the job editor: {exception.Message}";
        }
    }

    private async Task DeleteAsync(int jobId)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
        UnitResult<IApplicationError> result = await useCases.DeleteAsync(jobId);
        if (result.IsFailure)
        {
            StatusText = string.Join(" | ", result.Error.Messages);
            return;
        }

        await RefreshAsync();
    }

    private async Task ToggleEnabledAsync(int jobId)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
        ScheduledJobListItemViewModel? row = Items.FirstOrDefault(candidate => candidate.Id == jobId);
        bool isEnabled = row?.IsEnabled == false;
        Result<ScheduledJob, IApplicationError> result = await useCases.SetEnabledAsync(jobId, isEnabled);
        if (result.IsFailure)
        {
            StatusText = string.Join(" | ", result.Error.Messages);
            return;
        }

        await RefreshAsync();
    }

    private async Task RunNowAsync(int jobId)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
        UnitResult<IApplicationError> result = await useCases.RunNowAsync(jobId);
        StatusText = result.IsSuccess
            ? "Job triggered."
            : string.Join(" | ", result.Error.Messages);
    }

    private async Task SelectAsync(int jobId)
    {
        foreach (ScheduledJobListItemViewModel row in Items)
        {
            row.IsSelected = row.Id == jobId;
        }

        ScheduledJobListItemViewModel? selected = Items.FirstOrDefault(row => row.Id == jobId);
        SelectedItem = selected;
        if (selected is null)
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
        Result<List<ScheduledJobRun>, IApplicationError> result = await useCases.GetRunsAsync(jobId);
        if (result.IsFailure)
        {
            StatusText = string.Join(" | ", result.Error.Messages);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            selected.SetRuns(result.Value.Select(run => new ScheduledJobRunViewModel(run))));
    }

    private async Task PopulateNextFireTimesAsync(IReadOnlyList<ScheduledJobListItemViewModel> rows)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ScheduledJobUseCases useCases = scope.ServiceProvider.GetRequiredService<ScheduledJobUseCases>();
        foreach (ScheduledJobListItemViewModel row in rows)
        {
            DateTimeOffset? nextFire = await useCases.GetNextFireTimeAsync(row.Id);
            await Dispatcher.UIThread.InvokeAsync(() => row.NextFireTime = nextFire);
        }
    }
}
