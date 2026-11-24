using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Loads the raw chat targets that can be selected in the chat picker.
/// </summary>
public interface IChatTargetCatalogService
{
    /// <summary>
    /// Gets all available chat targets as raw entities.
    /// </summary>
    Task<IReadOnlyList<IChatTarget>> GetTargetsAsync(CancellationToken cancellationToken = default);
}
