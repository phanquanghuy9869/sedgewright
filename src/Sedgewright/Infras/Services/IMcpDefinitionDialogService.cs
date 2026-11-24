using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Mcp;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Opens the modal editor for an MCP definition.
/// </summary>
public interface IMcpDefinitionDialogService
{
    /// <summary>
    /// Shows the editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowAsync(McpDefinitionEditorViewModel viewModel);
}
