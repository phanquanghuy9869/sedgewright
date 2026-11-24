using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class ToolDefinitionRepositoryTests
{
    [Test]
    public async Task Create_AssignsVersion7Id()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        ToolDefinitionRepository repository = new(dbContext);
        ToolDefinition definition = new()
        {
            Name = "generated-id-tool",
            Type = ToolDefinitionType.Native,
        };

        Assert.That(definition.Id, Is.EqualTo(Guid.Empty));

        repository.Create(definition);

        Assert.That(definition.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(definition.Id.ToString("N")[12], Is.EqualTo('7'));
        await repository.SaveChangesAsync();
    }

    [Test]
    public async Task GetAsync_LoadsEachOrchestrationNavigation()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);

        LLMProvider provider = CreateProvider();
        AgentDefinition agent = CreateAgent("workflow-agent", provider);
        SequentialOrchestration sequential = new() { Name = "sequential-tool-workflow" };
        ConcurrentOrchestration concurrent = new() { Name = "concurrent-tool-workflow" };
        GroupChatOrchestration groupChat = new() { Name = "group-chat-tool-workflow" };
        MagenticOrchestration magentic = new()
        {
            Name = "magentic-tool-workflow",
            ManagerAgentDefinition = agent,
        };
        HandoffOrchestration handoff = new()
        {
            Name = "handoff-tool-workflow",
            StartAgentDefinition = agent,
        };

        dbContext.AddRange(provider, agent, sequential, concurrent, groupChat, magentic, handoff);
        await dbContext.SaveChangesAsync();

        ToolDefinition sequentialTool = new()
        {
            Name = "sequential-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestrationId = sequential.Id,
        };
        ToolDefinition concurrentTool = new()
        {
            Name = "concurrent-tool",
            Type = ToolDefinitionType.Workflow,
            ConcurrentOrchestrationId = concurrent.Id,
        };
        ToolDefinition groupChatTool = new()
        {
            Name = "group-chat-tool",
            Type = ToolDefinitionType.Workflow,
            GroupChatOrchestrationId = groupChat.Id,
        };
        ToolDefinition magenticTool = new()
        {
            Name = "magentic-tool",
            Type = ToolDefinitionType.Workflow,
            MagenticOrchestrationId = magentic.Id,
        };
        ToolDefinition handoffTool = new()
        {
            Name = "handoff-tool",
            Type = ToolDefinitionType.Workflow,
            HandoffOrchestrationId = handoff.Id,
        };

        ToolDefinitionRepository repository = new(dbContext);
        repository.Create(sequentialTool);
        repository.Create(concurrentTool);
        repository.Create(groupChatTool);
        repository.Create(magenticTool);
        repository.Create(handoffTool);
        await repository.SaveChangesAsync();

        ToolDefinition? loadedSequential = await repository.GetAsync(sequentialTool.Id);
        ToolDefinition? loadedConcurrent = await repository.GetAsync(concurrentTool.Id);
        ToolDefinition? loadedGroupChat = await repository.GetAsync(groupChatTool.Id);
        ToolDefinition? loadedMagentic = await repository.GetAsync(magenticTool.Id);
        ToolDefinition? loadedHandoff = await repository.GetAsync(handoffTool.Id);

        Assert.That(loadedSequential?.SequentialOrchestration?.Name, Is.EqualTo(sequential.Name));
        Assert.That(loadedConcurrent?.ConcurrentOrchestration?.Name, Is.EqualTo(concurrent.Name));
        Assert.That(loadedGroupChat?.GroupChatOrchestration?.Name, Is.EqualTo(groupChat.Name));
        Assert.That(loadedMagentic?.MagenticOrchestration?.Name, Is.EqualTo(magentic.Name));
        Assert.That(loadedHandoff?.HandoffOrchestration?.Name, Is.EqualTo(handoff.Name));
    }

    [Test]
    public async Task GetAllAsync_ReturnsForeignKeyWithoutLoadingOrchestrationNavigation()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        SequentialOrchestration orchestration = new() { Name = "list-tool-workflow" };
        dbContext.Add(orchestration);
        await dbContext.SaveChangesAsync();

        ToolDefinition definition = new()
        {
            Name = "list-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestrationId = orchestration.Id,
        };
        ToolDefinitionRepository repository = new(dbContext);
        repository.Create(definition);
        await repository.SaveChangesAsync();

        ToolDefinition loaded = (await repository.GetAllAsync()).Single(item => item.Id == definition.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.SequentialOrchestrationId, Is.EqualTo(orchestration.Id));
            Assert.That(loaded.SequentialOrchestration, Is.Null);
        }
    }

    [Test]
    public async Task GetAssignedAgentNamesAsync_ReturnsSortedAgentsAssignedToToolDefinition()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);

        LLMProvider provider = CreateProvider();
        AgentDefinition zuluAgent = CreateAgent("Zulu agent", provider);
        AgentDefinition alphaAgent = CreateAgent("Alpha agent", provider);
        ToolDefinition definition = new()
        {
            Name = "assigned-tool",
            Type = ToolDefinitionType.Native,
        };

        dbContext.AddRange(provider, zuluAgent, alphaAgent, definition);
        await dbContext.SaveChangesAsync();

        dbContext.AgentTools.AddRange(
            new AgentTool { AgentDefinition = zuluAgent, ToolDefinition = definition },
            new AgentTool { AgentDefinition = alphaAgent, ToolDefinition = definition });
        await dbContext.SaveChangesAsync();

        ToolDefinitionRepository repository = new(dbContext);
        List<string> assignedAgentNames = await repository.GetAssignedAgentNamesAsync(definition.Id);

        Assert.That(assignedAgentNames, Is.EqualTo(new[] { "Alpha agent", "Zulu agent" }));
    }

    [Test]
    public async Task CreateUpdateDelete_AndMissingId_WorkForToolDefinitions()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        ToolDefinitionRepository repository = new(dbContext);

        ToolDefinition definition = new()
        {
            Name = "native-tool",
            ToolDescription = "Native tool description",
            Type = ToolDefinitionType.Native,
        };

        repository.Create(definition);
        await repository.SaveChangesAsync();

        ToolDefinition? created = await repository.GetAsync(definition.Id);
        Assert.That(created, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created!.Type, Is.EqualTo(ToolDefinitionType.Native));
            Assert.That(created.ToolDescription, Is.EqualTo("Native tool description"));
        }

        created.Name = "renamed-native-tool";
        created.ToolDescription = "Updated native tool description";
        repository.Update(created);
        await repository.SaveChangesAsync();

        ToolDefinition? updated = await repository.GetAsync(definition.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated?.Name, Is.EqualTo("renamed-native-tool"));
            Assert.That(updated?.ToolDescription, Is.EqualTo("Updated native tool description"));
        }

        repository.Delete(updated!);
        await repository.SaveChangesAsync();

        Assert.That(await repository.GetAsync(definition.Id), Is.Null);
        Assert.That(await repository.GetAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task SaveChanges_RejectsWorkflowWithoutExactlyOneOrchestration()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        ToolDefinitionRepository repository = new(dbContext);

        repository.Create(new ToolDefinition
        {
            Name = "unbound-workflow-tool",
            Type = ToolDefinitionType.Workflow,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await repository.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_RejectsNativeToolWithOrchestration()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);

        SequentialOrchestration orchestration = new() { Name = "native-tool-orchestration" };
        dbContext.Add(orchestration);
        await dbContext.SaveChangesAsync();

        ToolDefinitionRepository repository = new(dbContext);
        repository.Create(new ToolDefinition
        {
            Name = "invalid-native-tool",
            Type = ToolDefinitionType.Native,
            SequentialOrchestrationId = orchestration.Id,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await repository.SaveChangesAsync());
    }

    [Test]
    public async Task DeleteOrchestration_WithToolReference_IsRejected()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);

        SequentialOrchestration orchestration = new() { Name = "referenced-tool-orchestration" };
        dbContext.Add(orchestration);
        await dbContext.SaveChangesAsync();

        ToolDefinition definition = new()
        {
            Name = "referencing-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestrationId = orchestration.Id,
        };
        ToolDefinitionRepository repository = new(dbContext);
        repository.Create(definition);
        await repository.SaveChangesAsync();

        dbContext.Remove(orchestration);

        Assert.ThrowsAsync<DbUpdateException>(async () => await dbContext.SaveChangesAsync());
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<SedgewrightDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static LLMProvider CreateProvider()
        => new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

    private static AgentDefinition CreateAgent(string name, LLMProvider provider)
        => new()
        {
            Name = name,
            Description = $"{name} description",
            Model = "gpt-5",
            Provider = provider,
        };
}
