using System;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Represents one selectable reusable tool definition in the agent editor.
/// </summary>
public sealed class AgentToolDefinitionSelectionItemViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    /// <summary>
    /// Creates a selectable tool definition item.
    /// </summary>
    public AgentToolDefinitionSelectionItemViewModel(
        ToolDefinition definition,
        bool isSelected,
        Action selectionChanged)
    {
        Id = definition.Id;
        Name = definition.Name;
        Type = definition.Type.ToString();
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    /// <summary>
    /// Gets the tool definition identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the tool definition name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tool definition type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets or sets whether the tool is selected for the agent.
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
