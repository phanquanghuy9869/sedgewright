using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes sequential orchestration rows.
/// </summary>
public interface ISequentialOrchestrationRepository
{
    /// <summary>
    /// Gets all sequential orchestrations with their step graphs loaded.
    /// </summary>
    Task<List<SequentialOrchestration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one sequential orchestration by identifier with its step graph loaded.
    /// </summary>
    Task<SequentialOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a sequential orchestration name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new sequential orchestration.
    /// </summary>
    void Create(SequentialOrchestration orchestration);

    /// <summary>
    /// Updates an existing sequential orchestration.
    /// </summary>
    void Update(SequentialOrchestration orchestration);

    /// <summary>
    /// Removes a sequential orchestration.
    /// </summary>
    void Delete(SequentialOrchestration orchestration);

    /// <summary>
    /// Saves pending sequential orchestration changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
