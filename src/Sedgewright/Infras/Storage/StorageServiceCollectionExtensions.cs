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
        services.AddSingleton<LLMProviderRepository>();
        services.AddSingleton<IApplicationSettingsRepository, ApplicationSettingsRepository>();
        services.AddSingleton<IAgentDefinitionRepository, AgentDefinitionRepository>();
        services.AddSingleton<IAgentSessionRepository, AgentSessionRepository>();
        services.AddSingleton<IMcpDefinitionRepository, McpDefinitionRepository>();
        services.AddSingleton<ISequentialOrchestrationRepository, SequentialOrchestrationRepository>();
        services.AddSingleton<IConcurrentOrchestrationRepository, ConcurrentOrchestrationRepository>();
        services.AddSingleton<IGroupChatOrchestrationRepository, GroupChatOrchestrationRepository>();
        services.AddSingleton<IMagenticOrchestrationRepository, MagenticOrchestrationRepository>();
        services.AddSingleton<IHandoffOrchestrationRepository, HandoffOrchestrationRepository>();
        services.AddSingleton<IToolDefinitionRepository, ToolDefinitionRepository>();
        services.AddSingleton<IOrchestrationQueryRepository, OrchestrationQueryRepository>();
        services.AddSingleton<ISharedState, SharedState>();
        services.AddSingleton<IAgentChatExecutionStrategy, AgentChatExecutionStrategy>();
        services.AddSingleton<AgentChatUseCases>();
        services.AddSingleton<IChatService, ChatService>();
        return services;
    }
}
