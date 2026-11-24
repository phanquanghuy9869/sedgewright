using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.AI;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Resolves a chat target and forwards the request to the correct execution path.
/// </summary>
public sealed class ChatService(
    IAgentDefinitionRepository agentRepository,
    ISequentialOrchestrationRepository sequentialOrchestrationRepository,
    IConcurrentOrchestrationRepository concurrentOrchestrationRepository,
    IGroupChatOrchestrationRepository groupChatOrchestrationRepository,
    IMagenticOrchestrationRepository magenticOrchestrationRepository,
    IHandoffOrchestrationRepository handoffOrchestrationRepository,
    IAgentSessionRepository agentSessionRepository,
    ISharedState sharedState,
    AgentChatUseCases agentChatUseCases,
    OrchestrationUseCases orchestrationUseCases) : IChatService
{
    private readonly IAgentDefinitionRepository _agentRepository = agentRepository;
    private readonly ISequentialOrchestrationRepository _sequentialOrchestrationRepository = sequentialOrchestrationRepository;
    private readonly IConcurrentOrchestrationRepository _concurrentOrchestrationRepository = concurrentOrchestrationRepository;
    private readonly IGroupChatOrchestrationRepository _groupChatOrchestrationRepository = groupChatOrchestrationRepository;
    private readonly IMagenticOrchestrationRepository _magenticOrchestrationRepository = magenticOrchestrationRepository;
    private readonly IHandoffOrchestrationRepository _handoffOrchestrationRepository = handoffOrchestrationRepository;
    private readonly IAgentSessionRepository _agentSessionRepository = agentSessionRepository;
    private readonly ISharedState _sharedState = sharedState;
    private readonly AgentChatUseCases _agentChatUseCases = agentChatUseCases;
    private readonly OrchestrationUseCases _orchestrationUseCases = orchestrationUseCases;

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> SendAsync(
        IChatTarget selectedTarget,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
        CancellationToken cancellationToken = default)
    {
        IChatTarget? resolvedTarget = await ResolveChatTargetAsync(selectedTarget, cancellationToken);
        if (resolvedTarget is null)
        {
            return new InvalidOperationError(["Selected chat target is no longer available."]);
        }

        if (resolvedTarget.IsAgent(out _))
        {
            if (!_sharedState.AgentRuntime.HasValue)
            {
                return new InvalidOperationError(["Agent chat requires an active session."]);
            }

            AgentRuntime activeRuntime = _sharedState.AgentRuntime.Value;
            List<ChatMessage> messages = [new(ChatRole.User, prompt)];

            UnitResult<IApplicationError> result = await _agentChatUseCases.Execute(
                activeRuntime.Agent,
                activeRuntime.AgentSession,
                messages,
                onIntermediateUpdate,
                onFinalOutput,
                onApprovalRequired,
                cancellationToken);

            if (result.IsSuccess)
            {
                AgentSessionRecord? sessionRecord = await _agentSessionRepository.GetAsync(activeRuntime.AgentRecordId, cancellationToken).ConfigureAwait(false);
                if (sessionRecord is null)
                {
                    return new InvalidOperationError(["Agent session is no longer available."]);
                }

                JsonElement serializedSession = await activeRuntime.Agent
                    .SerializeSessionAsync(activeRuntime.AgentSession, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                sessionRecord.SerializedSessionJson = serializedSession.GetRawText();
                sessionRecord.LastUsedAtUtc = DateTime.UtcNow;

                _agentSessionRepository.Update(sessionRecord);
                await _agentSessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        if (resolvedTarget.IsOrchestration(out IOrchestration? orchestration) && orchestration is not null)
        {
            UnitResult<IApplicationError> result = await _orchestrationUseCases.Execute(
                orchestration,
                prompt,
                onIntermediateUpdate,
                onFinalOutput,
                cancellationToken);

            return result;
        }

        return UnitResult.Failure<IApplicationError>(new InvalidOperationError(["Unsupported chat target."]));
    }

    /// <inheritdoc />
    public async Task<Result<List<MessageModel>, IApplicationError>> GetSelectedAgentSessionMessagesAsync(
        IChatTarget selectedTarget,
        CancellationToken cancellationToken = default)
    {
        if (!selectedTarget.IsAgent(out _))
        {
            return Result.Failure<List<MessageModel>, IApplicationError>(new InvalidOperationError(["Selected chat target is not an agent."]));
        }

        IChatTarget? resolvedTarget = await ResolveChatTargetAsync(selectedTarget, cancellationToken);
        if (resolvedTarget is null)
        {
            return Result.Failure<List<MessageModel>, IApplicationError>(new InvalidOperationError(["Selected chat target is no longer available."]));
        }

        if (!_sharedState.AgentRuntime.HasValue)
        {
            return Result.Failure<List<MessageModel>, IApplicationError>(new InvalidOperationError(["Agent chat requires an active session."]));
        }

        AgentRuntime activeRuntime = _sharedState.AgentRuntime.Value;
        Result<IReadOnlyCollection<ChatMessage>, IApplicationError> messagesResult = await activeRuntime
            .GetMessages(cancellationToken)
            .ConfigureAwait(false);
        if (messagesResult.IsFailure)
        {
            return Result.Failure<List<MessageModel>, IApplicationError>(messagesResult.Error);
        }

        List<MessageModel> messages = [];
        foreach (ChatMessage message in messagesResult.Value)
        {
            messages.Add(ConvertChatMessage(message));
        }

        return Result.Success<List<MessageModel>, IApplicationError>(messages);
    }

    private async Task<IChatTarget?> ResolveChatTargetAsync(IChatTarget selectedTarget, CancellationToken cancellationToken)
        => selectedTarget switch
    {
        AgentDefinition agent => await _agentRepository.GetAsync(agent.Id, cancellationToken),
        SequentialOrchestration orchestration => await _sequentialOrchestrationRepository.GetAsync(orchestration.Id, cancellationToken),
        ConcurrentOrchestration orchestration => await _concurrentOrchestrationRepository.GetAsync(orchestration.Id, cancellationToken),
        GroupChatOrchestration orchestration => await _groupChatOrchestrationRepository.GetAsync(orchestration.Id, cancellationToken),
        MagenticOrchestration orchestration => await _magenticOrchestrationRepository.GetAsync(orchestration.Id, cancellationToken),
        HandoffOrchestration orchestration => await _handoffOrchestrationRepository.GetAsync(orchestration.Id, cancellationToken),
        _ => null,
    };

    private static MessageModel ConvertChatMessage(ChatMessage message)
    {
        string roleText = GetPropertyTextOrNull(message, "Role");
        bool isUser = string.Equals(roleText, "User", StringComparison.OrdinalIgnoreCase);
        string content = ExtractMessageText(message);

        string? authorLabel = GetPropertyTextOrNull(message, "AuthorName");
        if (string.IsNullOrWhiteSpace(authorLabel))
        {
            authorLabel = string.IsNullOrWhiteSpace(roleText) ? null : roleText;
        }

        return new MessageModel(isUser, content, DateTime.UtcNow, authorLabel);
    }

    private static string ExtractMessageText(object message)
    {
        string text = GetPropertyTextOrNull(message, "Text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        text = GetPropertyTextOrNull(message, "Content");
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        object? contentsValue = GetPropertyValue(message, "Contents");
        if (contentsValue is System.Collections.IEnumerable contents)
        {
            List<string> contentParts = [];
            foreach (object? contentItem in contents)
            {
                string contentText = contentItem?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(contentText))
                {
                    contentParts.Add(contentText);
                }
            }

            if (contentParts.Count > 0)
            {
                return string.Join(Environment.NewLine, contentParts);
            }
        }

        return message.ToString() ?? string.Empty;
    }

    private static string GetPropertyTextOrNull(object instance, string propertyName)
        => GetPropertyValue(instance, propertyName)?.ToString() ?? string.Empty;

    private static object? GetPropertyValue(object instance, string propertyName)
        => instance.GetType().GetProperty(propertyName)?.GetValue(instance);
}
