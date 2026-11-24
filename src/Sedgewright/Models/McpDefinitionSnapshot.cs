namespace Sedgewright.Models;

/// <summary>
/// Lightweight MCP definition data for list rendering.
/// </summary>
public sealed record McpDefinitionSnapshot(
    int Id,
    string Name,
    string Description,
    bool IsReadOnly,
    bool IsStdio,
    string ConfigJson,
    int AttachmentCount);
