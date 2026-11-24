using System;

namespace Sedgewright.Entities;

/// <summary>
/// Stores a persisted Microsoft Agents session together with enough agent metadata to list and
/// resume the conversation later.
/// </summary>
public sealed class AgentSessionRecord
{
    public int Id { get; set; }

    public int AgentDefinitionId { get; set; }

    public AgentDefinition AgentDefinition { get; set; } = null!;

    public required string Summary { get; set; }

    public required string SerializedSessionJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUsedAtUtc { get; set; }
}
