using System;
using System.Collections.Generic;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Represents one skill-directory attachment in the editor draft.
/// </summary>
public sealed record AgentSkillAttachmentDraft(string SkillDirectory, IReadOnlyList<string> SelectedSkills);

/// <summary>
/// Represents the aggregate editable state of the agent definition editor.
/// </summary>
public sealed record AgentDefinitionEditorDraft(
    int DefinitionId,
    string Name,
    string Description,
    string SystemPrompt,
    string UserPrompt,
    string Model,
    bool IsReadOnly,
    LLMProvider? SelectedProvider,
    ShellExecutorType ShellExecutor,
    string ShellWorkingDirectory,
    bool IsShellActive,
    bool IsHarnessAgent,
    bool EnableFileAccess,
    bool DisableTodoProvider,
    bool DisableAgentModeProvider,
    bool DisableFileMemory,
    bool DisableWebSearch,
    bool DisableToolAutoApproval,
    bool DisableAgentSkillsProvider,
    bool DisableOpenTelemetry,
    int? MaxContextWindowTokens,
    int? MaxOutputTokens,
    bool EnableCodeExecution,
    IReadOnlyList<int> SelectedMcpIds,
    IReadOnlyList<AgentSkillAttachmentDraft> SkillAttachments,
    IReadOnlyList<Guid> SelectedToolDefinitionIds);
