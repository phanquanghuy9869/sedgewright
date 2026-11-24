using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.ViewModels.Orchestrations.Concurrent;
using Sedgewright.ViewModels.Orchestrations.Sequential;
using Sedgewright.ViewModels.Orchestrations.GroupChat;
using Sedgewright.ViewModels.Orchestrations.Magentic;
using Sedgewright.ViewModels.Orchestrations.Handoff;
using Sedgewright.Views.Orchestrations;
using Sedgewright.Views.Orchestrations.Concurrent;
using Sedgewright.Views.Orchestrations.Sequential;
using Sedgewright.Views.Orchestrations.GroupChat;
using Sedgewright.Views.Orchestrations.Magentic;
using Sedgewright.Views.Orchestrations.Handoff;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Displays orchestration editor windows as modal dialogs.
/// </summary>
public sealed class OrchestrationDialogService : IOrchestrationDialogService
{
    /// <inheritdoc />
    public async Task<OrchestrationKind?> ShowKindPickerAsync(OrchestrationKindPickerViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        OrchestrationKindPickerWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<OrchestrationKind?>(owner);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowSequentialAsync(SequentialOrchestrationEditorViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        SequentialOrchestrationEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowConcurrentAsync(ConcurrentOrchestrationEditorViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        ConcurrentOrchestrationEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowGroupChatAsync(GroupChatOrchestrationEditorViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        GroupChatOrchestrationEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowMagenticAsync(MagenticOrchestrationEditorViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        MagenticOrchestrationEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }

    /// <inheritdoc />
    public async Task<UnitResult<IApplicationError>> ShowHandoffAsync(HandoffOrchestrationEditorViewModel viewModel)
    {
        Window owner = GetOwnerWindow();

        HandoffOrchestrationEditorWindow window = new()
        {
            DataContext = viewModel,
        };

        return await window.ShowDialog<UnitResult<IApplicationError>>(owner);
    }

    private static Window GetOwnerWindow()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktopLifetime?.MainWindow ?? throw new InvalidOperationException("Main window is not available.");
    }
}
