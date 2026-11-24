using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Apps.UseCases;

[TestFixture]
public sealed class EnvironmentSetupTests
{
    [Test]
    public async Task InitializeAsync_WhenMcpTableIsEmpty_CreatesPlaywrightDefinition()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        EnvironmentSetup setup = new(new McpDefinitionRepository(dbContext));

        await setup.InitializeAsync();

        List<Sedgewright.Models.McpDefinitionSnapshot> definitions =
            await new McpDefinitionRepository(dbContext).GetAllAsync();
        McpDefinitionSnapshotAssertions.AssertPlaywright(definitions);
    }

    [Test]
    public async Task InitializeAsync_WhenCalledTwice_DoesNotCreateDuplicateDefinitions()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        McpDefinitionRepository repository = new(dbContext);
        EnvironmentSetup setup = new(repository);

        await setup.InitializeAsync();
        await setup.InitializeAsync();

        List<Sedgewright.Models.McpDefinitionSnapshot> definitions = await repository.GetAllAsync();
        Assert.That(definitions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_WhenMcpTableIsNotEmpty_LeavesExistingDefinitionsUnchanged()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using SedgewrightDbContext dbContext = await CreateDbContextAsync(connection);
        McpDefinitionRepository repository = new(dbContext);
        repository.Create(new McpDefinition
        {
            Name = "Existing MCP",
            Description = "Created by the user",
            IsReadOnly = true,
            IsStdio = false,
            ConfigJson = "{\"Name\":\"Existing MCP\",\"Endpoint\":\"https://example.invalid/mcp\"}",
        });
        await repository.SaveChangesAsync();

        EnvironmentSetup setup = new(repository);
        await setup.InitializeAsync();

        List<Sedgewright.Models.McpDefinitionSnapshot> definitions = await repository.GetAllAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(definitions[0].Name, Is.EqualTo("Existing MCP"));
            Assert.That(definitions[0].Description, Is.EqualTo("Created by the user"));
            Assert.That(definitions[0].IsReadOnly, Is.True);
            Assert.That(definitions[0].IsStdio, Is.False);
            Assert.That(definitions[0].ConfigJson, Is.EqualTo("{\"Name\":\"Existing MCP\",\"Endpoint\":\"https://example.invalid/mcp\"}"));
        }
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

    private static class McpDefinitionSnapshotAssertions
    {
        public static void AssertPlaywright(IReadOnlyList<Sedgewright.Models.McpDefinitionSnapshot> definitions)
        {
            Assert.That(definitions, Has.Count.EqualTo(1));
            Sedgewright.Models.McpDefinitionSnapshot definition = definitions[0];
            StdioConfig config = JsonSerializer.Deserialize<StdioConfig>(definition.ConfigJson)
                ?? throw new AssertionException("The Playwright MCP configuration could not be deserialized.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(definition.Name, Is.EqualTo("Playwright"));
                Assert.That(definition.Description, Is.EqualTo("Playwright browser automation MCP server"));
                Assert.That(definition.IsReadOnly, Is.False);
                Assert.That(definition.IsStdio, Is.True);
                Assert.That(config.Name, Is.EqualTo("Playwright"));
                Assert.That(config.Command, Is.EqualTo("npx"));
                Assert.That(config.Arguments, Is.EqualTo(new[] { "@playwright/mcp@latest" }));
            }
        }
    }
}
