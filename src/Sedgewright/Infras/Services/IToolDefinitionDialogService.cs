using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Tools;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Opens the modal editor for a workflow tool definition.
/// </summary>
public interface IToolDefinitionDialogService
{
    /// <summary>
    /// Shows the editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowAsync(ToolDefinitionEditorViewModel viewModel);
}
