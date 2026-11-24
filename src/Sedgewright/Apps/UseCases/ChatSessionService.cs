using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Coordinates persisted chat selection and agent-session lifecycle operations.
/// </summary>
public sealed class ChatSessionService(
    ISharedState sharedState,
    IChatTargetCatalogService targetCatalogService,
    IAgentDefinitionRepository agentDefinitionRepository,
    IAgentSessionRepository agentSessionRepository) : IChatSessionService
{
    private readonly ISharedState _sharedState = sharedState;
    private readonly IChatTargetCatalogService _targetCatalogService = targetCatalogService;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository = agentDefinitionRepository;
    private readonly IAgentSessionRepository _agentSessionRepository = agentSessionRepository;

    /// <inheritdoc />
    public async Task<Result<IChatTarget?, IApplicationError>> RestoreLastTargetAsync(
        CancellationToken cancellationToken = default)
    {
        ApplicationSettings? settings = _sharedState.ApplicationSettings;
        if (settings is null)
        {
            return Result.Failure<IChatTarget?, IApplicationError>(
                new InvalidOperationError(["Application settings have not been loaded."]));
        }

        if (settings.LastChatTargetType is null && settings.LastChatTargetId is null)
        {
            return Result.Success<IChatTarget?, IApplicationError>(null);
        }

        if (settings.LastChatTargetType is null || settings.LastChatTargetId is null)
        {
            return Result.Failure<IChatTarget?, IApplicationError>(
                new InvalidOperationError(["The saved chat target identity is incomplete."]));
        }

        ChatTargetIdentity savedIdentity = new(settings.LastChatTargetType.Value, settings.LastChatTargetId.Value);
        System.Collections.Generic.IReadOnlyList<IChatTarget> targets = await _targetCatalogService
            .GetTargetsAsync(cancellationToken);
        IChatTarget? target = targets.FirstOrDefault(savedIdentity.Matches);
        if (target is null)
        {
            return Result.Failure<IChatTarget?, IApplicationError>(
                new InvalidOperationError(["The previously selected chat target is no longer available."]));
        }

        if (target is AgentDefinition agentDefinition)
        {
            if (settings.LastAgentSessionId is not int sessionId)
            {
                return Result.Failure<IChatTarget?, IApplicationError>(
                    new InvalidOperationError(["The previously selected agent has no saved session to resume."]));
            }

            Result<AgentRuntime, IApplicationError> restoreResult = await RestoreAgentSessionAsync(
                sessionId,
                agentDefinition.Id,
                cancellationToken);
            if (restoreResult.IsFailure)
            {
                return Result.Failure<IChatTarget?, IApplicationError>(restoreResult.Error);
            }
        }

        return Result.Success<IChatTarget?, IApplicationError>(target);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> SaveLastTargetAsync(
        IChatTarget? target,
        CancellationToken cancellationToken = default)
    {
        ApplicationSettings? currentSettings = _sharedState.ApplicationSettings;
        if (currentSettings is null)
        {
            return UnitResult.Failure<IApplicationError>(
                new InvalidOperationError(["Application settings have not been loaded."]));
        }

        ChatTargetIdentity? identity = target is null ? null : ChatTargetIdentity.From(target);
        int? sessionId = null;
        if (target is AgentDefinition agentDefinition)
        {
            if (!_sharedState.AgentRuntime.HasValue || _sharedState.AgentRuntime.Value.AgentRecordId == 0)
            {
                return UnitResult.Failure<IApplicationError>(
                    new InvalidOperationError(["The selected agent has no active persisted session."]));
            }

            sessionId = _sharedState.AgentRuntime.Value.AgentRecordId;
            AgentSessionRecord? activeSession = await _agentSessionRepository.GetAsync(sessionId.Value, cancellationToken);
            if (activeSession is null || activeSession.AgentDefinitionId != agentDefinition.Id)
            {
                return UnitResult.Failure<IApplicationError>(
                    new InvalidOperationError(["The active session does not belong to the selected agent."]));
            }
        }

        ApplicationSettings updatedSettings = new()
        {
            Id = currentSettings.Id,
            DefaultWorkingDirectory = currentSettings.DefaultWorkingDirectory,
            ThemeMode = currentSettings.ThemeMode,
            LastChatTargetType = identity?.TargetType,
            LastChatTargetId = identity?.Id,
            LastAgentSessionId = sessionId,
        };

        await _sharedState.SaveApplicationSettingsAsync(updatedSettings, cancellationToken);
        return UnitResult.Success<IApplicationError>();
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> CreateNewAgentSessionAsync(
        AgentDefinition agentDefinition,
        CancellationToken cancellationToken = default)
    {
        AgentDefinition? currentDefinition = await _agentDefinitionRepository.GetAsync(
            agentDefinition.Id,
            cancellationToken);
        if (currentDefinition is null)
        {
            return UnitResult.Failure<IApplicationError>(
                new InvalidOperationError(["The selected agent is no longer available."]));
        }

        Result<AgentRuntime, IApplicationError> buildResult = await AIAgent.From(currentDefinition, cancellationToken);
        if (buildResult.IsFailure)
        {
            return UnitResult.Failure<IApplicationError>(buildResult.Error);
        }

        AgentRuntime runtime = buildResult.Value;
        bool ownershipTransferred = false;
        try
        {
            JsonElement serializedSession = await runtime.Agent
                .SerializeSessionAsync(runtime.AgentSession, cancellationToken: cancellationToken);
            DateTime now = DateTime.UtcNow;
            AgentSessionRecord record = new()
            {
                AgentDefinitionId = currentDefinition.Id,
                Summary = $"Last used at {now:u}",
                SerializedSessionJson = serializedSession.GetRawText(),
                CreatedAtUtc = now,
                LastUsedAtUtc = now,
            };

            _agentSessionRepository.Create(record);
            await _agentSessionRepository.SaveChangesAsync(cancellationToken);
            runtime.SetAgentRecordId(record.Id);
            await _sharedState.SetAgentRuntimeAsync(runtime);
            ownershipTransferred = true;
            return UnitResult.Success<IApplicationError>();
        }
        finally
        {
            if (!ownershipTransferred)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<AgentRuntime, IApplicationError>> RestoreAgentSessionAsync(
        int sessionId,
        int agentDefinitionId,
        CancellationToken cancellationToken = default)
    {
        AgentSessionRecord? sessionRecord = await _agentSessionRepository.GetAsync(sessionId, cancellationToken);
        if (sessionRecord is null)
        {
            return Result.Failure<AgentRuntime, IApplicationError>(
                new InvalidOperationError(["The saved agent session is no longer available."]));
        }

        if (sessionRecord.AgentDefinitionId != agentDefinitionId)
        {
            return Result.Failure<AgentRuntime, IApplicationError>(
                new InvalidOperationError(["The saved session does not belong to the selected agent."]));
        }

        AgentDefinition? agentDefinition = await _agentDefinitionRepository.GetAsync(
            sessionRecord.AgentDefinitionId,
            cancellationToken);
        if (agentDefinition is null)
        {
            return Result.Failure<AgentRuntime, IApplicationError>(
                new InvalidOperationError(["The agent definition for the saved session is no longer available."]));
        }

        Result<AgentRuntime, IApplicationError> buildResult = await AIAgent.From(agentDefinition, cancellationToken);
        if (buildResult.IsFailure)
        {
            return buildResult;
        }

        AgentRuntime runtime = buildResult.Value;
        bool ownershipTransferred = false;
        try
        {
            using JsonDocument serializedSession = JsonDocument.Parse(sessionRecord.SerializedSessionJson);
            AgentSession restoredSession = await runtime.Agent.DeserializeSessionAsync(
                serializedSession.RootElement,
                cancellationToken: cancellationToken);
            runtime.SetAgentRecordId(sessionRecord.Id);
            runtime.SetAgentSession(restoredSession);
            await _sharedState.SetAgentRuntimeAsync(runtime);
            ownershipTransferred = true;
            return Result.Success<AgentRuntime, IApplicationError>(runtime);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                await runtime.DisposeAsync();
            }
        }
    }
}
