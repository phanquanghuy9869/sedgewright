using System;
using System.Collections.Generic;
using System.Linq;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Maps agent editor draft state to persisted entities and back.
/// </summary>
public sealed class AgentDefinitionDraftMapper
{
    /// <summary>
    /// Creates an editable draft from an existing definition or the default new-agent state.
    /// </summary>
    /// <param name="definition">The existing definition, if any.</param>
    /// <returns>The editor draft.</returns>
    public static AgentDefinitionEditorDraft CreateDraft(
        AgentDefinition? definition,
        string? defaultWorkingDirectory = null)
    {
        if (definition is null)
        {
            string workingDirectory = ResolveWorkingDirectory(defaultWorkingDirectory);

            return new AgentDefinitionEditorDraft(
                DefinitionId: 0,
                Name: string.Empty,
                Description: string.Empty,
                SystemPrompt: string.Empty,
                UserPrompt: string.Empty,
                Model: string.Empty,
                IsReadOnly: false,
                SelectedProvider: null,
                ShellExecutor: ShellExecutorType.Local,
                ShellWorkingDirectory: workingDirectory,
                IsShellActive: true,
                IsHarnessAgent: false,
                EnableFileAccess: workingDirectory.Length > 0,
                DisableTodoProvider: false,
                DisableAgentModeProvider: false,
                DisableFileMemory: false,
                DisableWebSearch: true,
                DisableToolAutoApproval: false,
                DisableAgentSkillsProvider: true,
                DisableOpenTelemetry: false,
                MaxContextWindowTokens: null,
                MaxOutputTokens: null,
                EnableCodeExecution: false,
                SelectedMcpIds: [],
                SkillAttachments: [],
                SelectedToolDefinitionIds: []);
        }

        HarnessAgentDefinition? harnessDefinition = definition as HarnessAgentDefinition;
        IReadOnlyList<AgentSkillAttachmentDraft> skillAttachments = [.. definition.Skills
            .Select(skill => new AgentSkillAttachmentDraft(
                NormalizeSkillDirectory(skill.SkillDirectory),
                NormalizeSkills(skill.SelectedSkills)))
            .Where(skill => skill.SkillDirectory.Length > 0)
            .GroupBy(skill => skill.SkillDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];

        return new AgentDefinitionEditorDraft(
            DefinitionId: definition.Id,
            Name: definition.Name,
            Description: definition.Description,
            SystemPrompt: definition.SystemPrompt ?? string.Empty,
            UserPrompt: definition.UserPrompt ?? string.Empty,
            Model: definition.Model,
            IsReadOnly: definition.IsReadOnly,
            SelectedProvider: definition.Provider,
            ShellExecutor: definition.ShellConfiguration.Executor,
            ShellWorkingDirectory: definition.ShellConfiguration.WorkingDirectory,
            IsShellActive: definition.ShellConfiguration.IsActive,
            IsHarnessAgent: harnessDefinition is not null,
            EnableFileAccess: !string.IsNullOrWhiteSpace(harnessDefinition?.FileAccessWorkingDirectory),
            DisableTodoProvider: harnessDefinition?.DisableTodoProvider ?? false,
            DisableAgentModeProvider: harnessDefinition?.DisableAgentModeProvider ?? false,
            DisableFileMemory: harnessDefinition?.DisableFileMemory ?? false,
            DisableWebSearch: harnessDefinition?.DisableWebSearch ?? true,
            DisableToolAutoApproval: harnessDefinition?.DisableToolAutoApproval ?? false,
            DisableAgentSkillsProvider: harnessDefinition?.DisableAgentSkillsProvider ?? true,
            DisableOpenTelemetry: harnessDefinition?.DisableOpenTelemetry ?? false,
            MaxContextWindowTokens: harnessDefinition?.MaxContextWindowTokens,
            MaxOutputTokens: harnessDefinition?.MaxOutputTokens,
            EnableCodeExecution: harnessDefinition?.EnableCodeExecution ?? false,
            SelectedMcpIds: [.. definition.McpAttachments
                .Select(attachment => attachment.McpId)
                .Distinct()],
            SkillAttachments: skillAttachments,
            SelectedToolDefinitionIds: [.. definition.Tools
                .Select(tool => tool.ToolDefinitionId)
                .Distinct()]);
    }

    /// <summary>
    /// Creates a candidate entity from the current draft state for validation or persistence.
    /// </summary>
    /// <param name="draft">The draft to convert.</param>
    /// <param name="sourceDefinition">The source definition used to clone relationships.</param>
    /// <returns>The converted entity.</returns>
    public AgentDefinition CreateCandidateDefinition(AgentDefinitionEditorDraft draft, AgentDefinition sourceDefinition)
    {
        AgentDefinition definition = draft.IsHarnessAgent
            ? new HarnessAgentDefinition
            {
                Id = sourceDefinition.Id,
                Name = string.Empty,
                Description = string.Empty,
                Model = string.Empty,
            }
            : new AgentDefinition
            {
                Id = sourceDefinition.Id,
                Name = string.Empty,
                Description = string.Empty,
                Model = string.Empty,
            };

        ApplyCommonDraft(definition, draft);
        if (definition is HarnessAgentDefinition harnessDefinition)
        {
            ApplyHarnessDraft(harnessDefinition, draft);
        }

        CloneRelationships(sourceDefinition, definition, draft);
        return definition;
    }

    /// <summary>
    /// Applies the current draft state to an existing persisted definition.
    /// </summary>
    /// <param name="target">The persisted definition being edited.</param>
    /// <param name="draft">The current editor draft.</param>
    public void ApplyDraft(AgentDefinition target, AgentDefinitionEditorDraft draft)
    {
        ApplyCommonDraft(target, draft);
        SyncMcpAttachments(target, draft);
        SyncSkillAttachments(target, draft);
        SyncToolAttachments(target, draft);

        if (target is HarnessAgentDefinition harnessDefinition)
        {
            ApplyHarnessDraft(harnessDefinition, draft);
        }
    }

    private static void ApplyCommonDraft(AgentDefinition definition, AgentDefinitionEditorDraft draft)
    {
        definition.Name = Normalize(draft.Name);
        definition.Description = Normalize(draft.Description);
        definition.SystemPrompt = NormalizeOptional(draft.SystemPrompt);
        definition.UserPrompt = NormalizeOptional(draft.UserPrompt);
        definition.Model = Normalize(draft.Model);
        definition.IsReadOnly = draft.IsReadOnly;
        definition.ShellConfiguration = new(
            draft.ShellExecutor,
            Normalize(draft.ShellWorkingDirectory),
            draft.IsShellActive);
        definition.Provider = draft.SelectedProvider!;
        definition.LLMProviderId = draft.SelectedProvider?.Id ?? 0;
    }

    private static void ApplyHarnessDraft(HarnessAgentDefinition definition, AgentDefinitionEditorDraft draft)
    {
        definition.DisableTodoProvider = draft.DisableTodoProvider;
        definition.DisableAgentModeProvider = draft.DisableAgentModeProvider;
        definition.DisableFileMemory = draft.DisableFileMemory;
        definition.DisableWebSearch = draft.DisableWebSearch;
        definition.DisableToolAutoApproval = draft.DisableToolAutoApproval;
        definition.DisableAgentSkillsProvider = draft.DisableAgentSkillsProvider;
        definition.DisableOpenTelemetry = draft.DisableOpenTelemetry;
        definition.MaxContextWindowTokens = draft.MaxContextWindowTokens;
        definition.MaxOutputTokens = draft.MaxOutputTokens;
        definition.FileAccessWorkingDirectory = draft.EnableFileAccess
            ? NormalizeOptional(draft.ShellWorkingDirectory)
            : null;
        definition.EnableCodeExecution = draft.EnableCodeExecution;
    }

    private static void CloneRelationships(
        AgentDefinition sourceDefinition,
        AgentDefinition targetDefinition,
        AgentDefinitionEditorDraft draft)
    {
        Dictionary<Guid, AgentTool> existingToolsById = sourceDefinition.Tools
            .GroupBy(tool => tool.ToolDefinitionId)
            .ToDictionary(group => group.Key, group => group.First());
        targetDefinition.Tools = [.. draft.SelectedToolDefinitionIds
            .Distinct()
            .Select(toolDefinitionId => CreateToolAttachment(
                targetDefinition,
                toolDefinitionId,
                existingToolsById))];

        Dictionary<int, AgentMCPAttachment> existingAttachmentsById = sourceDefinition.McpAttachments
            .GroupBy(attachment => attachment.McpId)
            .ToDictionary(group => group.Key, group => group.First());
        targetDefinition.McpAttachments = [.. draft.SelectedMcpIds
            .Distinct()
            .Select(mcpId => CreateMcpAttachment(targetDefinition, mcpId, existingAttachmentsById))];

        targetDefinition.Skills = [.. draft.SkillAttachments
            .Select(skill => CreateSkillAttachment(targetDefinition, skill))];
    }

    private static void SyncMcpAttachments(AgentDefinition targetDefinition, AgentDefinitionEditorDraft draft)
    {
        HashSet<int> selectedMcpIds = [.. draft.SelectedMcpIds];
        foreach (AgentMCPAttachment attachment in targetDefinition.McpAttachments.ToList())
        {
            if (!selectedMcpIds.Contains(attachment.McpId))
            {
                targetDefinition.McpAttachments.Remove(attachment);
            }
        }

        HashSet<int> currentAttachmentIds = targetDefinition.McpAttachments
            .Select(attachment => attachment.McpId)
            .ToHashSet();
        foreach (int mcpId in selectedMcpIds)
        {
            if (currentAttachmentIds.Contains(mcpId))
            {
                continue;
            }

            targetDefinition.McpAttachments.Add(new AgentMCPAttachment
            {
                AgentId = targetDefinition.Id,
                AgentDefinition = targetDefinition,
                McpId = mcpId,
            });
        }
    }

    private static void SyncSkillAttachments(AgentDefinition targetDefinition, AgentDefinitionEditorDraft draft)
    {
        HashSet<string> selectedDirectories = new(
            draft.SkillAttachments.Select(skill => skill.SkillDirectory),
            StringComparer.OrdinalIgnoreCase);

        foreach (AgentSkillDefinition existingSkill in targetDefinition.Skills.ToList())
        {
            if (!selectedDirectories.Contains(existingSkill.SkillDirectory))
            {
                targetDefinition.Skills.Remove(existingSkill);
            }
        }

        foreach (AgentSkillAttachmentDraft skillDraft in draft.SkillAttachments)
        {
            AgentSkillDefinition? existingSkill = targetDefinition.Skills.FirstOrDefault(skill =>
                string.Equals(skill.SkillDirectory, skillDraft.SkillDirectory, StringComparison.OrdinalIgnoreCase));
            if (existingSkill is null)
            {
                targetDefinition.Skills.Add(CreateSkillAttachment(targetDefinition, skillDraft));
                continue;
            }

            existingSkill.AgentId = targetDefinition.Id;
            existingSkill.AgentDefinition = targetDefinition;
            existingSkill.SkillDirectory = skillDraft.SkillDirectory;
            existingSkill.SelectedSkills = [.. skillDraft.SelectedSkills];
        }
    }

    private static void SyncToolAttachments(AgentDefinition targetDefinition, AgentDefinitionEditorDraft draft)
    {
        HashSet<Guid> selectedToolDefinitionIds = [.. draft.SelectedToolDefinitionIds];

        foreach (AgentTool existingTool in targetDefinition.Tools.ToList())
        {
            if (!selectedToolDefinitionIds.Contains(existingTool.ToolDefinitionId))
            {
                targetDefinition.Tools.Remove(existingTool);
            }
        }

        HashSet<Guid> currentToolDefinitionIds = targetDefinition.Tools
            .Select(tool => tool.ToolDefinitionId)
            .ToHashSet();
        foreach (Guid toolDefinitionId in selectedToolDefinitionIds)
        {
            if (currentToolDefinitionIds.Contains(toolDefinitionId))
            {
                continue;
            }

            targetDefinition.Tools.Add(new AgentTool
            {
                AgentId = targetDefinition.Id,
                AgentDefinition = targetDefinition,
                ToolDefinitionId = toolDefinitionId,
                ToolDefinition = null!,
            });
        }
    }

    private static AgentTool CreateToolAttachment(
        AgentDefinition targetDefinition,
        Guid toolDefinitionId,
        IReadOnlyDictionary<Guid, AgentTool> existingToolsById)
    {
        AgentTool attachment = new()
        {
            AgentId = targetDefinition.Id,
            AgentDefinition = targetDefinition,
            ToolDefinitionId = toolDefinitionId,
            ToolDefinition = null!,
        };

        if (existingToolsById.TryGetValue(toolDefinitionId, out AgentTool? existingTool))
        {
            attachment.Id = existingTool.Id;
            attachment.ToolDefinition = existingTool.ToolDefinition;
        }

        return attachment;
    }

    private static AgentMCPAttachment CreateMcpAttachment(
        AgentDefinition targetDefinition,
        int mcpId,
        IReadOnlyDictionary<int, AgentMCPAttachment> existingAttachmentsById)
    {
        AgentMCPAttachment attachment = new()
        {
            AgentId = targetDefinition.Id,
            AgentDefinition = targetDefinition,
            McpId = mcpId,
        };

        if (existingAttachmentsById.TryGetValue(mcpId, out AgentMCPAttachment? existingAttachment))
        {
            attachment.McpDefinition = existingAttachment.McpDefinition;
        }

        return attachment;
    }

    private static AgentSkillDefinition CreateSkillAttachment(
        AgentDefinition targetDefinition,
        AgentSkillAttachmentDraft skillDraft)
        => new()
        {
            AgentId = targetDefinition.Id,
            SkillDirectory = skillDraft.SkillDirectory,
            SelectedSkills = [.. skillDraft.SelectedSkills],
            AgentDefinition = targetDefinition,
        };

    private static IReadOnlyList<string> NormalizeSkills(IEnumerable<string> selectedSkills)
        => [.. selectedSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string ResolveWorkingDirectory(string? value)
        => Normalize(value) is { Length: > 0 } normalized
            ? normalized
            : Environment.CurrentDirectory;

    private static string NormalizeSkillDirectory(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/', '\\');

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
