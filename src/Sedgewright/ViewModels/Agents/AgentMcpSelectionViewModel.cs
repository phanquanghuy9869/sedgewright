using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Drives the modal MCP attachment selection dialog.
/// </summary>
public sealed class AgentMcpSelectionViewModel : ViewModelBase
{
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _cancelCommand;

    /// <summary>
    /// Creates an MCP attachment selection dialog model.
    /// </summary>
    /// <param name="mcpDefinitions">The MCP definitions available for attachment.</param>
    /// <param name="initiallySelectedMcpIds">The MCP ids selected before opening the dialog.</param>
    public AgentMcpSelectionViewModel(
        IReadOnlyList<McpDefinitionSnapshot> mcpDefinitions,
        IReadOnlyList<int> initiallySelectedMcpIds)
    {
        Items = [];
        _applyCommand = new RelayCommand(ApplySelection, CanApply);
        _cancelCommand = new RelayCommand(CancelSelection);

        HashSet<int> selectedIds = [.. initiallySelectedMcpIds];
        foreach (McpDefinitionSnapshot snapshot in mcpDefinitions.OrderBy(snapshot => snapshot.Name))
        {
            Items.Add(new AgentMcpDefinitionSelectionItemViewModel(
                snapshot,
                selectedIds.Contains(snapshot.Id),
                OnSelectionChanged));
        }
    }

    /// <summary>
    /// Raised when the dialog requests closing.
    /// </summary>
    public event EventHandler<AgentMcpSelectionCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets the selectable MCP definitions.
    /// </summary>
    public ObservableCollection<AgentMcpDefinitionSelectionItemViewModel> Items { get; }

    /// <summary>
    /// Gets whether any MCP definitions are available.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets the command that applies the current selection.
    /// </summary>
    public ICommand ApplyCommand => _applyCommand;

    /// <summary>
    /// Gets the command that cancels the selection.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets the selected MCP ids.
    /// </summary>
    public IReadOnlyList<int> SelectedMcpIds => Items
        .Where(item => item.IsSelected)
        .Select(item => item.McpId)
        .ToArray();

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string WindowTitle => "Choose MCP attachments";

    private void ApplySelection()
        => CloseRequested?.Invoke(this, new AgentMcpSelectionCloseRequestedEventArgs(true));

    private bool CanApply()
        => HasItems;

    private void CancelSelection()
        => CloseRequested?.Invoke(this, new AgentMcpSelectionCloseRequestedEventArgs(false));

    private void OnSelectionChanged()
        => OnPropertyChanged(nameof(SelectedMcpIds));
}

/// <summary>
/// Describes a close request from the MCP selection dialog.
/// </summary>
public sealed class AgentMcpSelectionCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    /// Gets whether the selection should be applied.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
/// Provides the MCP definitions and current selection to the chooser window.
/// </summary>
public sealed class AgentMcpSelectionRequestedEventArgs(
    IReadOnlyList<McpDefinitionSnapshot> mcpDefinitions,
    IReadOnlyList<int> initiallySelectedMcpIds) : EventArgs
{
    /// <summary>
    /// Gets the available MCP definitions.
    /// </summary>
    public IReadOnlyList<McpDefinitionSnapshot> McpDefinitions { get; } = mcpDefinitions;

    /// <summary>
    /// Gets the MCP ids selected before opening the dialog.
    /// </summary>
    public IReadOnlyList<int> InitiallySelectedMcpIds { get; } = initiallySelectedMcpIds;
}
