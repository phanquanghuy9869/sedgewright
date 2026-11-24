using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sedgewright.Apps.Abstractions;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Views.Agents;

/// <summary>
/// Renders skill attachment controls for the agent editor.
/// </summary>
public partial class AgentDefinitionSkillsSectionView : UserControl
{
    private AgentDefinitionSkillsViewModel? _viewModel;

    internal IAgentSkillDiscoveryService? SkillDiscoveryService { get; set; }

    /// <summary>
    /// Creates the view.
    /// </summary>
    public AgentDefinitionSkillsSectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SkillDirectorySelectionRequested -= OnSkillDirectorySelectionRequested;
            _viewModel.SkillSelectionRequested -= OnSkillSelectionRequested;
        }

        _viewModel = DataContext as AgentDefinitionSkillsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SkillDirectorySelectionRequested += OnSkillDirectorySelectionRequested;
            _viewModel.SkillSelectionRequested += OnSkillSelectionRequested;
        }
    }

    private async void OnSkillDirectorySelectionRequested(
        object? sender,
        EventArgs e)
    {
        if (sender is not AgentDefinitionSkillsViewModel skillsViewModel)
        {
            return;
        }

        IStorageProvider? storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            skillsViewModel.ShowStatus("Storage access is not available for this window.");
            return;
        }

        IReadOnlyList<IStorageFolder> selectedFolders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose skills directory",
            });

        if (selectedFolders.Count == 0)
        {
            return;
        }

        string? selectedDirectory = selectedFolders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            skillsViewModel.ShowStatus("The selected directory is not available as a local path.");
            return;
        }

        skillsViewModel.RequestSkillSelection(selectedDirectory);
    }

    private async void OnSkillSelectionRequested(object? sender, AgentSkillSelectionRequestedEventArgs e)
    {
        if (sender is not AgentDefinitionSkillsViewModel skillsViewModel)
        {
            return;
        }

        if (SkillDiscoveryService is null)
        {
            return;
        }

        Window? owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        AgentSkillSelectionWindow selectionWindow = new()
        {
            DataContext = new AgentSkillSelectionViewModel(
                e.SkillSources,
                e.InitiallySelectedAttachments,
                SkillDiscoveryService),
        };

        IReadOnlyList<AgentSkillAttachmentDraft>? selectedAttachments =
            await selectionWindow.ShowDialog<IReadOnlyList<AgentSkillAttachmentDraft>?>(owner);
        if (selectedAttachments is not null)
        {
            skillsViewModel.ApplySkillSelection(selectedAttachments);
        }
    }
}
