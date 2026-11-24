using System;
using Avalonia.Controls;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders Harness-specific options for the agent editor.
/// </summary>
public partial class AgentDefinitionHarnessOptionsSectionView : UserControl
{
    private AgentDefinitionHarnessOptionsViewModel? _viewModel;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public AgentDefinitionHarnessOptionsSectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.HarnessOptionsSelectionRequested -= OnHarnessOptionsSelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionHarnessOptionsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.HarnessOptionsSelectionRequested += OnHarnessOptionsSelectionRequested;
        }
    }

    private async void OnHarnessOptionsSelectionRequested(
        object? sender,
        AgentHarnessOptionsSelectionRequestedEventArgs e)
    {
        if (sender is not AgentDefinitionHarnessOptionsViewModel harnessOptionsViewModel)
        {
            return;
        }

        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            if (e.RevertSelection is not null)
            {
                harnessOptionsViewModel.ApplyHarnessOptionsSelection(e.RevertSelection);
            }

            return;
        }

        AgentHarnessOptionsSelectionWindow selectionWindow = new()
        {
            DataContext = new AgentHarnessOptionsSelectionViewModel(
                e.InitialSelection),
        };

        AgentHarnessOptionsSelection? selection =
            await selectionWindow.ShowDialog<AgentHarnessOptionsSelection?>(owner);
        if (selection is not null)
        {
            harnessOptionsViewModel.ApplyHarnessOptionsSelection(selection);
            return;
        }

        if (e.RevertSelection is not null)
        {
            harnessOptionsViewModel.ApplyHarnessOptionsSelection(e.RevertSelection);
        }
    }
}
