using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Displays discovered skills for a selected agent skills directory.
/// </summary>
public partial class AgentSkillSelectionWindow : Window
{
    private AgentSkillSelectionViewModel? _viewModel;

    /// <summary>
    /// Creates the skill selection window.
    /// </summary>
    public AgentSkillSelectionWindow()
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

        _viewModel = DataContext as AgentSkillSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, AgentSkillSelectionCloseRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        Close(e.Accepted ? _viewModel.SelectedSkillAttachments : null);
    }
}
