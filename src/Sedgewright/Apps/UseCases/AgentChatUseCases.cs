using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Executes chat turns against a single agent.
/// </summary>
public sealed class AgentChatUseCases(IAgentChatExecutionStrategy strategy)
{
    private readonly IAgentChatExecutionStrategy _strategy = strategy;

    /// <summary>
    /// Executes a turn for one agent using the provided thread.
    /// </summary>
    public Task<UnitResult<IApplicationError>> Execute(
        AIAgent agent,
        AgentSession session,
        List<ChatMessage> messages,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
        CancellationToken cancellationToken = default)
        => _strategy.Execute(agent, session, messages, onIntermediateUpdate, onFinalOutput, onApprovalRequired, cancellationToken);
}
