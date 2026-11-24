using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes concurrent orchestration rows.
/// </summary>
public interface IConcurrentOrchestrationRepository
{
    /// <summary>
    /// Gets all concurrent orchestrations with their participants loaded.
    /// </summary>
    Task<List<ConcurrentOrchestration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one concurrent orchestration by identifier with its participant graph loaded.
    /// </summary>
    Task<ConcurrentOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a concurrent orchestration name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new concurrent orchestration.
    /// </summary>
    void Create(ConcurrentOrchestration orchestration);

    /// <summary>
    /// Updates an existing concurrent orchestration.
    /// </summary>
    void Update(ConcurrentOrchestration orchestration);

    /// <summary>
    /// Removes a concurrent orchestration.
    /// </summary>
    void Delete(ConcurrentOrchestration orchestration);

    /// <summary>
    /// Saves pending concurrent orchestration changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
