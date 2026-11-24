using System;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Displays available MCP definitions for agent attachment selection.
/// </summary>
public partial class AgentMcpSelectionWindow : Window
{
    private AgentMcpSelectionViewModel? _viewModel;

    /// <summary>
    /// Creates the MCP selection window.
    /// </summary>
    public AgentMcpSelectionWindow()
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

        _viewModel = DataContext as AgentMcpSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, AgentMcpSelectionCloseRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        Close(e.Accepted ? _viewModel.SelectedMcpIds : null);
    }
}
