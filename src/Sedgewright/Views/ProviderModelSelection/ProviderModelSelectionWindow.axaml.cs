using System;
using Avalonia.Controls;
using Sedgewright.ViewModels.ProviderModelSelection;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.ProviderModelSelection;

/// <summary>
/// Modal window for selecting which discovered provider models stay enabled.
/// </summary>
public partial class ProviderModelSelectionWindow : Window
{
    private ProviderModelSelectionViewModel? _viewModel;

    /// <summary>
    /// Creates the chooser window.
    /// </summary>
    public ProviderModelSelectionWindow()
    {
        InitializeComponent();
        DialogWindowBehavior.Attach(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as ProviderModelSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, ProviderModelSelectionCloseRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Accepted)
        {
            Close(_viewModel.SelectedModelIds);
            return;
        }

        Close(null);
    }
}
