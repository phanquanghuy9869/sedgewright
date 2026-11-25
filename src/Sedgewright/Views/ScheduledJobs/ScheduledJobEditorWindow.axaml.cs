using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.ScheduledJobs;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.ScheduledJobs;

/// <summary>
/// Modal editor for a persisted scheduled job.
/// </summary>
public partial class ScheduledJobEditorWindow : Window
{
    private ScheduledJobEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the scheduled job editor window.
    /// </summary>
    public ScheduledJobEditorWindow()
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

        _viewModel = DataContext as ScheduledJobEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
