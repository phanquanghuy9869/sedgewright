using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class SequentialOrchestrationRepositoryTests
{
    [Test]
    public async Task GetAsync_LoadsSequentialStepsAndAgentDefinitions()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition firstAgent = CreateAgent("first-agent", provider);
        AgentDefinition secondAgent = CreateAgent("second-agent", provider);

        SequentialOrchestration orchestration = new()
        {
            Name = "seq-load-test",
            Description = "Sequential load test",
            Steps =
            [
                new SequentialOrchestrationAgent
                {
                    StepOrder = 2,
                    AgentDefinition = secondAgent,
                },
                new SequentialOrchestrationAgent
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
            ],
        };

        dbContext.AddRange(provider, firstAgent, secondAgent, orchestration);
        await dbContext.SaveChangesAsync();

        SequentialOrchestrationRepository repository = new(dbContext);

        SequentialOrchestration? loaded = await repository.GetAsync(orchestration.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.ChainOnlyAgentResponses, Is.False);
        Assert.That(loaded!.Steps, Has.Count.EqualTo(2));
        Assert.That(loaded.Steps.OrderBy(step => step.StepOrder).Select(step => step.AgentDefinition.Name), Is.EqualTo(["first-agent", "second-agent"]));
    }

    [Test]
    public async Task CreateUpdateDelete_AndNameExistsAsync_WorkForSequentialOrchestrations()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition agent = CreateAgent("agent-one", provider);

        dbContext.AddRange(provider, agent);
        await dbContext.SaveChangesAsync();

        SequentialOrchestrationRepository repository = new(dbContext);

        SequentialOrchestration orchestration = new()
        {
            Name = "seq-crud-test",
            Description = "Initial description",
            ChainOnlyAgentResponses = true,
            Steps =
            [
                new SequentialOrchestrationAgent
                {
                    StepOrder = 1,
                    AgentDefinition = agent,
                },
            ],
        };

        repository.Create(orchestration);
        await repository.SaveChangesAsync();

        Assert.That(await repository.NameExistsAsync("seq-crud-test"), Is.True);
        Assert.That(await repository.NameExistsAsync("seq-crud-test", orchestration.Id), Is.False);

        orchestration.Description = "Updated description";
        repository.Update(orchestration);
        await repository.SaveChangesAsync();

        SequentialOrchestration? updated = await repository.GetAsync(orchestration.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Description, Is.EqualTo("Updated description"));
        Assert.That(updated.ChainOnlyAgentResponses, Is.True);

        repository.Delete(updated);
        await repository.SaveChangesAsync();

        SequentialOrchestration? deleted = await repository.GetAsync(orchestration.Id);
        Assert.That(deleted, Is.Null);
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

[TestFixture]
public sealed class OrchestrationRuntimeGraphRepositoryTests
{
    [Test]
    public async Task GetAsync_LoadsRuntimeAgentNavigationProperties_ForEveryOrchestration()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition firstAgent = CreateAgent("first-runtime-agent", provider);
        AgentDefinition secondAgent = CreateAgent("second-runtime-agent", provider);
        McpDefinition mcpDefinition = new()
        {
            Name = "runtime-mcp",
            Description = "Runtime MCP definition",
            IsReadOnly = true,
            IsStdio = false,
            ConfigJson = "{}",
        };
        ToolDefinition toolDefinition = new()
        {
            Name = "runtime-tool",
            Type = ToolDefinitionType.Native,
        };

        SequentialOrchestration sequential = new()
        {
            Name = "runtime-sequential",
            Steps =
            [
                new SequentialOrchestrationAgent
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
                new SequentialOrchestrationAgent
                {
                    StepOrder = 2,
                    AgentDefinition = secondAgent,
                },
            ],
        };
        ConcurrentOrchestration concurrent = new()
        {
            Name = "runtime-concurrent",
            Participants =
            [
                new ConcurrentOrchestrationParticipant { AgentDefinition = firstAgent },
                new ConcurrentOrchestrationParticipant { AgentDefinition = secondAgent },
            ],
        };
        GroupChatOrchestration groupChat = new()
        {
            Name = "runtime-group-chat",
            Participants =
            [
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 2,
                    AgentDefinition = secondAgent,
                },
            ],
        };
        MagenticOrchestration magentic = new()
        {
            Name = "runtime-magentic",
            ManagerAgentDefinition = firstAgent,
            Participants =
            [
                new MagenticOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = secondAgent,
                },
            ],
        };
        HandoffOrchestration handoff = new()
        {
            Name = "runtime-handoff",
            StartAgentDefinition = firstAgent,
            Participants =
            [
                new HandoffOrchestrationParticipant { AgentDefinition = firstAgent },
                new HandoffOrchestrationParticipant { AgentDefinition = secondAgent },
            ],
            Routes =
            [
                new HandoffOrchestrationRoute
                {
                    SourceAgentDefinition = firstAgent,
                    TargetAgentDefinition = secondAgent,
                },
            ],
        };

        dbContext.AddRange(provider, firstAgent, secondAgent, mcpDefinition, toolDefinition, sequential, concurrent, groupChat, magentic, handoff);
        await dbContext.SaveChangesAsync();

        dbContext.AddRange(
            new AgentTool
            {
                AgentDefinition = firstAgent,
                ToolDefinition = toolDefinition,
            },
            new AgentTool
            {
                AgentDefinition = secondAgent,
                ToolDefinition = toolDefinition,
            },
            new AgentMCPAttachment
            {
                AgentDefinition = firstAgent,
                McpDefinition = mcpDefinition,
            },
            new AgentMCPAttachment
            {
                AgentDefinition = secondAgent,
                McpDefinition = mcpDefinition,
            },
            new AgentSkillDefinition
            {
                AgentDefinition = firstAgent,
                SkillDirectory = ".agents/skills/first",
                SelectedSkills = ["first-skill"],
            },
            new AgentSkillDefinition
            {
                AgentDefinition = secondAgent,
                SkillDirectory = ".agents/skills/second",
                SelectedSkills = ["second-skill"],
            });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        SequentialOrchestration? loadedSequential = await new SequentialOrchestrationRepository(dbContext).GetAsync(sequential.Id);
        ConcurrentOrchestration? loadedConcurrent = await new ConcurrentOrchestrationRepository(dbContext).GetAsync(concurrent.Id);
        GroupChatOrchestration? loadedGroupChat = await new GroupChatOrchestrationRepository(dbContext).GetAsync(groupChat.Id);
        MagenticOrchestration? loadedMagentic = await new MagenticOrchestrationRepository(dbContext).GetAsync(magentic.Id);
        HandoffOrchestration? loadedHandoff = await new HandoffOrchestrationRepository(dbContext).GetAsync(handoff.Id);

        AssertRuntimeGraph(loadedSequential!.Steps[0].AgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedSequential.Steps[1].AgentDefinition, "second-runtime-agent", "second-skill");
        AssertRuntimeGraph(loadedConcurrent!.Participants[0].AgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedConcurrent.Participants[1].AgentDefinition, "second-runtime-agent", "second-skill");
        AssertRuntimeGraph(loadedGroupChat!.Participants[0].AgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedGroupChat.Participants[1].AgentDefinition, "second-runtime-agent", "second-skill");
        AssertRuntimeGraph(loadedMagentic!.ManagerAgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedMagentic.Participants[0].AgentDefinition, "second-runtime-agent", "second-skill");
        AssertRuntimeGraph(loadedHandoff!.StartAgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedHandoff.Participants[0].AgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedHandoff.Participants[1].AgentDefinition, "second-runtime-agent", "second-skill");
        AssertRuntimeGraph(loadedHandoff.Routes[0].SourceAgentDefinition, "first-runtime-agent", "first-skill");
        AssertRuntimeGraph(loadedHandoff.Routes[0].TargetAgentDefinition, "second-runtime-agent", "second-skill");
    }

    private static void AssertRuntimeGraph(AgentDefinition agent, string expectedName, string expectedSkill)
    {
        Assert.That(agent.Name, Is.EqualTo(expectedName));
        Assert.That(agent.Provider.DisplayName, Is.EqualTo("OpenAI"));
        Assert.That(agent.Tools, Has.Count.EqualTo(1));
        Assert.That(agent.Tools[0].ToolDefinition.Name, Is.EqualTo("runtime-tool"));
        Assert.That(agent.McpAttachments, Has.Count.EqualTo(1));
        Assert.That(agent.McpAttachments[0].McpDefinition.Name, Is.EqualTo("runtime-mcp"));
        Assert.That(agent.Skills, Has.Count.EqualTo(1));
        Assert.That(agent.Skills[0].SelectedSkills, Is.EqualTo([expectedSkill]));
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

[TestFixture]
public sealed class OrchestrationQueryRepositoryTests
{
    [Test]
    public async Task GetAllAsync_AggregatesAllOrchestrationTables()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition alpha = CreateAgent("alpha-agent", provider);
        AgentDefinition beta = CreateAgent("beta-agent", provider);
        AgentDefinition gamma = CreateAgent("gamma-agent", provider);

        SequentialOrchestration sequential = new()
        {
            Name = "seq-test",
            Description = "Sequential description",
            Steps =
            [
                new SequentialOrchestrationAgent
                {
                    StepOrder = 1,
                    AgentDefinition = alpha,
                },
            ],
        };

        ConcurrentOrchestration concurrent = new()
        {
            Name = "concurrent-test",
            Description = "Concurrent description",
            Participants =
            [
                new ConcurrentOrchestrationParticipant
                {
                    AgentDefinition = alpha,
                },
                new ConcurrentOrchestrationParticipant
                {
                    AgentDefinition = beta,
                },
            ],
        };

        GroupChatOrchestration groupChat = new()
        {
            Name = "group-chat-test",
            Description = "Group chat description",
            MaximumIterationCount = 7,
            Participants =
            [
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = alpha,
                },
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 2,
                    AgentDefinition = beta,
                },
            ],
        };

        MagenticOrchestration magentic = new()
        {
            Name = "magentic-test",
            Description = "Magentic description",
            ManagerAgentDefinition = gamma,
            MaximumRoundCount = 4,
            Participants =
            [
                new MagenticOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = alpha,
                },
            ],
        };

        HandoffOrchestration handoff = new()
        {
            Name = "handoff-test",
            Description = "Handoff description",
            StartAgentDefinition = alpha,
            IsAutonomousModeEnabled = false,
            Participants =
            [
                new HandoffOrchestrationParticipant
                {
                    AgentDefinition = alpha,
                },
                new HandoffOrchestrationParticipant
                {
                    AgentDefinition = beta,
                },
            ],
            Routes =
            [
                new HandoffOrchestrationRoute
                {
                    SourceAgentDefinition = alpha,
                    TargetAgentDefinition = beta,
                },
            ],
        };

        dbContext.AddRange(provider, alpha, beta, gamma, sequential, concurrent, groupChat, magentic, handoff);
        await dbContext.SaveChangesAsync();

        OrchestrationQueryRepository repository = new(
            new SequentialOrchestrationRepository(dbContext),
            new ConcurrentOrchestrationRepository(dbContext),
            new GroupChatOrchestrationRepository(dbContext),
            new MagenticOrchestrationRepository(dbContext),
            new HandoffOrchestrationRepository(dbContext));

        List<OrchestrationListItemSnapshot> items = await repository.GetAllAsync();

        Assert.That(items, Has.Count.EqualTo(5));
        Assert.That(items.Select(item => item.Kind), Is.EqualTo([OrchestrationKind.Sequential, OrchestrationKind.Concurrent, OrchestrationKind.GroupChat, OrchestrationKind.Magentic, OrchestrationKind.Handoff]));
        Assert.That(items.Single(item => item.Kind == OrchestrationKind.Sequential).Summary, Is.EqualTo("1 steps"));
        Assert.That(items.Single(item => item.Kind == OrchestrationKind.Concurrent).Summary, Is.EqualTo("2 participants"));
        Assert.That(items.Single(item => item.Kind == OrchestrationKind.GroupChat).Summary, Is.EqualTo("2 participants - 7 iterations"));
        Assert.That(items.Single(item => item.Kind == OrchestrationKind.Magentic).Summary, Is.EqualTo("1 participants - 4 rounds"));
        Assert.That(items.Single(item => item.Kind == OrchestrationKind.Handoff).Summary, Is.EqualTo("2 participants - 1 routes"));
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
