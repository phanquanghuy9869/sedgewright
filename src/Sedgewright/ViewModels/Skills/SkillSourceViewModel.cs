using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sedgewright.ViewModels.Skills;

/// <summary>
/// Groups read-only discovered skills beneath one source root.
/// </summary>
public sealed class SkillSourceViewModel(
    string name,
    string directory,
    IEnumerable<SkillItemViewModel> skills)
{
    /// <summary>
    /// Gets the source display name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the source root directory.
    /// </summary>
    public string Directory { get; } = directory;

    /// <summary>
    /// Gets all discovered skills in this source.
    /// </summary>
    public ObservableCollection<SkillItemViewModel> Skills { get; } = [.. skills];

    /// <summary>
    /// Gets skills currently matching the page filter.
    /// </summary>
    public ObservableCollection<SkillItemViewModel> VisibleSkills { get; } = [.. skills];

    /// <summary>
    /// Updates the visible skills without changing the complete source collection.
    /// </summary>
    /// <param name="filterText">The text used to filter skill names and paths.</param>
    public void ApplyFilter(string filterText)
    {
        IEnumerable<SkillItemViewModel> matchingSkills = string.IsNullOrWhiteSpace(filterText)
            ? Skills
            : Skills.Where(skill =>
                skill.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || skill.Directory.Contains(filterText, StringComparison.OrdinalIgnoreCase));

        VisibleSkills.Clear();
        foreach (SkillItemViewModel skill in matchingSkills)
        {
            VisibleSkills.Add(skill);
        }
    }
}
