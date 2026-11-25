using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;
using Sedgewright.ViewModels.ScheduledJobs;
using Sedgewright.Views.ScheduledJobs;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Displays the scheduled job editor as a modal dialog.
/// </summary>
public sealed class ScheduledJobDialogService : IScheduledJobDialogService
{
    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowAsync(ScheduledJobEditorViewModel viewModel)
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetime =
            Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        Window owner = desktopLifetime?.MainWindow
            ?? throw new InvalidOperationException("Main window is not available.");

        ScheduledJobEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }
}
