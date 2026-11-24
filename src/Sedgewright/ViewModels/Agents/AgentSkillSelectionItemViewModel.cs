using System;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Represents one discovered skill that can be enabled for an agent.
/// </summary>
public sealed class AgentSkillSelectionItemViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    /// <summary>
    /// Creates a selectable skill item.
    /// </summary>
    /// <param name="name">The skill directory name.</param>
    /// <param name="displayName">The name shown to the user.</param>
    /// <param name="isSelected">Whether the skill starts selected.</param>
    /// <param name="selectionChanged">The callback raised after selection changes.</param>
    public AgentSkillSelectionItemViewModel(
        string name,
        string displayName,
        bool isSelected,
        Action selectionChanged)
    {
        Name = name;
        DisplayName = displayName;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    /// <summary>
    /// Gets the skill directory name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the display name, including a relative path when the skill is nested.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets or sets whether this skill is enabled.
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
