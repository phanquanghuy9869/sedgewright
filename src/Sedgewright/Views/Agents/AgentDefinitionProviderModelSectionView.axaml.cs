using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sedgewright.ViewModels.ProviderModelSelection;
using Sedgewright.Views.ProviderModelSelection;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders provider and model selection fields for the agent editor.
/// </summary>
public partial class AgentDefinitionProviderModelSectionView : UserControl
{
    private AgentDefinitionProviderModelViewModel? _viewModel;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public AgentDefinitionProviderModelSectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ModelTextBox.AddHandler(
            InputElement.PointerPressedEvent,
            OnModelTextBoxPointerPressed,
            RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ModelSelectionRequested -= OnModelSelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionProviderModelViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ModelSelectionRequested += OnModelSelectionRequested;
        }
    }

    private void OnModelTextBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel?.ChooseModelCommand.CanExecute(null) is not true)
        {
            return;
        }

        e.Handled = true;
        _viewModel.ChooseModelCommand.Execute(null);
    }

    private async void OnModelSelectionRequested(object? sender, AgentModelSelectionRequestedEventArgs e)
    {
        if (sender is not AgentDefinitionProviderModelViewModel providerModelViewModel)
        {
            return;
        }

        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        ProviderModelSelectionWindow selectionWindow = new()
        {
            DataContext = new ProviderModelSelectionViewModel(
                e.ProviderDisplayName,
                e.DiscoveredModels,
                e.InitiallySelectedModels),
        };

        IReadOnlyList<string>? selectedModels = await selectionWindow.ShowDialog<IReadOnlyList<string>?>(owner);
        if (selectedModels is not null && selectedModels.Count > 0)
        {
            providerModelViewModel.Model = selectedModels[0];
        }
    }
}
