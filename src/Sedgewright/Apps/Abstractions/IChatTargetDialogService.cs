using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Opens the modal chat target picker.
/// </summary>
public interface IChatTargetDialogService
{
    /// <summary>
    /// Shows the picker and returns the selected target, or null when cancelled.
    /// </summary>
    Task<IChatTarget?> ShowAsync(IChatTarget? selectedTarget = null);
}
