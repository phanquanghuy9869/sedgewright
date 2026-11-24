using ModelContextProtocol.Client;
using NUnit.Framework;
using Sedgewright.Infras.MAF.Extensions;

namespace Sedgewright.Tests.Infras.MAF.Extensions;

[TestFixture]
[NonParallelizable]
[Category("Integration")]
public sealed class MCPDefinitionExtensionsTests
{
    [Test]
    public async Task FromStdio_CanStartPlaywrightMcpAndListTools()
    {
        StdioConfig stdioConfig = new(
            Name: "playwright",
            Command: "npx",
            Arguments: ["@playwright/mcp@latest"]);

        await using McpClient mcpClient = await McpClient.FromStdio(stdioConfig);

        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

        Assert.That(tools, Is.Not.Empty);
        Assert.That(tools, Has.Some.Matches<McpClientTool>(tool => tool.Name == "browser_navigate"));
    }
}
