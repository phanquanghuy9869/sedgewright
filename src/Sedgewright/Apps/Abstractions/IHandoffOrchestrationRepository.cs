using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes handoff orchestration rows.
/// </summary>
public interface IHandoffOrchestrationRepository
{
    /// <summary>
    /// Gets all handoff orchestrations with their participants and routes loaded.
    /// </summary>
    Task<List<HandoffOrchestration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one handoff orchestration by identifier with its graph loaded.
    /// </summary>
    Task<HandoffOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a handoff orchestration name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new handoff orchestration.
    /// </summary>
    void Create(HandoffOrchestration orchestration);

    /// <summary>
    /// Updates an existing handoff orchestration.
    /// </summary>
    void Update(HandoffOrchestration orchestration);

    /// <summary>
    /// Removes a handoff orchestration.
    /// </summary>
    void Delete(HandoffOrchestration orchestration);

    /// <summary>
    /// Saves pending handoff orchestration changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}