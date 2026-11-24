using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes persisted agent session records.
/// </summary>
public interface IAgentSessionRepository
{
    /// <summary>
    /// Gets all saved sessions ordered by most recently used first.
    /// </summary>
    Task<List<AgentSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one saved session by identifier.
    /// </summary>
    Task<AgentSessionRecord?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new saved session.
    /// </summary>
    void Create(AgentSessionRecord session);

    /// <summary>
    /// Updates an existing saved session.
    /// </summary>
    void Update(AgentSessionRecord session);

    /// <summary>
    /// Removes a saved session.
    /// </summary>
    void Delete(AgentSessionRecord session);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
