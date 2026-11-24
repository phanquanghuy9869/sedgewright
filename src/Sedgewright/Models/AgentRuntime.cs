using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Tools.Shell;

namespace Sedgewright.Models;

/// <summary>
/// Owns the resources required by one active agent session.
/// </summary>
/// <remarks>
/// Creates an agent runtime.
/// </remarks>
public sealed class AgentRuntime(
    AIAgent agent,
    AgentSession agentSession,
    ShellExecutor? shellExecutor,
    IAsyncDisposable? ownedResource = null) : IAsyncDisposable
{
    private int _disposed;

    internal IAsyncDisposable? OwnedResource { get; } = ownedResource;

    /// <summary>
    /// Gets the agent used by this runtime.
    /// </summary>
    public AIAgent Agent { get; } = agent;

    /// <summary>
    /// Gets the session used by this runtime.
    /// </summary>
    public AgentSession AgentSession { get; private set; } = agentSession;

    /// <summary>
    /// Gets the persisted record identity associated with this runtime.
    /// </summary>
    public int AgentRecordId { get; private set; }

    /// <summary>
    /// Associates the runtime with its persisted session record.
    /// </summary>
    public void SetAgentRecordId(int agentRecordId)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        if (AgentRecordId != 0 && AgentRecordId != agentRecordId)
        {
            throw new InvalidOperationException("The agent runtime is already associated with a different session record.");
        }

        AgentRecordId = agentRecordId;
    }

    /// <summary>
    /// Replaces the session held by this runtime after restoring persisted state.
    /// </summary>
    public void SetAgentSession(AgentSession agentSession)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        AgentSession = agentSession;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (OwnedResource is not null)
            {
                await OwnedResource.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (shellExecutor is not null)
            {
                await shellExecutor.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
