
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Streams a single agent chat turn using the Microsoft Agent Framework.
/// </summary>
public sealed class AgentChatExecutionStrategy(ILogger<AgentChatExecutionStrategy> logger) : IAgentChatExecutionStrategy
{
    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> Execute(
        AIAgent agent,
        AgentSession session,
        List<ChatMessage> messages,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
        CancellationToken cancellationToken = default)
    {
        List<string> chunks = [];
        List<ChatMessage> nextMessages = messages;
        string source = agent.Name ?? "Unknown Agent";

        try
        {
            AgentRunResult runResult;

            while (true)
            {
                runResult = await RunAgentTurnAsync(
                    agent,
                    session,
                    nextMessages,
                    chunks,
                    source,
                    onIntermediateUpdate,
                    cancellationToken).ConfigureAwait(false);

                if (runResult.ApprovalRequests.Count > 0)
                {
                    nextMessages = await CreateApprovalResponsesAsync(
                        runResult.ApprovalRequests,
                        onApprovalRequired,
                        source,
                        onIntermediateUpdate,
                        logger).ConfigureAwait(false);
                    continue;
                }
                break;
            }

            await PublishCompletedTurnAsync(
                runResult.LastUpdate,
                chunks,
                agent,
                source,
                onIntermediateUpdate,
                onFinalOutput).ConfigureAwait(false);
            return UnitResult.Success<IApplicationError>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnitResult.Failure<IApplicationError>(new ExceptionError([exception.Message], exception));
        }
    }

    private static async Task<AgentRunResult> RunAgentTurnAsync(
        AIAgent agent,
        AgentSession session,
        List<ChatMessage> messages,
        List<string> chunks,
        string source,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate,
        CancellationToken cancellationToken)
    {
        AgentResponseUpdate? lastUpdate = null;
        List<ToolApprovalRequestContent> approvalRequests = [];
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            lastUpdate = update;

            foreach (AIContent content in update.Contents)
            {
                if (content is ToolApprovalRequestContent approvalRequest)
                {
                    approvalRequests.Add(approvalRequest);
                    continue;
                }

                if (content is TextContent textContent)
                {
                    if (string.IsNullOrEmpty(textContent.Text))
                    {
                        continue;
                    }

                    chunks.Add(textContent.Text);

                    if (onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            source,
                            textContent.Text,
                            false,
                            ExecutorId: update.AgentId,
                            ResponseId: update.ResponseId,
                            MessageId: update.MessageId)).ConfigureAwait(false);
                    }

                    continue;
                }

                IntermediateUpdate? activityUpdate = content.ToActivityUpdate(
                    source,
                    update.AgentId,
                    update.ResponseId,
                    update.MessageId);
                if (activityUpdate is not null && onIntermediateUpdate is not null)
                {
                    await onIntermediateUpdate(activityUpdate).ConfigureAwait(false);
                }
            }
        }

        return new AgentRunResult(lastUpdate, approvalRequests);
    }

    private static async Task<List<ChatMessage>> CreateApprovalResponsesAsync(
        List<ToolApprovalRequestContent> approvalRequests,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired,
        string source,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate,
        ILogger logger)
    {
        List<ChatMessage> responses = [];

        foreach (ToolApprovalRequestContent approvalRequest in approvalRequests)
        {
            ToolApprovalRequestModel approvalModel = ToolApprovalRequestModel.From(approvalRequest);
            logger.LogInformation(
                "Tool approval requested for {ToolName}; request {RequestId}, call {CallId}.",
                approvalModel.ToolName,
                approvalModel.RequestId,
                approvalModel.CallId);
            // Temporarily bypass real user approval; the callback flow can be restored when needed.
            // bool isApproved = onApprovalRequired is null
            //     ? true
            //     : await onApprovalRequired(approvalModel).ConfigureAwait(false);
            bool isApproved = true;
            logger.LogInformation(
                "Tool approval decision for {ToolName}; request {RequestId}, call {CallId}: {IsApproved}.",
                approvalModel.ToolName,
                approvalModel.RequestId,
                approvalModel.CallId,
                isApproved);

            if (onIntermediateUpdate is not null)
            {
                await onIntermediateUpdate(new IntermediateUpdate(
                    source,
                    $"{approvalModel.ToolName} was automatically approved.",
                    false,
                    IntermediateUpdateKind.ApprovalAutoApproved,
                    CallId: approvalModel.CallId)).ConfigureAwait(false);
            }

            responses.Add(new ChatMessage(ChatRole.User, [approvalRequest.CreateResponse(isApproved)]));
        }

        return responses;
    }

    private static async Task PublishCompletedTurnAsync(
        AgentResponseUpdate? lastUpdate,
        List<string> chunks,
        AIAgent agent,
        string source,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate,
        Func<List<MessageModel>, Task>? onFinalOutput)
    {
        await PublishFinalOutputAsync(chunks, agent, onFinalOutput).ConfigureAwait(false);
        if (lastUpdate is not null && onIntermediateUpdate is not null)
        {
            string? finishReason = lastUpdate.FinishReason?.ToString();
            await onIntermediateUpdate(new IntermediateUpdate(
                source,
                "Agent response finished.",
                false,
                IntermediateUpdateKind.ResponseCompleted,
                lastUpdate.AgentId,
                lastUpdate.ResponseId,
                lastUpdate.MessageId,
                Details: string.IsNullOrWhiteSpace(finishReason) ? null : $"Finish reason: {finishReason}")).ConfigureAwait(false);
        }
    }

    private static Task PublishFinalOutputAsync(
        List<string> chunks,
        AIAgent agent,
        Func<List<MessageModel>, Task>? onFinalOutput)
    {
        if (onFinalOutput is null)
        {
            return Task.CompletedTask;
        }

        string aggregatedText = string.Concat(chunks);
        return onFinalOutput([new MessageModel(false, aggregatedText, DateTime.UtcNow, agent.Name)]);
    }

    private sealed record AgentRunResult(
        AgentResponseUpdate? LastUpdate,
        List<ToolApprovalRequestContent> ApprovalRequests);
}
