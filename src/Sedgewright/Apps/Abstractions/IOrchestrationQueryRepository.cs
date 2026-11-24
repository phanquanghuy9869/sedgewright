using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Loads orchestration snapshots across all orchestration tables.
/// </summary>
public interface IOrchestrationQueryRepository
{
    /// <summary>
    /// Gets the combined orchestration list ordered for the UI.
    /// </summary>
    Task<List<OrchestrationListItemSnapshot>> GetAllAsync(CancellationToken cancellationToken = default);
}