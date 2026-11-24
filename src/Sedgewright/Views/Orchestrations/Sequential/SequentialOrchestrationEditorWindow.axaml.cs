using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations.Sequential;

namespace Sedgewright.Views.Orchestrations.Sequential;

/// <summary>
/// Borderless editor for sequential orchestrations.
/// </summary>
public partial class SequentialOrchestrationEditorWindow : Window
{
    private SequentialOrchestrationEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor window.
    /// </summary>
    public SequentialOrchestrationEditorWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as SequentialOrchestrationEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
