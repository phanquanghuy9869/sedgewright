namespace Sedgewright.Models;

/// <summary>
/// Describes a single tool exposed by an MCP server.
/// </summary>
public sealed record McpToolInfo(string Name, string Description);
