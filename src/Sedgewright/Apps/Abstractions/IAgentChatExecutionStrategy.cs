using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Runs a single-agent chat turn.
/// </summary>
public interface IAgentChatExecutionStrategy
{
    /// <summary>
    /// Executes a chat turn for one agent using the supplied thread.
    /// </summary>
    Task<UnitResult<IApplicationError>> Execute(
        AIAgent agent,
        AgentSession session,
        List<ChatMessage> messages,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
        CancellationToken cancellationToken = default);
}
