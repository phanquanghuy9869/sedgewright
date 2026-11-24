using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds reusable tool-definition attachment state for the agent editor.
/// </summary>
public sealed class AgentDefinitionToolsViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly IReadOnlyList<ToolDefinition> _toolDefinitions;
    private readonly RelayCommand _openToolPickerCommand;

    /// <summary>
    /// Creates the tool attachment editor state.
    /// </summary>
    /// <param name="selectedToolDefinitionIds">The tool definitions already attached to the draft.</param>
    /// <param name="toolDefinitions">The reusable tool definitions available for attachment.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    public AgentDefinitionToolsViewModel(
        IReadOnlyList<Guid> selectedToolDefinitionIds,
        IReadOnlyList<ToolDefinition> toolDefinitions,
        Action draftChanged)
    {
        _draftChanged = draftChanged;
        _toolDefinitions = toolDefinitions;
        Items = [];
        _openToolPickerCommand = new RelayCommand(RequestToolSelection);

        HashSet<Guid> selectedIds = [.. selectedToolDefinitionIds];
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
    /// Raised when the editor requests the tool selection dialog.
    /// </summary>
    public event EventHandler<AgentToolSelectionRequestedEventArgs>? ToolSelectionRequested;

    /// <summary>
    /// Gets the available tool definitions.
    /// </summary>
    public ObservableCollection<AgentToolDefinitionSelectionItemViewModel> Items { get; }

    /// <summary>
    /// Gets the command that opens the tool selection dialog.
    /// </summary>
    public ICommand OpenToolPickerCommand => _openToolPickerCommand;

    /// <summary>
    /// Gets the selected tool definition identifiers.
    /// </summary>
    public IReadOnlyList<Guid> SelectedToolDefinitionIds => [.. Items
        .Where(item => item.IsSelected)
        .Select(item => item.Id)];

    /// <summary>
    /// Gets a compact summary of the current selection.
    /// </summary>
    public string ToolSummary
    {
        get
        {
            int selectedCount = Items.Count(item => item.IsSelected);
            if (selectedCount == 0)
            {
                return "No tools attached";
            }

            return selectedCount == 1
                ? "1 tool attached"
                : $"{selectedCount} tools attached";
        }
    }

    /// <summary>
    /// Applies the accepted selection from the modal picker.
    /// </summary>
    internal void ApplyToolSelection(IReadOnlyList<Guid> selectedToolDefinitionIds)
    {
        HashSet<Guid> selectedIds = [.. selectedToolDefinitionIds];
        foreach (AgentToolDefinitionSelectionItemViewModel item in Items)
        {
            item.IsSelected = selectedIds.Contains(item.Id);
        }

        OnSelectionChanged();
    }

    private void RequestToolSelection()
        => ToolSelectionRequested?.Invoke(
            this,
            new AgentToolSelectionRequestedEventArgs(_toolDefinitions, SelectedToolDefinitionIds));

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(ToolSummary));
        OnPropertyChanged(nameof(SelectedToolDefinitionIds));
        _draftChanged();
    }
}
