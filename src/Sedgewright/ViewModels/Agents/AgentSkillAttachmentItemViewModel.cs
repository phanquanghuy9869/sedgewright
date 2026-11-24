using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds one skills root directory and the skills enabled from that directory.
/// </summary>
public sealed class AgentSkillAttachmentItemViewModel : ViewModelBase
{
    private readonly Action<AgentSkillAttachmentItemViewModel> _editRequested;
    private readonly Action<AgentSkillAttachmentItemViewModel> _removeRequested;

    /// <summary>
    /// Creates a draft skill attachment.
    /// </summary>
    /// <param name="skillDirectory">The skills root directory.</param>
    /// <param name="selectedSkills">The initially enabled skill names.</param>
    /// <param name="editRequested">The callback raised when the attachment is edited.</param>
    /// <param name="removeRequested">The callback raised when the attachment is removed.</param>
    public AgentSkillAttachmentItemViewModel(
        string skillDirectory,
        IEnumerable<string> selectedSkills,
        Action<AgentSkillAttachmentItemViewModel> editRequested,
        Action<AgentSkillAttachmentItemViewModel> removeRequested)
    {
        SkillDirectory = skillDirectory;
        _editRequested = editRequested;
        _removeRequested = removeRequested;
        SelectedSkills = [.. selectedSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        EditCommand = new RelayCommand(() => _editRequested(this));
        RemoveCommand = new RelayCommand(() => _removeRequested(this));
    }

    /// <summary>
    /// Gets the root directory containing the discovered skill folders.
    /// </summary>
    public string SkillDirectory { get; }

    /// <summary>
    /// Gets the names of the enabled skills.
    /// </summary>
    public IReadOnlyList<string> SelectedSkills { get; private set; }

    /// <summary>
    /// Gets a compact selected-skill summary.
    /// </summary>
    public string SelectedSkillsSummary
        => SelectedSkills.Count == 0
            ? "All skills enabled"
            : SelectedSkills.Count == 1
                ? "1 skill enabled"
                : $"{SelectedSkills.Count} skills enabled";

    /// <summary>
    /// Gets the command that reopens skill selection for this directory.
    /// </summary>
    public ICommand EditCommand { get; }

    /// <summary>
    /// Gets the command that removes this directory attachment.
    /// </summary>
    public ICommand RemoveCommand { get; }

    /// <summary>
    /// Applies a confirmed selection from the skill chooser.
    /// </summary>
    /// <param name="selectedSkills">The names selected in the chooser.</param>
    public void ApplySelection(IEnumerable<string> selectedSkills)
    {
        SelectedSkills = [.. selectedSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        OnPropertyChanged(nameof(SelectedSkills));
        OnPropertyChanged(nameof(SelectedSkillsSummary));
    }

    /// <summary>
    /// Creates the persisted entity for this draft attachment.
    /// </summary>
    /// <param name="definition">The owning agent definition.</param>
    /// <returns>A new persisted skill relationship.</returns>
    public AgentSkillDefinition ToEntity(AgentDefinition definition)
        => new()
        {
            AgentId = definition.Id,
            SkillDirectory = SkillDirectory,
            SelectedSkills = [.. SelectedSkills],
            AgentDefinition = definition,
        };
}
