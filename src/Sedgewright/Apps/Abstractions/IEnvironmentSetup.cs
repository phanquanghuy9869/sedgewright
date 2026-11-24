using System.Threading;
using System.Threading.Tasks;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Initializes default application data and environment resources.
/// </summary>
public interface IEnvironmentSetup
{
    /// <summary>
    /// Initializes missing default data without overwriting existing user data.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
