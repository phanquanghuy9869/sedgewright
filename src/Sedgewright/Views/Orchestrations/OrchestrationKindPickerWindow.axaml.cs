using System;
using Avalonia.Controls;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Orchestrations;

/// <summary>
/// Borderless picker for orchestration kinds.
/// </summary>
public partial class OrchestrationKindPickerWindow : Window
{
    private OrchestrationKindPickerViewModel? _viewModel;

    /// <summary>
    /// Creates the picker window.
    /// </summary>
    public OrchestrationKindPickerWindow()
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

        _viewModel = DataContext as OrchestrationKindPickerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(OrchestrationKind? kind)
        => Close(kind);
}
