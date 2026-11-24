using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sedgewright.Entities;

/// <summary>
/// Builds <see cref="AgentDefinition"/> instances with a fluent API.
/// </summary>
public sealed class AgentDefinitionBuilder
{
    private readonly AgentDefinition agentDefinition = new()
    {
        Name = string.Empty,
        Description = string.Empty,
        Model = string.Empty,
    };

    /// <summary>
    /// Sets the agent name, description, prompt, and model in one call.
    /// </summary>
    public AgentDefinitionBuilder With(string name, string description, string? systemPrompt, string model)
    {
        agentDefinition.Name = name;
        agentDefinition.Description = description;
        agentDefinition.SystemPrompt = systemPrompt;
        agentDefinition.Model = model;
        return this;
    }

    /// <summary>
    /// Sets the LLM provider.
    /// </summary>
    public AgentDefinitionBuilder WithProvider(LLMProvider provider)
    {
        agentDefinition.Provider = provider;
        agentDefinition.LLMProviderId = provider.Id;
        return this;
    }

    /// <summary>
    /// Adds a skill attachment to the agent.
    /// </summary>
    public AgentDefinitionBuilder AddSkill(string skillDirectory, IEnumerable<string> selectedSkills)
    {
        agentDefinition.Skills.Add(new AgentSkillDefinition
        {
            AgentDefinition = agentDefinition,
            SkillDirectory = skillDirectory,
            SelectedSkills = [.. selectedSkills],
        });

        return this;
    }

    /// <summary>
    /// Adds a Playwright-style stdio MCP server attachment.
    /// </summary>
    public AgentDefinitionBuilder AddMcpStdio(string name, string description, string command, IEnumerable<string> arguments, bool isReadOnly = false)
    {
        agentDefinition.McpAttachments.Add(CreateMcpAttachment(
            name,
            description,
            isReadOnly,
            isStdio: true,
            JsonSerializer.Serialize(new
            {
                Name = name,
                Command = command,
                Arguments = arguments.ToArray(),
            })));

        return this;
    }

    /// <summary>
    /// Adds an HTTP MCP server attachment.
    /// </summary>
    public AgentDefinitionBuilder AddMcpHttp(string name, string description, string endpoint, Dictionary<string, string>? headers = null, bool isReadOnly = false)
    {
        Dictionary<string, string> resolvedHeaders = headers ?? [];

        agentDefinition.McpAttachments.Add(CreateMcpAttachment(
            name,
            description,
            isReadOnly,
            isStdio: false,
            JsonSerializer.Serialize(new
            {
                Name = name,
                Endpoint = endpoint,
                Headers = resolvedHeaders,
            })));

        return this;
    }

    /// <summary>
    /// Returns the configured agent definition.
    /// </summary>
    public AgentDefinition Build() => agentDefinition;

    private AgentMCPAttachment CreateMcpAttachment(string name, string description, bool isReadOnly, bool isStdio, string configJson)
    {
        McpDefinition mcpDefinition = new()
        {
            Name = name,
            Description = description,
            IsReadOnly = isReadOnly,
            IsStdio = isStdio,
            ConfigJson = configJson,
        };

        return new AgentMCPAttachment
        {
            AgentDefinition = agentDefinition,
            McpDefinition = mcpDefinition,
        };
    }
}
