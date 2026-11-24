using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations.Concurrent;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Orchestrations.Concurrent;

/// <summary>
/// Borderless editor window for concurrent orchestrations.
/// </summary>
public partial class ConcurrentOrchestrationEditorWindow : Window
{
    private ConcurrentOrchestrationEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor window.
    /// </summary>
    public ConcurrentOrchestrationEditorWindow()
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

        _viewModel = DataContext as ConcurrentOrchestrationEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
