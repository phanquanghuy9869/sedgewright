using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.ScheduledJobs;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Opens the scheduled job editor dialog.
/// </summary>
public interface IScheduledJobDialogService
{
    /// <summary>
    /// Shows the editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowAsync(ScheduledJobEditorViewModel viewModel);
}
