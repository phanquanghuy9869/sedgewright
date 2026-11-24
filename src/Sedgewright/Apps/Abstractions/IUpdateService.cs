using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Models.Updates;

namespace Sedgewright.Apps.Abstractions;

public interface IUpdateService
{
    string CurrentVersion { get; }

    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task ApplyUpdateAsync(CancellationToken cancellationToken = default);
}
