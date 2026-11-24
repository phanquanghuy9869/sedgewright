using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Sedgewright.ViewModels.Settings;
using Sedgewright.ViewModels.FirstRun;
using Sedgewright.Views.ProviderModelSelection;
using Sedgewright.ViewModels.ProviderModelSelection;

namespace Sedgewright.Views.Settings;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            foreach (var item in _viewModel.Providers)
            {
                item.ModelSelectionRequested -= OnModelSelectionRequested;
            }

            _viewModel.DefaultWorkingDirectorySelectionRequested -=
                OnDefaultWorkingDirectorySelectionRequested;

            if (_viewModel.Providers is INotifyCollectionChanged oldCC)
            {
                oldCC.CollectionChanged -= OnProvidersCollectionChanged;
            }
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            foreach (var item in _viewModel.Providers)
            {
                item.ModelSelectionRequested += OnModelSelectionRequested;
            }

            _viewModel.DefaultWorkingDirectorySelectionRequested +=
                OnDefaultWorkingDirectorySelectionRequested;

            if (_viewModel.Providers is INotifyCollectionChanged cc)
            {
                cc.CollectionChanged += OnProvidersCollectionChanged;
            }
        }
    }

    private void OnProvidersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var old in e.OldItems.OfType<object>())
            {
                if (old is ProviderSettingsItemViewModel vm)
                {
                    vm.ModelSelectionRequested -= OnModelSelectionRequested;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var @new in e.NewItems.OfType<object>())
            {
                if (@new is ProviderSettingsItemViewModel vm)
                {
                    vm.ModelSelectionRequested += OnModelSelectionRequested;
                }
            }
        }
    }

    private async void OnModelSelectionRequested(object? sender, ModelSelectionRequestedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (sender is not ProviderSettingsItemViewModel providerItem)
        {
            return;
        }

        ProviderModelSelectionWindow selectionWindow = new()
        {
            DataContext = new ProviderModelSelectionViewModel(providerItem.DisplayName, e.DiscoveredModels, e.InitiallyEnabledModels),
        };

        Window parent = TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("Settings view must be hosted inside a window.");

        IReadOnlyList<string>? selectedModels = await selectionWindow.ShowDialog<IReadOnlyList<string>?>(parent);
        if (selectedModels is not null)
        {
            providerItem.SetEnabledModels(selectedModels);
        }
    }

    private async void OnDefaultWorkingDirectorySelectionRequested(object? sender, EventArgs e)
    {
        if (sender is not SettingsViewModel settingsViewModel)
        {
            return;
        }

        IStorageProvider? storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            settingsViewModel.ShowStatus("Storage access is not available for this window.");
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
            settingsViewModel.ApplyDefaultWorkingDirectory(selectedFolders[0].TryGetLocalPath() ?? string.Empty);
        }
    }
}
