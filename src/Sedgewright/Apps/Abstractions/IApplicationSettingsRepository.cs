using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Persists the singleton application settings record.
/// </summary>
public interface IApplicationSettingsRepository
{
    /// <summary>
    /// Gets the singleton settings record, creating it when it does not exist.
    /// </summary>
    Task<ApplicationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the singleton settings record.
    /// </summary>
    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
