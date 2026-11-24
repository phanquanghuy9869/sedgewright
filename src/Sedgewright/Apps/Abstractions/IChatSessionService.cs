using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Creates and restores agent sessions and remembers the last selected chat target.
/// </summary>
public interface IChatSessionService
{
    /// <summary>
    /// Restores the last selected target and its saved agent session, if one exists.
    /// </summary>
    Task<Result<IChatTarget?, IApplicationError>> RestoreLastTargetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the selected target and its active agent session, when applicable.
    /// </summary>
    Task<UnitResult<IApplicationError>> SaveLastTargetAsync(
        IChatTarget? target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new persisted session for an agent and makes it the active shared runtime.
    /// </summary>
    Task<UnitResult<IApplicationError>> CreateNewAgentSessionAsync(
        AgentDefinition agentDefinition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a persisted agent session and makes it the active shared runtime.
    /// </summary>
    Task<Result<AgentRuntime, IApplicationError>> RestoreAgentSessionAsync(
        int sessionId,
        int agentDefinitionId,
        CancellationToken cancellationToken = default);
}
