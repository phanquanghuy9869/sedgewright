using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class MagenticOrchestrationRepositoryTests
{
    [Test]
    public async Task GetAsync_LoadsManagerAndParticipants()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition managerAgent = CreateAgent("manager-agent", provider);
        AgentDefinition firstAgent = CreateAgent("first-agent", provider);
        AgentDefinition secondAgent = CreateAgent("second-agent", provider);

        MagenticOrchestration orchestration = new()
        {
            Name = "magentic-load-test",
            ManagerAgentDefinition = managerAgent,
            Participants =
            [
                new MagenticOrchestrationParticipant
                {
                    StepOrder = 2,
                    AgentDefinition = secondAgent,
                },
                new MagenticOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
            ],
        };

        dbContext.AddRange(provider, managerAgent, firstAgent, secondAgent, orchestration);
        await dbContext.SaveChangesAsync();

        MagenticOrchestrationRepository repository = new(dbContext);

        MagenticOrchestration? loaded = await repository.GetAsync(orchestration.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.ManagerAgentDefinition.Name, Is.EqualTo("manager-agent"));
        Assert.That(loaded.Participants, Has.Count.EqualTo(2));
        Assert.That(loaded.Participants.OrderBy(participant => participant.StepOrder).Select(participant => participant.AgentDefinition.Name), Is.EqualTo(["first-agent", "second-agent"]));
    }

    [Test]
    public async Task CreateUpdateDelete_AndNameExistsAsync_WorkForMagenticOrchestrations()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        LLMProvider provider = CreateProvider();
        AgentDefinition managerAgent = CreateAgent("manager-agent", provider);
        AgentDefinition firstAgent = CreateAgent("first-agent", provider);
        AgentDefinition secondAgent = CreateAgent("second-agent", provider);

        dbContext.AddRange(provider, managerAgent, firstAgent, secondAgent);
        await dbContext.SaveChangesAsync();

        MagenticOrchestrationRepository repository = new(dbContext);

        MagenticOrchestration orchestration = new()
        {
            Name = "magentic-crud-test",
            Description = "Initial description",
            ManagerAgentDefinition = managerAgent,
            RequirePlanSignoff = true,
            MaximumRoundCount = 4,
            MaximumStallCount = 1,
            MaximumResetCount = 1,
            Participants =
            [
                new MagenticOrchestrationParticipant
                {
                    StepOrder = 1,
                    AgentDefinition = firstAgent,
                },
            ],
        };

        repository.Create(orchestration);
        await repository.SaveChangesAsync();

        Assert.That(await repository.NameExistsAsync("magentic-crud-test"), Is.True);
        Assert.That(await repository.NameExistsAsync("magentic-crud-test", orchestration.Id), Is.False);

        orchestration.Description = "Updated description";
        orchestration.MaximumRoundCount = 6;
        orchestration.Participants.Add(new MagenticOrchestrationParticipant
        {
            StepOrder = 2,
            AgentDefinition = secondAgent,
        });

        repository.Update(orchestration);
        await repository.SaveChangesAsync();

        MagenticOrchestration? updated = await repository.GetAsync(orchestration.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Description, Is.EqualTo("Updated description"));
        Assert.That(updated.MaximumRoundCount, Is.EqualTo(6));
        Assert.That(updated.Participants, Has.Count.EqualTo(2));

        repository.Delete(updated);
        await repository.SaveChangesAsync();

        MagenticOrchestration? deleted = await repository.GetAsync(orchestration.Id);
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
