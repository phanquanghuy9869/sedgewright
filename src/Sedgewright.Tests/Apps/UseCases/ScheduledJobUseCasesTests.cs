using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Infras.Quartz;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;
using Sedgewright.Tests.Infras.Quartz;

namespace Sedgewright.Tests.Apps.UseCases;

[TestFixture]
public sealed class ScheduledJobUseCasesTests
{
    [TestCase(ScheduleKind.Interval, 30, null, null, TestName = "Interval below minimum")]
    [TestCase(ScheduleKind.Cron, null, "every day", null, TestName = "Invalid cron")]
    [TestCase(ScheduleKind.Once, null, null, -5, TestName = "One-time in the past")]
    public async Task CreateAsync_RejectsInvalidSchedules_WithoutPersisting(
        ScheduleKind kind,
        int? intervalSeconds,
        string? cron,
        int? runAtOffsetMinutes)
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();
        ScheduledJob draft = CreateDraft(agentId, draft =>
        {
            draft.ScheduleKind = kind;
            draft.IntervalSeconds = intervalSeconds;
            draft.CronExpression = cron;
            draft.RunAt = runAtOffsetMinutes is int offset ? DateTimeOffset.UtcNow.AddMinutes(offset) : null;
        });

        Result<ScheduledJob, IApplicationError> result = await CreateAsync(host, draft);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Error, Is.InstanceOf<ValidationError>());
            Assert.That(await CountJobsAsync(host), Is.Zero);
        }
    }

    [Test]
    public async Task CreateAsync_RejectsMissingTarget()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();

        Result<ScheduledJob, IApplicationError> result = await CreateAsync(host, CreateDraft(targetId: 404));

        Assert.That(result.Error.Messages, Has.Some.Contains("does not exist"));
    }

    [Test]
    public async Task CreateDisableDelete_KeepsSchedulerInSyncWithDatabase()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();
        IScheduler scheduler = await host.GetSchedulerAsync();

        ScheduledJob created = (await CreateAsync(host, CreateDraft(agentId))).Value;
        bool scheduledAfterCreate = await IsScheduledAsync(scheduler, created.Id);

        await host.InScopeAsync(services => services.GetRequiredService<ScheduledJobUseCases>().SetEnabledAsync(created.Id, false));
        bool scheduledAfterDisable = await IsScheduledAsync(scheduler, created.Id);

        await host.InScopeAsync(services => services.GetRequiredService<ScheduledJobUseCases>().SetEnabledAsync(created.Id, true));
        bool scheduledAfterEnable = await IsScheduledAsync(scheduler, created.Id);

        UnitResult<IApplicationError> deleted = await host.InScopeAsync(services =>
            services.GetRequiredService<ScheduledJobUseCases>().DeleteAsync(created.Id));
        bool scheduledAfterDelete = await IsScheduledAsync(scheduler, created.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scheduledAfterCreate, Is.True);
            Assert.That(scheduledAfterDisable, Is.False);
            Assert.That(scheduledAfterEnable, Is.True);
            Assert.That(deleted.IsSuccess, Is.True);
            Assert.That(scheduledAfterDelete, Is.False);
            Assert.That(await CountJobsAsync(host), Is.Zero);
        }
    }

    [Test]
    public async Task ReconcileAsync_SchedulesEnabledJobs_AndAbandonsInterruptedRuns()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob enabled = await host.InsertJobAsync();
        ScheduledJob disabled = await host.InsertJobAsync(job => job.IsEnabled = false);
        await host.InScopeAsync(async services =>
        {
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            repository.CreateRun(new ScheduledJobRun { ScheduledJobId = enabled.Id, StartedAt = DateTimeOffset.UtcNow });
            return await repository.SaveChangesAsync();
        });
        IScheduler scheduler = await host.GetSchedulerAsync();

        UnitResult<IApplicationError> result = await host.InScopeAsync(services =>
            services.GetRequiredService<ScheduledJobUseCases>().ReconcileAsync());

        ScheduledJobRunStatus runStatus = await host.InScopeAsync(services =>
            services.GetRequiredService<SedgewrightDbContext>().ScheduledJobRuns.Select(run => run.Status).SingleAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(await IsScheduledAsync(scheduler, enabled.Id), Is.True);
            Assert.That(await IsScheduledAsync(scheduler, disabled.Id), Is.False);
            Assert.That(runStatus, Is.EqualTo(ScheduledJobRunStatus.Abandoned));
        }
    }

    [Test]
    public async Task RunAsync_WhenTargetIsMissing_RecordsFailure_DisablesAndUnschedulesJob()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync();
        await host.Services.GetRequiredService<IScheduledJobScheduler>().ScheduleAsync(job);
        IScheduler scheduler = await host.GetSchedulerAsync();

        Result<ScheduledJobRun?, IApplicationError> result = await host.InScopeAsync(services =>
            services.GetRequiredService<IScheduledJobRunner>().RunAsync(job.Id));

        ScheduledJob stored = await host.InScopeAsync(services =>
            services.GetRequiredService<SedgewrightDbContext>().ScheduledJobs.AsNoTracking().SingleAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value!.Status, Is.EqualTo(ScheduledJobRunStatus.Failed));
            Assert.That(result.Value.CompletedAt, Is.Not.Null);
            Assert.That(stored.IsEnabled, Is.False);
            Assert.That(stored.LastRunStatus, Is.EqualTo(ScheduledJobRunStatus.Failed));
            Assert.That(await IsScheduledAsync(scheduler, job.Id), Is.False);
        }
    }

    [Test]
    public async Task RunAsync_WhenJobIsDisabled_DoesNotRecordRun()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync(job => job.IsEnabled = false);

        Result<ScheduledJobRun?, IApplicationError> result = await host.InScopeAsync(services =>
            services.GetRequiredService<IScheduledJobRunner>().RunAsync(job.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Null);
            Assert.That(await host.InScopeAsync(services =>
                services.GetRequiredService<SedgewrightDbContext>().ScheduledJobRuns.CountAsync()), Is.Zero);
        }
    }

    [Test]
    public async Task RunNowAsync_FiresQuartzJob_ThatRecordsRunThroughScopedServices()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync();
        IScheduler scheduler = await host.GetSchedulerAsync();
        await scheduler.Start();

        UnitResult<IApplicationError> triggered = await host.InScopeAsync(services =>
            services.GetRequiredService<ScheduledJobUseCases>().RunNowAsync(job.Id));

        ScheduledJobRun? run = null;
        for (int attempt = 0; attempt < 100 && run?.Status is null or ScheduledJobRunStatus.Running; attempt++)
        {
            await Task.Delay(100);
            run = await host.InScopeAsync(services =>
                services.GetRequiredService<SedgewrightDbContext>().ScheduledJobRuns.AsNoTracking().SingleOrDefaultAsync());
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(triggered.IsSuccess, Is.True);
            Assert.That(run?.Status, Is.EqualTo(ScheduledJobRunStatus.Failed));
            Assert.That(run?.Error, Does.Contain("no longer exists"));
        }
    }

    private static async Task<bool> IsScheduledAsync(IScheduler scheduler, int scheduledJobId)
        => await scheduler.GetJobDetail(QuartzScheduledJobScheduler.GetJobKey(scheduledJobId)) is not null;

    private static Task<Result<ScheduledJob, IApplicationError>> CreateAsync(SchedulingTestHost host, ScheduledJob draft)
        => host.InScopeAsync(services => services.GetRequiredService<ScheduledJobUseCases>().CreateAsync(draft));

    private static Task<int> CountJobsAsync(SchedulingTestHost host)
        => host.InScopeAsync(services => services.GetRequiredService<SedgewrightDbContext>().ScheduledJobs.CountAsync());

    private static ScheduledJob CreateDraft(int targetId, Action<ScheduledJob>? configure = null)
    {
        ScheduledJob draft = new()
        {
            Name = "Morning briefing",
            Prompt = "Brief me.",
            TargetType = ChatTargetType.Agent,
            TargetId = targetId,
            ScheduleKind = ScheduleKind.Interval,
            IntervalSeconds = 3600,
        };
        configure?.Invoke(draft);
        return draft;
    }
}
