using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations.Handoff;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Orchestrations.Handoff;

/// <summary>
/// Borderless editor for handoff orchestrations.
/// </summary>
public partial class HandoffOrchestrationEditorWindow : Window
{
    private HandoffOrchestrationEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor window.
    /// </summary>
    public HandoffOrchestrationEditorWindow()
    {
        InitializeComponent();
        DialogWindowBehavior.Attach(this, TitleDragRegion);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as HandoffOrchestrationEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
