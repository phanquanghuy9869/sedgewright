using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class AgentDefinitionRepositoryTests
{
    [Test]
    public async Task ShellConfiguration_RoundTripsThroughRepository()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

        AgentDefinition agent = new()
        {
            Name = "executor-agent",
            Description = "Persists the shell executor choice",
            Model = "gpt-5",
            Provider = provider,
            ShellConfiguration = new(ShellExecutorType.Docker, "C:/agents/docker", IsActive: true),
        };

        dbContext.AddRange(provider, agent);
        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);
        AgentDefinition? loaded = await repository.GetAsync(agent.Id);

        Assert.That(loaded!.ShellConfiguration, Is.EqualTo(new ShellConfiguration(ShellExecutorType.Docker, "C:/agents/docker", IsActive: true)));

        loaded.ShellConfiguration = new(ShellExecutorType.Local, "C:/agents/local", IsActive: false);
        await repository.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        AgentDefinition? reloaded = await repository.GetAsync(agent.Id);

        Assert.That(reloaded!.ShellConfiguration, Is.EqualTo(new ShellConfiguration(ShellExecutorType.Local, "C:/agents/local", IsActive: false)));
    }

    [Test]
    public async Task GetAllAsync_ReportsMcpAttachmentCount_AndUsageCount()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

        McpDefinition mcpDefinition = new()
        {
            Name = "playwright",
            Description = "Browser automation",
            IsReadOnly = false,
            IsStdio = true,
            ConfigJson = "{}",
        };

        AgentDefinition agent = new()
        {
            Name = "research-agent",
            Description = "Test agent",
            Model = "gpt-5",
            Provider = provider,
        };

        ConcurrentOrchestration orchestration = new()
        {
            Name = "Concurrent test orchestration",
        };

        dbContext.AddRange(provider, mcpDefinition, agent, orchestration);
        await dbContext.SaveChangesAsync();

        dbContext.Add(new AgentMCPAttachment
        {
            AgentDefinition = agent,
            McpDefinition = mcpDefinition,
        });

        dbContext.Add(new ConcurrentOrchestrationParticipant
        {
            ConcurrentOrchestration = orchestration,
            AgentDefinition = agent,
        });

        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);

        List<AgentDefinition> snapshots = await repository.GetAllAsync();

        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots[0].Provider.DisplayName, Is.EqualTo("OpenAI"));
        Assert.That(snapshots[0].McpAttachments, Has.Count.EqualTo(1));
        Assert.That(await repository.HasReferencesAsync(agent.Id), Is.True);
    }

    [Test]
    public async Task GetAsync_LoadsProviderAndMcpAttachments()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

        McpDefinition mcpDefinition = new()
        {
            Name = "filesystem",
            Description = "File system access",
            IsReadOnly = true,
            IsStdio = false,
            ConfigJson = "{}",
        };

        AgentDefinition agent = new()
        {
            Name = "builder-agent",
            Description = "Builds things",
            Model = "gpt-5",
            Provider = provider,
        };

        dbContext.AddRange(provider, mcpDefinition, agent);
        await dbContext.SaveChangesAsync();

        dbContext.Add(new AgentMCPAttachment
        {
            AgentDefinition = agent,
            McpDefinition = mcpDefinition,
        });

        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);

        AgentDefinition? loaded = await repository.GetAsync(agent.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Provider.DisplayName, Is.EqualTo("OpenAI"));
        Assert.That(loaded.McpAttachments, Has.Count.EqualTo(1));
        Assert.That(loaded.McpAttachments[0].McpDefinition.Name, Is.EqualTo("filesystem"));
    }

    [Test]
    public async Task GetAsync_LoadsMultipleToolDefinitions()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };
        AgentDefinition agent = new()
        {
            Name = "multi-tool-agent",
            Description = "Agent with multiple tools",
            Model = "gpt-5",
            Provider = provider,
        };
        ToolDefinition firstTool = new()
        {
            Name = "first-tool",
            Type = ToolDefinitionType.Native,
        };
        ToolDefinition secondTool = new()
        {
            Name = "second-tool",
            Type = ToolDefinitionType.Native,
        };

        dbContext.AddRange(provider, agent, firstTool, secondTool);
        await dbContext.SaveChangesAsync();

        dbContext.AddRange(
            new AgentTool
            {
                AgentDefinition = agent,
                ToolDefinition = firstTool,
            },
            new AgentTool
            {
                AgentDefinition = agent,
                ToolDefinition = secondTool,
            });
        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);
        AgentDefinition? loaded = await repository.GetAsync(agent.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Tools, Has.Count.EqualTo(2));
        Assert.That(loaded.ToolDefinitions.Count(), Is.EqualTo(2));
        Assert.That(loaded.Tools.Select(tool => tool.ToolDefinition.Name), Is.EquivalentTo(["first-tool", "second-tool"]));
    }

    [Test]
    public async Task GetAsync_LoadsAgentSkillsAndPersistsSelectedSkillChanges()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

        AgentDefinition agent = new()
        {
            Name = "skills-agent",
            Description = "Agent with directory-based skills",
            Model = "gpt-5",
            Provider = provider,
        };

        dbContext.AddRange(provider, agent);
        await dbContext.SaveChangesAsync();

        dbContext.AddRange(
            new AgentSkillDefinition
            {
                AgentDefinition = agent,
                SkillDirectory = ".agents/skills",
                SelectedSkills = ["find-skills"],
            },
            new AgentSkillDefinition
            {
                AgentDefinition = agent,
                SkillDirectory = ".github/skills",
                SelectedSkills = ["code-review", "testing"],
            });
        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);

        AgentDefinition? loaded = await repository.GetAsync(agent.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Skills, Has.Count.EqualTo(2));
        Assert.That(
            loaded.Skills.Single(skill => skill.SkillDirectory == ".agents/skills").SelectedSkills,
            Is.EqualTo(["find-skills"]));
        Assert.That(
            loaded.Skills.Single(skill => skill.SkillDirectory == ".github/skills").SelectedSkills,
            Is.EqualTo(["code-review", "testing"]));

        loaded.Skills.Single(skill => skill.SkillDirectory == ".agents/skills").SelectedSkills.Add("playwright");
        await repository.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        AgentDefinition? reloaded = await repository.GetAsync(agent.Id);

        Assert.That(reloaded, Is.Not.Null);
        Assert.That(
            reloaded!.Skills.Single(skill => skill.SkillDirectory == ".agents/skills").SelectedSkills,
            Is.EqualTo(["find-skills", "playwright"]));
    }

    [Test]
    public async Task Delete_CascadesAgentSessions_WithoutTreatingThemAsWorkflowReferences()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.OpenAI,
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };

        AgentDefinition agent = new()
        {
            Name = "session-backed-agent",
            Description = "Agent with sessions",
            Model = "gpt-5",
            Provider = provider,
        };

        AgentSessionRecord session = new()
        {
            AgentDefinition = agent,
            Summary = "Existing session",
            SerializedSessionJson = "{}",
            CreatedAtUtc = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc),
            LastUsedAtUtc = new DateTime(2026, 7, 18, 10, 5, 0, DateTimeKind.Utc),
        };

        dbContext.AddRange(provider, agent, session);
        await dbContext.SaveChangesAsync();

        AgentDefinitionRepository repository = new(dbContext);

        Assert.That(await repository.HasReferencesAsync(agent.Id), Is.False);

        dbContext.ChangeTracker.Clear();
        AgentDefinition persistedAgent = (await repository.GetAsync(agent.Id))!;
        repository.Delete(persistedAgent);
        await repository.SaveChangesAsync();

        Assert.That(await dbContext.AgentSessions.AnyAsync(), Is.False);
    }
}
