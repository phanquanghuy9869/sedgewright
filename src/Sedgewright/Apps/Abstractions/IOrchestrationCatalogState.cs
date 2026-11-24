using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Exposes the shared orchestration list used by the chat picker and orchestration editors.
/// </summary>
public interface IOrchestrationCatalogState
{
    /// <summary>
    /// Gets the shared orchestration collection bound by the UI.
    /// </summary>
    ObservableCollection<OrchestrationListItemSnapshot> Orchestrations { get; }

    /// <summary>
    /// Reloads the shared orchestration collection from persistent storage.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}