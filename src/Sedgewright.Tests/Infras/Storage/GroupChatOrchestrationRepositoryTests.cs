using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class GroupChatOrchestrationRepositoryTests
{
    [Test]
    public async Task GetAsync_LoadsParticipantsAndAgentDefinitions()
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

        GroupChatOrchestration orchestration = new()
        {
            Name = "group-chat-load-test",
            MaximumIterationCount = 4,
            Participants =
            [
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 2,
                    AgentDefinition = secondAgent,
                },
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
            ],
        };

        dbContext.AddRange(provider, firstAgent, secondAgent, orchestration);
        await dbContext.SaveChangesAsync();

        GroupChatOrchestrationRepository repository = new(dbContext);

        GroupChatOrchestration? loaded = await repository.GetAsync(orchestration.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Participants, Has.Count.EqualTo(2));
        Assert.That(loaded.Participants.OrderBy(participant => participant.StepOrder).Select(participant => participant.AgentDefinition.Name), Is.EqualTo(["first-agent", "second-agent"]));
    }

    [Test]
    public async Task CreateUpdateDelete_AndNameExistsAsync_WorkForGroupChatOrchestrations()
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

        dbContext.AddRange(provider, firstAgent, secondAgent);
        await dbContext.SaveChangesAsync();

        GroupChatOrchestrationRepository repository = new(dbContext);

        GroupChatOrchestration orchestration = new()
        {
            Name = "group-chat-crud-test",
            Description = "Initial description",
            MaximumIterationCount = 3,
            Participants =
            [
                new GroupChatOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
            ],
        };

        repository.Create(orchestration);
        await repository.SaveChangesAsync();

        Assert.That(await repository.NameExistsAsync("group-chat-crud-test"), Is.True);
        Assert.That(await repository.NameExistsAsync("group-chat-crud-test", orchestration.Id), Is.False);

        orchestration.Description = "Updated description";
        orchestration.MaximumIterationCount = 6;
        orchestration.Participants.Add(new GroupChatOrchestrationParticipant
        {
            StepOrder = 2,
            AgentDefinition = secondAgent,
        });

        repository.Update(orchestration);
        await repository.SaveChangesAsync();

        GroupChatOrchestration? updated = await repository.GetAsync(orchestration.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Description, Is.EqualTo("Updated description"));
        Assert.That(updated.MaximumIterationCount, Is.EqualTo(6));
        Assert.That(updated.Participants, Has.Count.EqualTo(2));

        repository.Delete(updated);
        await repository.SaveChangesAsync();

        GroupChatOrchestration? deleted = await repository.GetAsync(orchestration.Id);
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
