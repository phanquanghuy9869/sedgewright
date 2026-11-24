using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders shell execution controls for the agent editor.
/// </summary>
public partial class AgentDefinitionShellSectionView : UserControl
{
    private AgentDefinitionShellViewModel? _viewModel;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public AgentDefinitionShellSectionView()
    {
        InitializeComponent();
        Button shellConfigurationButton = this.FindControl<Button>("ShellConfigurationButton")
            ?? throw new InvalidOperationException("Shell configuration button is required.");
        shellConfigurationButton.Click += OnShellConfigurationClick;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ShellWorkingDirectorySelectionRequested -=
                OnShellWorkingDirectorySelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ShellWorkingDirectorySelectionRequested +=
                OnShellWorkingDirectorySelectionRequested;
        }
    }

    private void OnShellConfigurationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button shellConfigurationButton)
        {
            return;
        }

        FlyoutBase.GetAttachedFlyout(shellConfigurationButton)?.ShowAt(shellConfigurationButton);
    }

    private async void OnShellWorkingDirectorySelectionRequested(object? sender, EventArgs e)
    {
        if (sender is not AgentDefinitionShellViewModel shellViewModel)
        {
            return;
        }

        IStorageProvider? storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            shellViewModel.ShowStatus("Storage access is not available for this window.");
            return;
        }

        IReadOnlyList<IStorageFolder> selectedFolders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose shell working directory",
            });

        if (selectedFolders.Count == 0)
        {
            return;
        }

        string? selectedDirectory = selectedFolders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            shellViewModel.ShowStatus("The selected directory is not available as a local path.");
            return;
        }

        shellViewModel.ApplyShellWorkingDirectory(selectedDirectory);
    }
}
