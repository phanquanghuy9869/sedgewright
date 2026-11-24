using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Drives the grouped, multi-source skill selection dialog.
/// </summary>
public sealed class AgentSkillSelectionViewModel : ViewModelBase
{
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _cancelCommand;
    private string _statusText = string.Empty;
    private string _filterText = string.Empty;

    /// <summary>
    /// Raised when the selection dialog should close.
    /// </summary>
    public event EventHandler<AgentSkillSelectionCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// Creates the skill selection dialog model.
    /// </summary>
    /// <param name="skillSources">The source roots to scan and show as groups.</param>
    /// <param name="initialAttachments">The agent's existing source-root selections.</param>
    /// <param name="skillDiscoveryService">The service that discovers skills beneath each source root.</param>
    public AgentSkillSelectionViewModel(
        IReadOnlyList<AgentSkillSource> skillSources,
        IReadOnlyList<AgentSkillAttachmentDraft> initialAttachments,
        IAgentSkillDiscoveryService skillDiscoveryService)
    {
        ArgumentNullException.ThrowIfNull(skillSources);
        ArgumentNullException.ThrowIfNull(initialAttachments);
        ArgumentNullException.ThrowIfNull(skillDiscoveryService);

        SkillGroups = [];
        VisibleSkillGroups = [];
        _applyCommand = new RelayCommand(ApplySelection, CanApply);
        _cancelCommand = new RelayCommand(CancelSelection);

        List<string> discoveryErrors = [];
        foreach (AgentSkillSource source in skillSources)
        {
            Result<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError> result =
                skillDiscoveryService.Discover(source.Directory);
            if (result.IsFailure)
            {
                discoveryErrors.Add($"{source.Name} ({source.Directory}): {string.Join(Environment.NewLine, result.Error.Messages)}");
                continue;
            }

            AgentSkillAttachmentDraft? attachment = initialAttachments.FirstOrDefault(item =>
                string.Equals(item.SkillDirectory, source.Directory, StringComparison.OrdinalIgnoreCase));
            HashSet<string> initiallySelectedSkills = new(
                attachment?.SelectedSkills
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill.Trim()) ?? [],
                StringComparer.OrdinalIgnoreCase);
            bool selectAllSkills = attachment is not null && initiallySelectedSkills.Count == 0;

            List<AgentSkillSelectionItemViewModel> skillItems = result.Value
                .Select(skill => new AgentSkillSelectionItemViewModel(
                    skill.Name,
                    string.Equals(skill.RelativePath, skill.Name, StringComparison.OrdinalIgnoreCase)
                        ? skill.Name
                        : $"{skill.Name} ({skill.RelativePath})",
                    selectAllSkills || initiallySelectedSkills.Contains(skill.Name),
                    OnSelectionChanged))
                .ToList();

            SkillGroups.Add(new AgentSkillSelectionGroupViewModel(
                source.Name,
                source.Directory,
                skillItems));
        }

        RefreshVisibleSkillGroups();

        if (discoveryErrors.Count > 0)
        {
            StatusText = $"Some skill locations could not be scanned:{Environment.NewLine}{string.Join(Environment.NewLine, discoveryErrors)}";
        }
    }

    /// <summary>
    /// Gets the discovered skills grouped by source root.
    /// </summary>
    public ObservableCollection<AgentSkillSelectionGroupViewModel> SkillGroups { get; }

    /// <summary>
    /// Gets the source groups that contain skills matching the current filter.
    /// </summary>
    public ObservableCollection<AgentSkillSelectionGroupViewModel> VisibleSkillGroups { get; }

    /// <summary>
    /// Gets whether any source returned skills.
    /// </summary>
    public bool HasSkills => SkillGroups.Any(group => group.HasSkills);

    /// <summary>
    /// Gets whether no source returned skills.
    /// </summary>
    public bool HasNoSkills => !HasSkills;

    /// <summary>
    /// Gets whether discovered skills exist but none match the current filter.
    /// </summary>
    public bool HasNoMatches => HasSkills && VisibleSkillGroups.Count == 0;

    /// <summary>
    /// Gets or sets the text used to filter skills by name.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            string normalizedValue = NormalizeFilterText(value);
            if (!SetProperty(ref _filterText, normalizedValue))
            {
                return;
            }

            RefreshVisibleSkillGroups();
        }
    }

    /// <summary>
    /// Gets the discovery status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    /// <summary>
    /// Gets whether discovery status text is visible.
    /// </summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>
    /// Gets the command that confirms the selection.
    /// </summary>
    public ICommand ApplyCommand => _applyCommand;

    /// <summary>
    /// Gets the command that cancels the selection.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets a selection record for every successfully scanned source root.
    /// </summary>
    public IReadOnlyList<AgentSkillAttachmentDraft> SelectedSkillAttachments => [.. SkillGroups
        .Select(group => new AgentSkillAttachmentDraft(group.SkillDirectory, group.SelectedSkills))];

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string WindowTitle => "Choose agent skills";

    private void ApplySelection()
        => CloseRequested?.Invoke(this, new AgentSkillSelectionCloseRequestedEventArgs(true));

    private bool CanApply()
        => HasSkills;

    private void RefreshVisibleSkillGroups()
    {
        VisibleSkillGroups.Clear();
        foreach (AgentSkillSelectionGroupViewModel group in SkillGroups)
        {
            group.ApplyFilter(FilterText);
            if (group.VisibleSkillItems.Count > 0)
            {
                VisibleSkillGroups.Add(group);
            }
        }

        OnPropertyChanged(nameof(HasNoMatches));
    }

    private void CancelSelection()
        => CloseRequested?.Invoke(this, new AgentSkillSelectionCloseRequestedEventArgs(false));

    private void OnSelectionChanged()
        => OnPropertyChanged(nameof(SelectedSkillAttachments));

    private static string NormalizeFilterText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

/// <summary>
/// Describes a close request from the skill selection dialog.
/// </summary>
public sealed class AgentSkillSelectionCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    /// Gets whether the selection should be applied.
    /// </summary>
    public bool Accepted { get; } = accepted;
}
