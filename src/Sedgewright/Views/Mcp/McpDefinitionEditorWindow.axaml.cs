using System;
using Avalonia.Controls;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Mcp;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Mcp;

/// <summary>
/// Borderless editor for MCP definitions.
/// </summary>
public partial class McpDefinitionEditorWindow : Window
{
    private McpDefinitionEditorViewModel? _viewModel;

    /// <summary>
    /// Creates the editor window.
    /// </summary>
    public McpDefinitionEditorWindow()
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

        _viewModel = DataContext as McpDefinitionEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(UnitResult<IApplicationError> result)
        => Close(result);
}
