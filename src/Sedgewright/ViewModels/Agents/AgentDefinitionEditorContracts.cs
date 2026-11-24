using System;
using System.Collections.Generic;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds the Harness-specific settings edited by the agent definition editor.
/// </summary>
public sealed record AgentHarnessOptionsSelection(
    bool IsHarnessAgent,
    bool DisableTodoProvider,
    bool DisableAgentModeProvider,
    bool DisableFileMemory,
    bool DisableWebSearch,
    bool DisableToolAutoApproval,
    bool DisableAgentSkillsProvider,
    bool DisableOpenTelemetry,
    int? MaxContextWindowTokens,
    int? MaxOutputTokens,
    bool EnableFileAccess,
    bool EnableCodeExecution);

/// <summary>
/// Provides the current Harness settings to the selection dialog.
/// </summary>
public sealed class AgentHarnessOptionsSelectionRequestedEventArgs(
    AgentHarnessOptionsSelection initialSelection,
    AgentHarnessOptionsSelection? revertSelection) : EventArgs
{
    /// <summary>
    /// Gets the settings selected before opening the dialog.
    /// </summary>
    public AgentHarnessOptionsSelection InitialSelection { get; } = initialSelection;

    /// <summary>
    /// Gets the settings to restore when the dialog is canceled, if the dialog was opened by enabling Harness.
    /// </summary>
    public AgentHarnessOptionsSelection? RevertSelection { get; } = revertSelection;
}

/// <summary>
/// Describes a close request from the Harness options dialog.
/// </summary>
public sealed class AgentHarnessOptionsSelectionCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    /// Gets whether the edited settings should be applied.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
/// Provides discovered provider models and the current editor selection to the chooser window.
/// </summary>
public sealed class AgentModelSelectionRequestedEventArgs(
    string providerDisplayName,
    IReadOnlyList<string> discoveredModels,
    IReadOnlyList<string> initiallySelectedModels) : EventArgs
{
    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    public string ProviderDisplayName { get; } = providerDisplayName;

    /// <summary>
    /// Gets the discovered model ids.
    /// </summary>
    public IReadOnlyList<string> DiscoveredModels { get; } = discoveredModels;

    /// <summary>
    /// Gets the currently selected model ids.
    /// </summary>
    public IReadOnlyList<string> InitiallySelectedModels { get; } = initiallySelectedModels;
}

/// <summary>
/// Provides the source roots and current attachments to the skill selection dialog.
/// </summary>
public sealed class AgentSkillSelectionRequestedEventArgs(
    IReadOnlyList<AgentSkillSource> skillSources,
    IReadOnlyList<AgentSkillAttachmentDraft> initiallySelectedAttachments) : EventArgs
{
    /// <summary>
    /// Gets the source roots shown in the picker.
    /// </summary>
    public IReadOnlyList<AgentSkillSource> SkillSources { get; } = skillSources;

    /// <summary>
    /// Gets the existing per-root selections shown in the picker.
    /// </summary>
    public IReadOnlyList<AgentSkillAttachmentDraft> InitiallySelectedAttachments { get; } = initiallySelectedAttachments;
}

/// <summary>
/// Provides the available tool definitions and current selection to the picker.
/// </summary>
public sealed class AgentToolSelectionRequestedEventArgs(
    IReadOnlyList<ToolDefinition> toolDefinitions,
    IReadOnlyList<Guid> initiallySelectedToolDefinitionIds) : EventArgs
{
    /// <summary>
    /// Gets the available tool definitions.
    /// </summary>
    public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } = toolDefinitions;

    /// <summary>
    /// Gets the tool definitions selected before opening the picker.
    /// </summary>
    public IReadOnlyList<Guid> InitiallySelectedToolDefinitionIds { get; } = initiallySelectedToolDefinitionIds;
}
