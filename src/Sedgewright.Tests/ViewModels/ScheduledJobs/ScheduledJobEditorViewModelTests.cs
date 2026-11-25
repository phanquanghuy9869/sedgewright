using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.ScheduledJobs;
using Sedgewright.Tests.Infras.Quartz;

namespace Sedgewright.Tests.ViewModels.ScheduledJobs;

[TestFixture]
public sealed class ScheduledJobEditorViewModelTests
{
    [Test]
    public async Task NewEditor_DefaultsToIntervalAndRequiresTarget()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();

        ScheduledJobEditorViewModel viewModel = await CreateEditorAsync(host, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SelectedScheduleKind?.Kind, Is.EqualTo(ScheduleKind.Interval));
            Assert.That(viewModel.IsIntervalSchedule, Is.True);
            Assert.That(viewModel.IsCronSchedule, Is.False);
            Assert.That(viewModel.IsOnceSchedule, Is.False);
            Assert.That(viewModel.SaveCommand.CanExecute(null), Is.False);
        }
    }

    [Test]
    public async Task ChangingScheduleKind_ShowsOnlyMatchingFields()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        ScheduledJobEditorViewModel viewModel = await CreateEditorAsync(host, null);

        viewModel.SelectedScheduleKind = viewModel.ScheduleKindOptions[1];
        bool cronVisible = viewModel.IsCronSchedule;
        viewModel.SelectedScheduleKind = viewModel.ScheduleKindOptions[2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cronVisible, Is.True);
            Assert.That(viewModel.IsCronSchedule, Is.False);
            Assert.That(viewModel.IsOnceSchedule, Is.True);
        }
    }

    [Test]
    public async Task Edit_ResolvesTargetAndIntervalSettings()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();
        ScheduledJob job = await host.InsertJobAsync(job =>
        {
            job.Name = "Existing report";
            job.Description = "Existing description";
            job.Prompt = "Summarize it.";
            job.TargetType = ChatTargetType.Agent;
            job.TargetId = agentId;
            job.ScheduleKind = ScheduleKind.Interval;
            job.IntervalSeconds = 15 * 60;
        });

        ScheduledJobEditorViewModel viewModel = await CreateEditorAsync(host, job);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Name, Is.EqualTo("Existing report"));
            Assert.That(viewModel.Description, Is.EqualTo("Existing description"));
            Assert.That(viewModel.Prompt, Is.EqualTo("Summarize it."));
            Assert.That(viewModel.IntervalMinutes, Is.EqualTo("15"));
            Assert.That(viewModel.SelectedTarget?.Identity,
                Is.EqualTo(new ChatTargetIdentity(ChatTargetType.Agent, agentId)));
        }
    }

    [Test]
    public async Task Save_Interval_PersistsJobAndClosesSuccessfully()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();
        List<ScheduledJob> jobs = await host.InScopeAsync(async services =>
        {
            ScheduledJobUseCases useCases = services.GetRequiredService<ScheduledJobUseCases>();
            ScheduledJobEditorViewModel viewModel = CreateEditor(
                null,
                [CreateAgent(agentId)],
                useCases);
            viewModel.Name = "15-minute report";
            viewModel.Description = "  Daily summary  ";
            viewModel.Prompt = "Summarize activity.";
            viewModel.IntervalMinutes = "15";
            viewModel.SelectedTarget = viewModel.TargetOptions[0];

            Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
            viewModel.SaveCommand.Execute(null);
            UnitResult<IApplicationError> result = await closeTask;

            Assert.That(result.IsSuccess, Is.True);
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            return await repository.GetAllAsync();
        });

        ScheduledJob saved = jobs.Single(job => job.Name == "15-minute report");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Description, Is.EqualTo("Daily summary"));
            Assert.That(saved.TargetType, Is.EqualTo(ChatTargetType.Agent));
            Assert.That(saved.TargetId, Is.EqualTo(agentId));
            Assert.That(saved.ScheduleKind, Is.EqualTo(ScheduleKind.Interval));
            Assert.That(saved.IntervalSeconds, Is.EqualTo(900));
        }
    }

    [Test]
    public async Task Save_Cron_PersistsExpressionAndTimeZone()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();

        await host.InScopeAsync<bool>(async services =>
        {
            ScheduledJobUseCases useCases = services.GetRequiredService<ScheduledJobUseCases>();
            ScheduledJobEditorViewModel viewModel = CreateEditor(
                null,
                [CreateAgent(agentId)],
                useCases);
            viewModel.Name = "Cron report";
            viewModel.Prompt = "Send the report.";
            viewModel.SelectedTarget = viewModel.TargetOptions[0];
            viewModel.SelectedScheduleKind = viewModel.ScheduleKindOptions[1];
            viewModel.CronExpression = "0 0 9 * * ?";
            viewModel.TimeZoneId = "Asia/Ho_Chi_Minh";

            Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
            viewModel.SaveCommand.Execute(null);
            UnitResult<IApplicationError> result = await closeTask;

            Assert.That(result.IsSuccess, Is.True);
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            ScheduledJob saved = (await repository.GetAllAsync()).Single(job => job.Name == "Cron report");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(saved.ScheduleKind, Is.EqualTo(ScheduleKind.Cron));
                Assert.That(saved.CronExpression, Is.EqualTo("0 0 9 * * ?"));
                Assert.That(saved.TimeZoneId, Is.EqualTo("Asia/Ho_Chi_Minh"));
            }

            return true;
        });
    }

    [Test]
    public async Task Save_Once_PersistsUtcRunTime()
    {
        await using SchedulingTestHost host = await SchedulingTestHost.CreateAsync();
        int agentId = await host.SeedAgentAsync();

        await host.InScopeAsync<bool>(async services =>
        {
            ScheduledJobUseCases useCases = services.GetRequiredService<ScheduledJobUseCases>();
            ScheduledJobEditorViewModel viewModel = CreateEditor(
                null,
                [CreateAgent(agentId)],
                useCases);
            viewModel.Name = "Launch report";
            viewModel.Prompt = "Run the launch report.";
            viewModel.SelectedTarget = viewModel.TargetOptions[0];
            viewModel.SelectedScheduleKind = viewModel.ScheduleKindOptions[2];
            viewModel.RunAtText = DateTime.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm");

            Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
            viewModel.SaveCommand.Execute(null);
            UnitResult<IApplicationError> result = await closeTask;

            Assert.That(result.IsSuccess, Is.True);
            IScheduledJobRepository repository = services.GetRequiredService<IScheduledJobRepository>();
            ScheduledJob saved = (await repository.GetAllAsync()).Single(job => job.Name == "Launch report");

            Assert.That(saved.ScheduleKind, Is.EqualTo(ScheduleKind.Once));
            Assert.That(saved.RunAtUtc, Is.Not.Null);
            return true;
        });
    }

    private static async Task<ScheduledJobEditorViewModel> CreateEditorAsync(
        SchedulingTestHost host,
        ScheduledJob? existingJob)
        => await host.InScopeAsync(services =>
        {
            ScheduledJobUseCases useCases = services.GetRequiredService<ScheduledJobUseCases>();
            return Task.FromResult(CreateEditor(
                existingJob,
                [CreateAgent(existingJob?.TargetId ?? 1)],
                useCases));
        });

    private static ScheduledJobEditorViewModel CreateEditor(
        ScheduledJob? existingJob,
        IReadOnlyList<IChatTarget> targets,
        ScheduledJobUseCases useCases)
        => new(existingJob, targets, useCases);

    private static AgentDefinition CreateAgent(int agentId)
        => new()
        {
            Id = agentId,
            Name = "Scheduled Agent",
            Description = "Runs on a schedule",
            Model = "gpt-5",
            Provider = new LLMProvider
            {
                DisplayName = "OpenAI",
            },
        };

    private static async Task<UnitResult<IApplicationError>> WaitForCloseAsync(
        ScheduledJobEditorViewModel viewModel)
    {
        TaskCompletionSource<UnitResult<IApplicationError>> completion = new();
        viewModel.CloseRequested += result => completion.TrySetResult(result);
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
