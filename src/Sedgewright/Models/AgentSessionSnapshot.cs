using System;

namespace Sedgewright.Models;

/// <summary>
/// Lightweight agent session projection for chooser and history views.
/// </summary>
public sealed record AgentSessionSnapshot(
    int Id,
    int AgentDefinitionId,
    string AgentDisplayName,
    string AgentDescription,
    string AgentModel,
    string AgentProviderDisplayName,
    string Summary,
    DateTime CreatedAtUtc,
    DateTime LastUsedAtUtc)
{
    /// <summary>
    /// Gets the compact label shown in lists.
    /// </summary>
    public string DisplayLabel => $"{AgentDisplayName} · {Summary}";
}
