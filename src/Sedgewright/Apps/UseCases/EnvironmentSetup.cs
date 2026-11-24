using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Creates default application resources on first startup.
/// </summary>
public sealed class EnvironmentSetup(IMcpDefinitionRepository mcpDefinitionRepository) : IEnvironmentSetup
{
    private const string PlaywrightName = "Playwright";
    private const string PlaywrightDescription = "Playwright browser automation MCP server";

    private readonly IMcpDefinitionRepository _mcpDefinitionRepository = mcpDefinitionRepository;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if ((await _mcpDefinitionRepository.GetAllAsync(cancellationToken)).Count > 0)
        {
            return;
        }

        StdioConfig stdioConfig = new(
            Name: PlaywrightName,
            Command: "npx",
            Arguments: ["@playwright/mcp@latest","--extension"]);

        McpDefinition playwrightDefinition = new()
        {
            Name = PlaywrightName,
            Description = PlaywrightDescription,
            IsReadOnly = false,
            IsStdio = true,
            ConfigJson = JsonSerializer.Serialize(stdioConfig),
        };

        _mcpDefinitionRepository.Create(playwrightDefinition);
        await _mcpDefinitionRepository.SaveChangesAsync(cancellationToken);
    }
}
