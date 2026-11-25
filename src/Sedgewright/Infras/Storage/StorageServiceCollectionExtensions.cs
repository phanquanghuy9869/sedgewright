using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Infras.Services;

namespace Sedgewright.Infras.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddSedgewrightStorage(this IServiceCollection services)
        => AddSedgewrightStorage(services, StoragePathResolver.GetDefaultDatabasePath());

    public static IServiceCollection AddSedgewrightStorage(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContext<SedgewrightDbContext>(options =>
        {
            options.UseSqlite($"Data Source={databasePath}");
        });
        // Repositories are scoped so each Quartz fire (which opens its own DI scope) gets a fresh
        // SedgewrightDbContext. UI singletons still resolve them from the root provider.
        services.AddScoped<LLMProviderRepository>();
        services.AddScoped<IApplicationSettingsRepository, ApplicationSettingsRepository>();
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();
        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();
        services.AddScoped<IMcpDefinitionRepository, McpDefinitionRepository>();
        services.AddScoped<ISequentialOrchestrationRepository, SequentialOrchestrationRepository>();
        services.AddScoped<IConcurrentOrchestrationRepository, ConcurrentOrchestrationRepository>();
        services.AddScoped<IGroupChatOrchestrationRepository, GroupChatOrchestrationRepository>();
        services.AddScoped<IMagenticOrchestrationRepository, MagenticOrchestrationRepository>();
        services.AddScoped<IHandoffOrchestrationRepository, HandoffOrchestrationRepository>();
        services.AddScoped<IToolDefinitionRepository, ToolDefinitionRepository>();
        services.AddScoped<IOrchestrationQueryRepository, OrchestrationQueryRepository>();
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
        services.AddSingleton<ISharedState, SharedState>();
        services.AddSingleton<IAgentChatExecutionStrategy, AgentChatExecutionStrategy>();
        services.AddSingleton<AgentChatUseCases>();
        services.AddSingleton<IChatService, ChatService>();
        return services;
    }
}
