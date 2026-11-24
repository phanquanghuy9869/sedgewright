using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sedgewright.ViewModels.FirstRun;
using Sedgewright.ViewModels.ProviderModelSelection;
using Sedgewright.Views.ProviderModelSelection;

namespace Sedgewright.Views.FirstRun;

public partial class FirstRunWindow : Window
{
    private FirstRunWindowViewModel? _viewModel;

    public FirstRunWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public event EventHandler? Completed;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Completed -= OnViewModelCompleted;
            foreach (ProviderDraftViewModel provider in _viewModel.Providers)
            {
                provider.ModelSelectionRequested -= OnModelSelectionRequested;
            }

            _viewModel.DefaultWorkingDirectorySelectionRequested -=
                OnDefaultWorkingDirectorySelectionRequested;
        }

        _viewModel = DataContext as FirstRunWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Completed += OnViewModelCompleted;
            foreach (ProviderDraftViewModel provider in _viewModel.Providers)
            {
                provider.ModelSelectionRequested += OnModelSelectionRequested;
            }

            _viewModel.DefaultWorkingDirectorySelectionRequested +=
                OnDefaultWorkingDirectorySelectionRequested;
        }
    }

    private async void OnModelSelectionRequested(object? sender, ModelSelectionRequestedEventArgs e)
    {
        if (sender is not ProviderDraftViewModel provider)
        {
            return;
        }

        ProviderModelSelectionWindow selectionWindow = new()
        {
            DataContext = new ProviderModelSelectionViewModel(provider.DisplayName, e.DiscoveredModels, e.InitiallyEnabledModels),
        };

        IReadOnlyList<string>? selectedModels = await selectionWindow.ShowDialog<IReadOnlyList<string>?>(this);
        if (selectedModels is not null)
        {
            provider.SetEnabledModels(selectedModels);
        }
    }

    private void OnViewModelCompleted(object? sender, EventArgs e)
        => Completed?.Invoke(this, EventArgs.Empty);

    private async void OnDefaultWorkingDirectorySelectionRequested(object? sender, EventArgs e)
    {
        if (sender is not FirstRunWindowViewModel viewModel)
        {
            return;
        }

        IStorageProvider? storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            viewModel.ShowStatus("Storage access is not available for this window.");
            return;
        }

        IReadOnlyList<IStorageFolder> selectedFolders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose default working directory",
            });

        if (selectedFolders.Count > 0)
        {
            viewModel.ApplyDefaultWorkingDirectory(selectedFolders[0].TryGetLocalPath() ?? string.Empty);
        }
    }
}
