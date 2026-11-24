using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Groups discovered skills under the source root they came from.
/// </summary>
public sealed class AgentSkillSelectionGroupViewModel
{
    /// <summary>
    /// Creates a source group for the skill picker.
    /// </summary>
    /// <param name="name">The source label.</param>
    /// <param name="skillDirectory">The source root path.</param>
    /// <param name="skillItems">The skills discovered under the root.</param>
    public AgentSkillSelectionGroupViewModel(
        string name,
        string skillDirectory,
        IEnumerable<AgentSkillSelectionItemViewModel> skillItems)
    {
        Name = name;
        SkillDirectory = skillDirectory;
        SkillItems = [.. skillItems];
        VisibleSkillItems = [.. SkillItems];
    }

    /// <summary>
    /// Gets the source label.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the source root path.
    /// </summary>
    public string SkillDirectory { get; }

    /// <summary>
    /// Gets the skills discovered under this source.
    /// </summary>
    public ObservableCollection<AgentSkillSelectionItemViewModel> SkillItems { get; }

    /// <summary>
    /// Gets the skills matching the current picker filter.
    /// </summary>
    public ObservableCollection<AgentSkillSelectionItemViewModel> VisibleSkillItems { get; }

    /// <summary>
    /// Gets whether this source contains any discovered skills.
    /// </summary>
    public bool HasSkills => SkillItems.Count > 0;

    /// <summary>
    /// Gets the names selected from this source.
    /// </summary>
    public IReadOnlyList<string> SelectedSkills => SkillItems
        .Where(skill => skill.IsSelected)
        .Select(skill => skill.Name)
        .Distinct(System.StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Updates the skills shown in this group without changing their selections.
    /// </summary>
    /// <param name="filterText">The name text to match, or an empty string to show every skill.</param>
    public void ApplyFilter(string filterText)
    {
        IEnumerable<AgentSkillSelectionItemViewModel> matchingSkills = string.IsNullOrWhiteSpace(filterText)
            ? SkillItems
            : SkillItems.Where(skill => skill.DisplayName.Contains(filterText, System.StringComparison.OrdinalIgnoreCase));

        VisibleSkillItems.Clear();
        foreach (AgentSkillSelectionItemViewModel skill in matchingSkills)
        {
            VisibleSkillItems.Add(skill);
        }
    }
}
