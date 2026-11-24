using System;

namespace Sedgewright.Models;

/// <summary>
/// Describes a tool call that needs a user's approval before execution.
/// </summary>
public sealed record ToolApprovalRequestModel(
    string RequestId,
    string CallId,
    string ToolName,
    string Arguments)
{
    /// <summary>
    /// Gets the compact approval question shown in the chat surface.
    /// </summary>
    public string Summary => $"Approve {ToolName}?";

    /// <summary>
    /// Gets the tool call details shown alongside the approval actions.
    /// </summary>
    public string Details => string.IsNullOrWhiteSpace(Arguments)
        ? $"Call ID: {CallId}"
        : $"Arguments:\n{Arguments}\n\nCall ID: {CallId}";
}
