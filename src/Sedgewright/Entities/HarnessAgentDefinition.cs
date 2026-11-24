using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Entities;

/// <summary>
/// An <see cref="AgentDefinition"/> that runs through the Microsoft Agent Framework
/// "Harness" pipeline (see doc/MAF/harness-learning.md), adding opt-in/opt-out toggles
/// for the pre-wired Harness capabilities such as todos, agent modes, file memory,
/// web search, tool auto-approval, agent skills, OpenTelemetry, context-window
/// compaction, file access, and sandboxed code execution.
/// </summary>
public sealed class HarnessAgentDefinition : AgentDefinition
{
    /// <summary>
    /// Disables the built-in todo/task-tracking provider. Enabled (false) by default,
    /// matching <c>HarnessAgentOptions.DisableTodoProvider</c>.
    /// </summary>
    public bool DisableTodoProvider { get; set; }

    /// <summary>
    /// Disables the plan/execute mode-switching provider. Enabled (false) by default,
    /// matching <c>HarnessAgentOptions.DisableAgentModeProvider</c>.
    /// </summary>
    public bool DisableAgentModeProvider { get; set; }

    /// <summary>
    /// Disables the durable file-based memory provider. Enabled (false) by default,
    /// matching <c>HarnessAgentOptions.DisableFileMemory</c>.
    /// </summary>
    public bool DisableFileMemory { get; set; }

    /// <summary>
    /// Disables the hosted web-search tool. Enabled (true) by default,
    /// matching <c>HarnessAgentOptions.DisableWebSearch</c>.
    /// </summary>
    public bool DisableWebSearch { get; set; } = true;

    /// <summary>
    /// Disables tool "don't ask again" auto-approval memory. Enabled (false) by default,
    /// matching <c>HarnessAgentOptions.DisableToolAutoApproval</c>.
    /// </summary>
    public bool DisableToolAutoApproval { get; set; }

    /// <summary>
    /// Disables discovery of SKILL.md-based skills. Enabled (true) by default,
    /// matching <c>HarnessAgentOptions.DisableAgentSkillsProvider</c>.
    /// </summary>
    public bool DisableAgentSkillsProvider { get; set; } = true;

    /// <summary>
    /// Disables GenAI semantic-convention OpenTelemetry tracing. Enabled (false) by
    /// default, matching <c>HarnessAgentOptions.DisableOpenTelemetry</c>.
    /// </summary>
    public bool DisableOpenTelemetry { get; set; }

    /// <summary>
    /// Maximum context-window tokens before in-loop compaction kicks in. Compaction only
    /// activates when both this and <see cref="MaxOutputTokens"/> are set.
    /// </summary>
    public int? MaxContextWindowTokens { get; set; }

    /// <summary>
    /// Maximum output tokens reserved when computing compaction thresholds. Compaction
    /// only activates when both this and <see cref="MaxContextWindowTokens"/> are set.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Working directory that enables the opt-in <c>FileAccessProvider</c>, letting the
    /// agent read/write local data files. Null means file access is disabled.
    /// </summary>
    public string? FileAccessWorkingDirectory { get; set; }

    /// <summary>
    /// Enables sandboxed code execution (CodeAct / Hyperlight micro-VM) so the agent can
    /// run code to compute or verify results. Opt-in, disabled by default.
    /// </summary>
    public bool EnableCodeExecution { get; set; }

    /// <inheritdoc />
    public override UnitResult<IApplicationError> IsValidDefinition()
    {
        List<string> errors = [];

        UnitResult<IApplicationError> baseResult = base.IsValidDefinition();
        if (baseResult.IsFailure && baseResult.Error is ValidationError validationError)
        {
            errors.AddRange(validationError.Messages);
        }

        if (MaxContextWindowTokens.HasValue != MaxOutputTokens.HasValue)
        {
            errors.Add("MaxContextWindowTokens and MaxOutputTokens must be set together to enable compaction.");
        }

        return UnitResult.FailureIf<IApplicationError>(errors.Count > 0, new ValidationError([.. errors]));
    }
}
