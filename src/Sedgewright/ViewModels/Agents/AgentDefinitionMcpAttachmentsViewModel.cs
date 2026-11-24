using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds MCP attachment selection state for the agent editor.
/// </summary>
public sealed class AgentDefinitionMcpAttachmentsViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly IReadOnlyList<McpDefinitionSnapshot> _mcpDefinitions;
    private readonly RelayCommand _openMcpPickerCommand;

    /// <summary>
    /// Creates the MCP attachment editor state.
    /// </summary>
    /// <param name="selectedMcpIds">The MCP ids already attached to the draft.</param>
    /// <param name="mcpDefinitions">The available MCP definitions.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    public AgentDefinitionMcpAttachmentsViewModel(
        IReadOnlyList<int> selectedMcpIds,
        IReadOnlyList<McpDefinitionSnapshot> mcpDefinitions,
        Action draftChanged)
    {
        _draftChanged = draftChanged;
        _mcpDefinitions = mcpDefinitions;
        Items = [];
        _openMcpPickerCommand = new RelayCommand(RequestMcpSelection);

        HashSet<int> selectedIds = [.. selectedMcpIds];
        foreach (McpDefinitionSnapshot snapshot in mcpDefinitions.OrderBy(snapshot => snapshot.Name))
        {
            Items.Add(new AgentMcpDefinitionSelectionItemViewModel(
                snapshot,
                selectedIds.Contains(snapshot.Id),
                OnSelectionChanged));
        }
    }

    public event EventHandler<AgentMcpSelectionRequestedEventArgs>? McpSelectionRequested;

    public ObservableCollection<AgentMcpDefinitionSelectionItemViewModel> Items { get; }

    public ICommand OpenMcpPickerCommand => _openMcpPickerCommand;

    public IReadOnlyList<int> SelectedMcpIds => [.. Items
        .Where(item => item.IsSelected)
        .Select(item => item.McpId)];

    public string McpSummary
    {
        get
        {
            int selectedCount = Items.Count(item => item.IsSelected);
            if (selectedCount == 0)
            {
                return "No MCP attached";
            }

            return selectedCount == 1
                ? "1 MCP attached"
                : $"{selectedCount} MCPs attached";
        }
    }

    internal void ApplyMcpSelection(IReadOnlyList<int> selectedMcpIds)
    {
        HashSet<int> selectedIds = [.. selectedMcpIds];
        foreach (AgentMcpDefinitionSelectionItemViewModel item in Items)
        {
            item.IsSelected = selectedIds.Contains(item.McpId);
        }

        OnSelectionChanged();
    }

    private void RequestMcpSelection()
        => McpSelectionRequested?.Invoke(
            this,
            new AgentMcpSelectionRequestedEventArgs(_mcpDefinitions, SelectedMcpIds));

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(McpSummary));
        _draftChanged();
    }
}
