using System;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Displays the Harness-specific settings for an agent definition.
/// </summary>
public partial class AgentHarnessOptionsSelectionWindow : Window
{
    private AgentHarnessOptionsSelectionViewModel? _viewModel;

    /// <summary>
    /// Creates the Harness options selection window.
    /// </summary>
    public AgentHarnessOptionsSelectionWindow()
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

        _viewModel = DataContext as AgentHarnessOptionsSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(
        object? sender,
        AgentHarnessOptionsSelectionCloseRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        Close(e.Accepted ? _viewModel.Selection : null);
    }
}
