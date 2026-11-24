using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes Magentic orchestration rows.
/// </summary>
public interface IMagenticOrchestrationRepository
{
    /// <summary>
    /// Gets all Magentic orchestrations with their participants loaded.
    /// </summary>
    Task<List<MagenticOrchestration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one Magentic orchestration by identifier with its participant graph loaded.
    /// </summary>
    Task<MagenticOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a Magentic orchestration name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new Magentic orchestration.
    /// </summary>
    void Create(MagenticOrchestration orchestration);

    /// <summary>
    /// Updates an existing Magentic orchestration.
    /// </summary>
    void Update(MagenticOrchestration orchestration);

    /// <summary>
    /// Removes a Magentic orchestration.
    /// </summary>
    void Delete(MagenticOrchestration orchestration);

    /// <summary>
    /// Saves pending Magentic orchestration changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}