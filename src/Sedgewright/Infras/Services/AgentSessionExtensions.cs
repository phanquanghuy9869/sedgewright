using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Shared state extensions for session creation and restoration workflows.
/// </summary>
public static class AgentSessionExtensions
{

    /// <summary>
    /// Restores a runtime session from the supplied persisted session record.
    /// </summary>
    public static async Task<Result<AgentRuntime, IApplicationError>> RestoreCurrentSessionAsync(
        this AgentSessionSnapshot sessionSnapshot,
        AgentSessionRecord sessionRecord,
        CancellationToken cancellationToken = default)
    {
        AgentRuntime? runtime = null;

        try
        {
            Result<AgentRuntime, IApplicationError> agentResult = await AIAgent
                .From(sessionRecord.AgentDefinition, cancellationToken)
                .ConfigureAwait(false);
            if (agentResult.IsFailure)
            {
                return Result.Failure<AgentRuntime, IApplicationError>(agentResult.Error);
            }

            runtime = agentResult.Value;
            using JsonDocument serializedSession = JsonDocument.Parse(sessionRecord.SerializedSessionJson);
            AgentSession session = await runtime.Agent.DeserializeSessionAsync(
                serializedSession.RootElement,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            runtime.SetAgentRecordId(sessionRecord.Id);
            runtime.SetAgentSession(session);

            AgentRuntime restoredRuntime = runtime;
            runtime = null;
            return Result.Success<AgentRuntime, IApplicationError>(restoredRuntime);
        }
        catch (Exception exception)
        {
            return Result.Failure<AgentRuntime, IApplicationError>(new ExceptionError(["Could not restore the selected agent session."], exception));
        }
        finally
        {
            if (runtime is not null)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
