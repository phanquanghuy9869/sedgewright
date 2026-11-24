using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class McpDefinitionRepositoryTests
{
    [Test]
    public async Task GetAllAsync_ReportsAttachmentCount_AndHasAttachmentsAsync_ReturnsTrue()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using SedgewrightDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO LLMProviders (ProviderKind, DisplayName, BaseUrl, ApiKey, EnabledModelsJson) VALUES ({0}, {1}, {2}, {3}, {4})",
            LlmProviderKind.OpenAI.ToString(),
            "OpenAI",
            "https://example.invalid",
            "test-key",
            "[]");

        int providerId = await GetLastInsertRowIdAsync(connection);

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO AgentDefinitions (Name, Description, SystemPrompt, UserPrompt, IsReadOnly, Model, ShellExecutor, ShellWorkingDirectory, ShellIsActive, LLMProviderId) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
            "agent-one",
            "Test agent",
            null,
            null,
            false,
            "gpt-5",
            ShellExecutorType.Local.ToString(),
            Environment.CurrentDirectory,
            false,
            providerId);

        int agentId = await GetLastInsertRowIdAsync(connection);

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO MCPDefinitions (Name, Description, IsReadOnly, IsStdio, ConfigJson) VALUES ({0}, {1}, {2}, {3}, {4})",
            "playwright",
            "Browser automation",
            false,
            true,
            "{}");

        int definitionId = await GetLastInsertRowIdAsync(connection);

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO AgentMCPAttachments (AgentId, McpId) VALUES ({0}, {1})",
            agentId,
            definitionId);

        McpDefinitionRepository repository = new(dbContext);

        List<McpDefinitionSnapshot> snapshots = await repository.GetAllAsync();

        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots[0].AttachmentCount, Is.EqualTo(1));
        Assert.That(await repository.HasAttachmentsAsync(definitionId), Is.True);
    }

    private static async Task<int> GetLastInsertRowIdAsync(SqliteConnection connection)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT last_insert_rowid();";
        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
