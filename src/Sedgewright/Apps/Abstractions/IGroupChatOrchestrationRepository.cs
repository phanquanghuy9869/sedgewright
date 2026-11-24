using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes group chat orchestration rows.
/// </summary>
public interface IGroupChatOrchestrationRepository
{
    /// <summary>
    /// Gets all group chat orchestrations with their participants loaded.
    /// </summary>
    Task<List<GroupChatOrchestration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one group chat orchestration by identifier with its participant graph loaded.
    /// </summary>
    Task<GroupChatOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a group chat orchestration name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new group chat orchestration.
    /// </summary>
    void Create(GroupChatOrchestration orchestration);

    /// <summary>
    /// Updates an existing group chat orchestration.
    /// </summary>
    void Update(GroupChatOrchestration orchestration);

    /// <summary>
    /// Removes a group chat orchestration.
    /// </summary>
    void Delete(GroupChatOrchestration orchestration);

    /// <summary>
    /// Saves pending group chat orchestration changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
