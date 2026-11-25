using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Infras.Quartz;
using Sedgewright.Infras.Services;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Quartz;

/// <summary>
/// Builds the real storage and scheduling container against a migrated temporary SQLite file.
/// </summary>
internal sealed class SchedulingTestHost : IAsyncDisposable
{
    private readonly string _databasePath;

    private SchedulingTestHost(ServiceProvider services, string databasePath)
    {
        Services = services;
        _databasePath = databasePath;
    }

    public ServiceProvider Services { get; }

    public static async Task<SchedulingTestHost> CreateAsync()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"sedgewright-scheduling-{Guid.NewGuid():N}.db");
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSedgewrightStorage(databasePath);
        services.AddSedgewrightScheduling();
        services.AddSingleton<IWorkflowExecutionStrategyFactory, WorkflowExecutionStrategyFactory>();
        services.AddSingleton<OrchestrationUseCases>();
        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SedgewrightDbContext>().Database.MigrateAsync();
        }

        return new SchedulingTestHost(provider, databasePath);
    }

    public async Task<IScheduler> GetSchedulerAsync()
        => await Services.GetRequiredService<ISchedulerFactory>().GetScheduler();

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task<int> SeedAgentAsync()
        => InScopeAsync(async services =>
        {
            SedgewrightDbContext dbContext = services.GetRequiredService<SedgewrightDbContext>();
            LLMProvider provider = new()
            {
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                BaseUrl = "https://example.invalid",
                ApiKey = "test-key",
            };
            AgentDefinition agent = new()
            {
                Name = "Scheduled Agent",
                Description = "Runs on a schedule",
                Model = "gpt-5",
                Provider = provider,
            };
            dbContext.AddRange(provider, agent);
            await dbContext.SaveChangesAsync();
            return agent.Id;
        });

    public Task<ScheduledJob> InsertJobAsync(Action<ScheduledJob>? configure = null)
        => InScopeAsync(async services =>
        {
            SedgewrightDbContext dbContext = services.GetRequiredService<SedgewrightDbContext>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ScheduledJob job = new()
            {
                Name = "Nightly report",
                Prompt = "Summarize yesterday.",
                TargetType = ChatTargetType.Agent,
                TargetId = 999,
                ScheduleKind = ScheduleKind.Interval,
                IntervalSeconds = 3600,
                CreatedAt = now,
                UpdatedAt = now,
            };
            configure?.Invoke(job);
            dbContext.ScheduledJobs.Add(job);
            await dbContext.SaveChangesAsync();
            return job;
        });

    public async ValueTask DisposeAsync()
    {
        IScheduler scheduler = await GetSchedulerAsync();
        await scheduler.Shutdown(waitForJobsToComplete: true);
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
