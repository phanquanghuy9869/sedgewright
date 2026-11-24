using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Drives the modal reusable tool-definition selection dialog.
/// </summary>
public sealed class AgentToolSelectionViewModel : ViewModelBase
{
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _cancelCommand;

    /// <summary>
    /// Creates a tool-definition selection dialog model.
    /// </summary>
    /// <param name="toolDefinitions">The tool definitions available for attachment.</param>
    /// <param name="initiallySelectedToolDefinitionIds">The definitions selected before opening the dialog.</param>
    public AgentToolSelectionViewModel(
        IReadOnlyList<ToolDefinition> toolDefinitions,
        IReadOnlyList<Guid> initiallySelectedToolDefinitionIds)
    {
        Items = [];
        _applyCommand = new RelayCommand(ApplySelection, CanApply);
        _cancelCommand = new RelayCommand(CancelSelection);

        HashSet<Guid> selectedIds = [.. initiallySelectedToolDefinitionIds];
        foreach (ToolDefinition definition in toolDefinitions
            .GroupBy(definition => definition.Id)
            .Select(group => group.First())
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase))
        {
            Items.Add(new AgentToolDefinitionSelectionItemViewModel(
                definition,
                selectedIds.Contains(definition.Id),
                OnSelectionChanged));
        }
    }

    /// <summary>
    /// Raised when the selection dialog requests closing.
    /// </summary>
    public event EventHandler<AgentToolSelectionCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets the selectable tool definitions.
    /// </summary>
    public ObservableCollection<AgentToolDefinitionSelectionItemViewModel> Items { get; }

    /// <summary>
    /// Gets whether any tool definitions are available.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets the command that applies the selection.
    /// </summary>
    public ICommand ApplyCommand => _applyCommand;

    /// <summary>
    /// Gets the command that cancels the selection.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets the selected tool definition identifiers.
    /// </summary>
    public IReadOnlyList<Guid> SelectedToolDefinitionIds => [.. Items
        .Where(item => item.IsSelected)
        .Select(item => item.Id)];

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string WindowTitle => "Choose tool attachments";

    private void ApplySelection()
        => CloseRequested?.Invoke(this, new AgentToolSelectionCloseRequestedEventArgs(true));

    private bool CanApply()
        => HasItems;

    private void CancelSelection()
        => CloseRequested?.Invoke(this, new AgentToolSelectionCloseRequestedEventArgs(false));

    private void OnSelectionChanged()
        => OnPropertyChanged(nameof(SelectedToolDefinitionIds));
}

/// <summary>
/// Describes a close request from the tool selection dialog.
/// </summary>
public sealed class AgentToolSelectionCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    /// Gets whether the selection should be applied.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

