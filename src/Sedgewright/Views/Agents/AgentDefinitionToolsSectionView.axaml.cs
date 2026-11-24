using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders reusable tool-definition attachment selection for the agent editor.
/// </summary>
public partial class AgentDefinitionToolsSectionView : UserControl
{
    private AgentDefinitionToolsViewModel? _viewModel;

    /// <summary>
    /// Creates the tools section view.
    /// </summary>
    public AgentDefinitionToolsSectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ToolSelectionRequested -= OnToolSelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionToolsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ToolSelectionRequested += OnToolSelectionRequested;
        }
    }

    private async void OnToolSelectionRequested(
        object? sender,
        AgentToolSelectionRequestedEventArgs e)
    {
        if (sender is not AgentDefinitionToolsViewModel toolsViewModel)
        {
            return;
        }

        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        AgentToolSelectionWindow selectionWindow = new()
        {
            DataContext = new AgentToolSelectionViewModel(
                e.ToolDefinitions,
                e.InitiallySelectedToolDefinitionIds),
        };

        IReadOnlyList<Guid>? selectedToolDefinitionIds = await selectionWindow
            .ShowDialog<IReadOnlyList<Guid>?>(owner);
        if (selectedToolDefinitionIds is not null)
        {
            toolsViewModel.ApplyToolSelection(selectedToolDefinitionIds);
        }
    }
}
