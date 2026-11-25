using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Runs a persisted <see cref="ScheduledJob"/> without any UI. Agents get a fresh, persisted session per run, and
/// orchestrations run through <see cref="OrchestrationUseCases"/>. Every run is recorded as a <see cref="ScheduledJobRun"/>.
/// </summary>
public sealed class ScheduledJobRunner(
    IScheduledJobRepository scheduledJobRepository,
    ScheduledJobTargetResolver targetResolver,
    IAgentSessionRepository agentSessionRepository,
    IScheduledJobScheduler scheduler,
    AgentChatUseCases agentChatUseCases,
    OrchestrationUseCases orchestrationUseCases,
    TimeProvider timeProvider,
    ILogger<ScheduledJobRunner> logger) : IScheduledJobRunner
{
    private const int MaxSummaryLength = 1000;

    /// <inheritdoc />
    public async Task<Result<ScheduledJobRun?, IApplicationError>> RunAsync(
        int scheduledJobId,
        CancellationToken cancellationToken = default)
    {
        ScheduledJob? job = await scheduledJobRepository.GetAsync(scheduledJobId, cancellationToken).ConfigureAwait(false);
        if (job is null || !job.IsEnabled)
        {
            logger.LogInformation("Scheduled job {ScheduledJobId} is missing or disabled; nothing to run.", scheduledJobId);
            return Result.Success<ScheduledJobRun?, IApplicationError>(null);
        }

        ScheduledJobRun run = new()
        {
            ScheduledJobId = job.Id,
            StartedAtUtc = UtcNow(),
            Status = ScheduledJobRunStatus.Running,
        };

        try
        {
            scheduledJobRepository.CreateRun(run);
            await scheduledJobRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<ScheduledJobRun?, IApplicationError>(
                new ExceptionError([$"Could not record the start of scheduled job {job.Id}."], exception));
        }

        bool targetMissing = false;
        try
        {
            IChatTarget? target = await targetResolver
                .ResolveAsync(job.TargetType, job.TargetId, cancellationToken)
                .ConfigureAwait(false);
            if (target is null)
            {
                targetMissing = true;
                Complete(run, ScheduledJobRunStatus.Failed, error: $"{job.TargetType} {job.TargetId} no longer exists; the job was disabled.");
            }
            else
            {
                Result<string, IApplicationError> outcome = await ExecuteAsync(job, target, run, cancellationToken).ConfigureAwait(false);
                if (outcome.IsSuccess)
                {
                    Complete(run, ScheduledJobRunStatus.Succeeded, output: outcome.Value);
                }
                else
                {
                    Complete(run, ScheduledJobRunStatus.Failed, error: string.Join(Environment.NewLine, outcome.Error.Messages));
                }
            }
        }
        catch (OperationCanceledException)
        {
            Complete(run, ScheduledJobRunStatus.Cancelled, error: "The run was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled job {ScheduledJobId} failed unexpectedly.", job.Id);
            Complete(run, ScheduledJobRunStatus.Failed, error: exception.Message);
        }

        job.LastRunAtUtc = run.StartedAtUtc;
        job.LastRunStatus = run.Status;
        if (targetMissing)
        {
            job.IsEnabled = false;
            job.UpdatedAtUtc = UtcNow();
        }

        try
        {
            // Persist the outcome even when the run itself was cancelled.
            await scheduledJobRepository.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return Result.Failure<ScheduledJobRun?, IApplicationError>(
                new ExceptionError([$"Could not record the outcome of scheduled job {job.Id}."], exception));
        }

        if (targetMissing)
        {
            UnitResult<IApplicationError> unscheduleResult = await scheduler
                .UnscheduleAsync(job.Id, CancellationToken.None)
                .ConfigureAwait(false);
            if (unscheduleResult.IsFailure)
            {
                logger.LogWarning("Could not unschedule job {ScheduledJobId}: {Errors}", job.Id, unscheduleResult.Error.Messages);
            }
        }

        return Result.Success<ScheduledJobRun?, IApplicationError>(run);
    }

    private Task<Result<string, IApplicationError>> ExecuteAsync(
        ScheduledJob job,
        IChatTarget target,
        ScheduledJobRun run,
        CancellationToken cancellationToken)
        => target switch
        {
            AgentDefinition agentDefinition => ExecuteAgentAsync(job, agentDefinition, run, cancellationToken),
            IOrchestration orchestration => ExecuteOrchestrationAsync(job, orchestration, cancellationToken),
            _ => Task.FromResult(Result.Failure<string, IApplicationError>(
                new InvalidOperationError([$"Unsupported target type {target.GetType().Name}."]))),
        };

    private async Task<Result<string, IApplicationError>> ExecuteAgentAsync(
        ScheduledJob job,
        AgentDefinition agentDefinition,
        ScheduledJobRun run,
        CancellationToken cancellationToken)
    {
        Result<AgentRuntime, IApplicationError> buildResult = await AIAgent
            .From(agentDefinition, cancellationToken)
            .ConfigureAwait(false);
        if (buildResult.IsFailure)
        {
            return Result.Failure<string, IApplicationError>(buildResult.Error);
        }

        await using AgentRuntime runtime = buildResult.Value;
        List<MessageModel> finalMessages = [];
        logger.LogInformation(
            "Scheduled job {ScheduledJobId} runs agent {AgentDefinitionId}; tool approvals are granted automatically.",
            job.Id,
            agentDefinition.Id);

        UnitResult<IApplicationError> result = await agentChatUseCases.Execute(
                runtime.Agent,
                runtime.AgentSession,
                [new ChatMessage(ChatRole.User, job.Prompt)],
                onFinalOutput: messages =>
                {
                    finalMessages.AddRange(messages);
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Persist the session even after a failure so the transcript can be inspected from Chat.
        JsonElement serializedSession = await runtime.Agent
            .SerializeSessionAsync(runtime.AgentSession, cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);
        DateTime now = UtcNow();
        AgentSessionRecord sessionRecord = new()
        {
            AgentDefinitionId = agentDefinition.Id,
            Summary = Truncate($"Scheduled: {job.Name} @ {now:u}", MaxSummaryLength),
            SerializedSessionJson = serializedSession.GetRawText(),
            CreatedAtUtc = now,
            LastUsedAtUtc = now,
        };
        agentSessionRepository.Create(sessionRecord);
        await agentSessionRepository.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        run.AgentSessionRecordId = sessionRecord.Id;

        cancellationToken.ThrowIfCancellationRequested();
        return result.Map(() => FormatOutput(finalMessages));
    }

    private async Task<Result<string, IApplicationError>> ExecuteOrchestrationAsync(
        ScheduledJob job,
        IOrchestration orchestration,
        CancellationToken cancellationToken)
    {
        List<MessageModel> finalMessages = [];
        logger.LogInformation(
            "Scheduled job {ScheduledJobId} runs {OrchestrationType} {TargetId}.",
            job.Id,
            orchestration.GetType().Name,
            job.TargetId);

        UnitResult<IApplicationError> result = await orchestrationUseCases.Execute(
                orchestration,
                job.Prompt,
                onFinalOutput: messages =>
                {
                    finalMessages.AddRange(messages);
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return result.Map(() => FormatOutput(finalMessages));
    }

    private static string FormatOutput(List<MessageModel> messages)
    {
        List<MessageModel> visible = [.. messages.Where(message => !message.IsUser && !message.IsToolActivity && !message.IsReasoning)];
        return visible.Count switch
        {
            0 => string.Empty,
            1 => visible[0].Content,
            _ => string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                visible.Select(message => $"[{message.AuthorLabel}]{Environment.NewLine}{message.Content}")),
        };
    }

    private void Complete(ScheduledJobRun run, ScheduledJobRunStatus status, string? output = null, string? error = null)
    {
        run.Status = status;
        run.CompletedAtUtc = UtcNow();
        run.Output = output;
        run.Error = error;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
