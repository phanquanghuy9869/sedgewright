using System;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Represents one selectable MCP attachment in the agent editor.
/// </summary>
public sealed class AgentMcpDefinitionSelectionItemViewModel : ViewModelBase
{
    private bool _isSelected;

    /// <summary>
    /// Creates a new selectable MCP item.
    /// </summary>
    public AgentMcpDefinitionSelectionItemViewModel(
        McpDefinitionSnapshot snapshot,
        bool isSelected,
        Action selectionChanged)
    {
        McpId = snapshot.Id;
        Name = snapshot.Name;
        Description = snapshot.Description;
        IsReadOnly = snapshot.IsReadOnly;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    private readonly Action _selectionChanged;

    /// <summary>
    /// Gets the MCP identifier.
    /// </summary>
    public int McpId { get; }

    /// <summary>
    /// Gets the MCP name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the MCP description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets whether the MCP is marked read-only.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets or sets whether the MCP is selected for the agent.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            _selectionChanged();
        }
    }
}
