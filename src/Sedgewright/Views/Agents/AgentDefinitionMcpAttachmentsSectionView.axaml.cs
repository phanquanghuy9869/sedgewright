using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders MCP attachment selection for the agent editor.
/// </summary>
public partial class AgentDefinitionMcpAttachmentsSectionView : UserControl
{
    private AgentDefinitionMcpAttachmentsViewModel? _viewModel;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public AgentDefinitionMcpAttachmentsSectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.McpSelectionRequested -= OnMcpSelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionMcpAttachmentsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.McpSelectionRequested += OnMcpSelectionRequested;
        }
    }

    private async void OnMcpSelectionRequested(
        object? sender,
        AgentMcpSelectionRequestedEventArgs e)
    {
        if (sender is not AgentDefinitionMcpAttachmentsViewModel mcpAttachmentsViewModel)
        {
            return;
        }

        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        AgentMcpSelectionWindow selectionWindow = new()
        {
            DataContext = new AgentMcpSelectionViewModel(
                e.McpDefinitions,
                e.InitiallySelectedMcpIds),
        };

        IReadOnlyList<int>? selectedMcpIds = await selectionWindow.ShowDialog<IReadOnlyList<int>?>(owner);
        if (selectedMcpIds is not null)
        {
            mcpAttachmentsViewModel.ApplyMcpSelection(selectedMcpIds);
        }
    }
}
