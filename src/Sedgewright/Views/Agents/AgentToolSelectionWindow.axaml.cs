using System;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Displays reusable tool definitions for agent attachment selection.
/// </summary>
public partial class AgentToolSelectionWindow : Window
{
    private AgentToolSelectionViewModel? _viewModel;

    /// <summary>
    /// Creates the tool selection window.
    /// </summary>
    public AgentToolSelectionWindow()
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

        _viewModel = DataContext as AgentToolSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, AgentToolSelectionCloseRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        Close(e.Accepted ? _viewModel.SelectedToolDefinitionIds : null);
    }
}
