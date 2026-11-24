using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Tools;
using Sedgewright.Views.Tools;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Displays the workflow tool definition editor as a modal dialog.
/// </summary>
public sealed class ToolDefinitionDialogService : IToolDefinitionDialogService
{
    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowAsync(ToolDefinitionEditorViewModel viewModel)
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        Window owner = desktopLifetime?.MainWindow ?? throw new System.InvalidOperationException("Main window is not available.");

        ToolDefinitionEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }
}
