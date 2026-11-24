using System.Collections.Generic;
using System;
using System.Linq;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Entities;

public class AgentDefinition : IChatTarget
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public bool IsReadOnly { get; set; }
    public required string Model { get; set; }
    public ShellConfiguration ShellConfiguration { get; set; } =
        new(ShellExecutorType.Local, Environment.CurrentDirectory, IsActive: false);
    public int LLMProviderId { get; set; }
    public LLMProvider Provider { get; set; } = null!;
    public List<AgentSessionRecord> AgentSessions { get; set; } = [];
    public List<AgentTool> Tools { get; set; } = [];
    public List<AgentSkillDefinition> Skills { get; set; } = [];
    public List<AgentMCPAttachment> McpAttachments { get; set; } = [];

    public IEnumerable<ToolDefinition> ToolDefinitions => Tools.Select(x => x.ToolDefinition);

    public IEnumerable<McpDefinition> McpDefinitions => McpAttachments.Select(x => x.McpDefinition);

    public virtual UnitResult<IApplicationError> IsValidDefinition()
    {
        List<string> errors = [];
        if (Provider == null)
        {
            errors.Add("LLM provider is not set.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Agent name is required.");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            errors.Add("Agent description is required.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            errors.Add("Agent model is required.");
        }

        return UnitResult.FailureIf<IApplicationError>(errors.Count > 0, new ValidationError([.. errors]));
    }
}
