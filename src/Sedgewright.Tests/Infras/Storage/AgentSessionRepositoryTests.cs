using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class AgentSessionRepositoryTests
{
    [Test]
    public async Task GetAllAsync_ReturnsSavedSessions_WithAgentMetadata()
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
            Name = "Research Agent",
            Description = "Helps with research",
            Model = "gpt-5",
            Provider = provider,
        };

        AgentSessionRecord oldSession = new()
        {
            AgentDefinition = agent,
            Summary = "Asked about architecture",
            SerializedSessionJson = "{\"id\":1}",
            CreatedAtUtc = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
            LastUsedAtUtc = new DateTime(2026, 7, 17, 12, 5, 0, DateTimeKind.Utc),
        };

        AgentSessionRecord recentSession = new()
        {
            AgentDefinition = agent,
            Summary = "Follow-up on persistence",
            SerializedSessionJson = "{\"id\":2}",
            CreatedAtUtc = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc),
            LastUsedAtUtc = new DateTime(2026, 7, 18, 8, 10, 0, DateTimeKind.Utc),
        };

        dbContext.AddRange(provider, agent, oldSession, recentSession);
        await dbContext.SaveChangesAsync();

        AgentSessionRepository repository = new(dbContext);

        List<AgentSessionRecord> sessions = await repository.GetAllAsync();

        Assert.That(sessions, Has.Count.EqualTo(2));
        Assert.That(sessions[0].Summary, Is.EqualTo("Follow-up on persistence"));
        Assert.That(sessions[0].SerializedSessionJson, Is.EqualTo("{\"id\":2}"));
        Assert.That(sessions[0].AgentDefinition.Provider.DisplayName, Is.EqualTo("OpenAI"));
        Assert.That(sessions[1].Summary, Is.EqualTo("Asked about architecture"));
    }

    [Test]
    public async Task DeleteAsync_RemovesSavedSession()
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
            Name = "Research Agent",
            Description = "Helps with research",
            Model = "gpt-5",
            Provider = provider,
        };

        AgentSessionRecord session = new()
        {
            AgentDefinition = agent,
            Summary = "Session to delete",
            SerializedSessionJson = "{\"id\":1}",
            CreatedAtUtc = DateTime.UtcNow,
            LastUsedAtUtc = DateTime.UtcNow,
        };

        dbContext.AddRange(provider, agent, session);
        await dbContext.SaveChangesAsync();

        AgentSessionRepository repository = new(dbContext);
        AgentSessionRecord? savedSession = await repository.GetAsync(session.Id);

        Assert.That(savedSession, Is.Not.Null);

        repository.Delete(savedSession!);
        await repository.SaveChangesAsync();

        Assert.That(await repository.GetAsync(session.Id), Is.Null);
    }
}
