using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;
using Sedgewright.Tests.Infras.Quartz;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class ScheduledJobRepositoryTests
{
    [Test]
    public async Task GetRunsAsync_ReturnsNewestRunsFirst_AndHonoursTake()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync();
        DateTime start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
        await host.InScopeAsync(async services =>
        {
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            for (int index = 0; index < 3; index++)
            {
                repository.CreateRun(new ScheduledJobRun
                {
                    ScheduledJobId = job.Id,
                    StartedAtUtc = start.AddHours(index),
                    Status = ScheduledJobRunStatus.Succeeded,
                });
            }

            return await repository.SaveChangesAsync();
        });

        List<ScheduledJobRun> runs = await host.InScopeAsync(services =>
            services.GetRequiredService<IScheduledJobRepository>().GetRunsAsync(job.Id, take: 2));

        Assert.That(runs.Select(run => run.StartedAtUtc), Is.EqualTo(new[] { start.AddHours(2), start.AddHours(1) }));
    }

    [Test]
    public async Task GetEnabledAsync_ExcludesDisabledJobs()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob enabled = await host.InsertJobAsync();
        await host.InsertJobAsync(job => job.IsEnabled = false);

        List<ScheduledJob> jobs = await host.InScopeAsync(services =>
            services.GetRequiredService<IScheduledJobRepository>().GetEnabledAsync());

        Assert.That(jobs.Select(job => job.Id), Is.EqualTo(new[] { enabled.Id }));
    }

    [Test]
    public async Task MarkRunningRunsAbandonedAsync_OnlyUpdatesRunningRuns()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync();
        await host.InScopeAsync(async services =>
        {
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            repository.CreateRun(new ScheduledJobRun { ScheduledJobId = job.Id, StartedAtUtc = DateTime.UtcNow, Status = ScheduledJobRunStatus.Running });
            repository.CreateRun(new ScheduledJobRun { ScheduledJobId = job.Id, StartedAtUtc = DateTime.UtcNow, Status = ScheduledJobRunStatus.Succeeded });
            return await repository.SaveChangesAsync();
        });

        int updated = await host.InScopeAsync(services =>
            services.GetRequiredService<IScheduledJobRepository>().MarkRunningRunsAbandonedAsync());

        List<ScheduledJobRunStatus> statuses = await host.InScopeAsync(services =>
            services.GetRequiredService<SedgewrightDbContext>().ScheduledJobRuns.Select(run => run.Status).ToListAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated, Is.EqualTo(1));
            Assert.That(statuses, Is.EquivalentTo(new[] { ScheduledJobRunStatus.Abandoned, ScheduledJobRunStatus.Succeeded }));
        }
    }

    [Test]
    public async Task Delete_RemovesRunHistory()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJob job = await host.InsertJobAsync();
        await host.InScopeAsync(async services =>
        {
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            repository.CreateRun(new ScheduledJobRun { ScheduledJobId = job.Id, StartedAtUtc = DateTime.UtcNow });
            await repository.SaveChangesAsync();
            repository.Delete((await repository.GetAsync(job.Id))!);
            return await repository.SaveChangesAsync();
        });

        int remainingRuns = await host.InScopeAsync(services =>
            services.GetRequiredService<SedgewrightDbContext>().ScheduledJobRuns.CountAsync());

        Assert.That(remainingRuns, Is.Zero);
    }
}
