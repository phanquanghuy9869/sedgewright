using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations.Magentic;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Orchestrations.Magentic;

/// <summary>
/// Borderless editor window for Magentic orchestrations.
/// </summary>
public partial class MagenticOrchestrationEditorWindow : Window
{
    private MagenticOrchestrationEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor window.
    /// </summary>
    public MagenticOrchestrationEditorWindow()
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

        _viewModel = DataContext as MagenticOrchestrationEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
