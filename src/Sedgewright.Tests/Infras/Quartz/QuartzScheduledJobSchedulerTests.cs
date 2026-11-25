using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Quartz;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Quartz;

[TestFixture]
public sealed class QuartzScheduledJobSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 0, 31, 0, TimeSpan.Zero);

    [Test]
    public void BuildTrigger_Interval_StartsAtNextSlotAlignedToAnchor()
    {
        ScheduledJob job = CreateJob(job =>
        {
            job.IntervalSeconds = 15 * 60;
            job.StartAt = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        });

        ITrigger? trigger = job.BuildTrigger(Now).Value;

        Assert.That(trigger!.StartTimeUtc, Is.EqualTo(new DateTimeOffset(2026, 11, 25, 0, 45, 0, TimeSpan.Zero)));
    }

    [Test]
    public void BuildTrigger_Cron_EvaluatesInConfiguredTimeZone()
    {
        ScheduledJob job = CreateJob(job =>
        {
            job.ScheduleKind = ScheduleKind.Cron;
            job.CronExpression = "0 0 9 * * ?";
            job.TimeZoneId = "Asia/Ho_Chi_Minh";
        });

        ITrigger? trigger = job.BuildTrigger(Now).Value;

        Assert.That(
            trigger!.GetFireTimeAfter(Now),
            Is.EqualTo(new DateTimeOffset(2026, 11, 25, 2, 0, 0, TimeSpan.Zero)));
    }

    [TestCase(ScheduleKind.Once)]
    [TestCase(ScheduleKind.Interval)]
    [TestCase(ScheduleKind.Cron)]
    public void BuildTrigger_ReturnsNull_WhenScheduleCannotFireAgain(ScheduleKind kind)
    {
        ScheduledJob job = CreateJob(job =>
        {
            job.ScheduleKind = kind;
            job.CronExpression = "0 0 9 * * ?";
            job.RunAt = Now.AddMinutes(-1);
            job.EndAt = kind == ScheduleKind.Once ? null : Now.AddSeconds(-1);
        });

        Result<ITrigger?, IApplicationError> result = job.BuildTrigger(Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Null);
        }
    }

    [TestCase("not a cron", null)]
    [TestCase("0 0 9 * * ?", "Mars/Olympus_Mons")]
    public async Task ValidateSchedule_RejectsInvalidCronOrTimeZone(string cron, string? timeZoneId)
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        IScheduledJobScheduler scheduler = host.Services.GetRequiredService<IScheduledJobScheduler>();
        ScheduledJob job = CreateJob(job =>
        {
            job.ScheduleKind = ScheduleKind.Cron;
            job.CronExpression = cron;
            job.TimeZoneId = timeZoneId;
        });

        UnitResult<IApplicationError> result = scheduler.ValidateSchedule(job);

        Assert.That(result.Error, Is.InstanceOf<ValidationError>());
    }

    [Test]
    public async Task ScheduleAsync_UsesStableKeys_AndReplacesExistingTrigger()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        IScheduledJobScheduler scheduledJobScheduler = host.Services.GetRequiredService<IScheduledJobScheduler>();
        IScheduler scheduler = await host.GetSchedulerAsync();
        ScheduledJob job = CreateJob(job => job.Id = 7);

        await scheduledJobScheduler.ScheduleAsync(job);
        job.IntervalSeconds = 120;
        Result<DateTimeOffset?, IApplicationError> rescheduled = await scheduledJobScheduler.ScheduleAsync(job);

        IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(new JobKey("scheduled-job-7", QuartzScheduledJobScheduler.Group));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rescheduled.Value, Is.Not.Null);
            Assert.That(triggers.Select(trigger => trigger.Key), Is.EqualTo(new[] { QuartzScheduledJobScheduler.GetTriggerKey(7) }));
            Assert.That(((ISimpleTrigger)triggers.Single()).RepeatInterval, Is.EqualTo(TimeSpan.FromSeconds(120)));
        }
    }

    [Test]
    public async Task ReconcileAsync_SchedulesEnabledJobs_AndRemovesOrphans()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        IScheduledJobScheduler scheduledJobScheduler = host.Services.GetRequiredService<IScheduledJobScheduler>();
        IScheduler scheduler = await host.GetSchedulerAsync();
        await scheduledJobScheduler.ScheduleAsync(CreateJob(job => job.Id = 1));
        await scheduledJobScheduler.ScheduleAsync(CreateJob(job => job.Id = 2));

        UnitResult<IApplicationError> result = await scheduledJobScheduler.ReconcileAsync([CreateJob(job => job.Id = 2), CreateJob(job => job.Id = 3)]);

        IReadOnlyCollection<JobKey> keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(QuartzScheduledJobScheduler.Group));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(keys, Is.EquivalentTo(new[] { QuartzScheduledJobScheduler.GetJobKey(2), QuartzScheduledJobScheduler.GetJobKey(3) }));
        }
    }

    private static ScheduledJob CreateJob(Action<ScheduledJob>? configure = null)
    {
        ScheduledJob job = new()
        {
            Id = 1,
            Name = "Job",
            Prompt = "Prompt",
            TargetType = ChatTargetType.Agent,
            TargetId = 1,
            ScheduleKind = ScheduleKind.Interval,
            IntervalSeconds = 300,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        configure?.Invoke(job);
        return job;
    }
}
