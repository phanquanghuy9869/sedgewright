using System;
using Avalonia.Controls;
using Sedgewright.Models;
using Sedgewright.ViewModels.Chat;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Modal window for selecting or creating an agent session.
/// </summary>
public partial class AgentSessionPickerWindow : Window
{
    private AgentSessionPickerViewModel? _viewModel;

    /// <summary>
    /// Creates the session picker window.
    /// </summary>
    public AgentSessionPickerWindow()
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

        _viewModel = DataContext as AgentSessionPickerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(AgentRuntime? agent)
        => Close(agent);
}
