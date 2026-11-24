using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Mcp;
using Sedgewright.Views.Mcp;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Displays the MCP editor window as a modal dialog.
/// </summary>
public sealed class McpDefinitionDialogService : IMcpDefinitionDialogService
{
    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowAsync(McpDefinitionEditorViewModel viewModel)
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        Window owner = desktopLifetime?.MainWindow ?? throw new System.InvalidOperationException("Main window is not available.");

        McpDefinitionEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }
}
