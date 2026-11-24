using System.Collections.Generic;

namespace Sedgewright.Entities;

/// <summary>
/// Represents one LLM provider row stored in SQLite.
/// </summary>
public sealed class LLMProvider
{
    public int Id { get; set; }

    public LlmProviderKind ProviderKind { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string EnabledModelsJson { get; set; } = "[]";

    public List<AgentDefinition> AgentDefinitions { get; set; } = [];
}
