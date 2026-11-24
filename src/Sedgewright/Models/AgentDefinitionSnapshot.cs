namespace Sedgewright.Models;

/// <summary>
/// Lightweight agent definition data for list rendering.
/// </summary>
public sealed record AgentDefinitionSnapshot(
    int Id,
    string Name,
    string Description,
    bool IsReadOnly,
    string Model,
    string ProviderDisplayName,
    int McpAttachmentCount,
    int UsageCount);
