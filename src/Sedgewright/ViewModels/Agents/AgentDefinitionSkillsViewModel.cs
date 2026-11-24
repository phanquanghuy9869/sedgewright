using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds skills attachment state for the agent editor.
/// </summary>
public sealed class AgentDefinitionSkillsViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly Action<string> _reportStatus;
    private readonly RelayCommand _addSkillCommand;
    private readonly IReadOnlyList<AgentSkillSource> _commonSkillSources;
    private readonly string _defaultWorkingDirectory;

    /// <summary>
    /// Creates the skills editor state.
    /// </summary>
    /// <param name="skillAttachments">The initial attached skill directories.</param>
    /// <param name="commonSkillSources">Existing skill roots shared by supported agent tools.</param>
    /// <param name="defaultWorkingDirectory">The configured default working directory, if any.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    /// <param name="reportStatus">Reports nonfatal workflow status back to the editor.</param>
    public AgentDefinitionSkillsViewModel(
        IReadOnlyList<AgentSkillAttachmentDraft> skillAttachments,
        IReadOnlyList<AgentSkillSource> commonSkillSources,
        string? defaultWorkingDirectory,
        Action draftChanged,
        Action<string> reportStatus)
    {
        ArgumentNullException.ThrowIfNull(skillAttachments);
        ArgumentNullException.ThrowIfNull(commonSkillSources);
        _draftChanged = draftChanged;
        _reportStatus = reportStatus;
        _defaultWorkingDirectory = string.IsNullOrWhiteSpace(defaultWorkingDirectory)
            ? string.Empty
            : NormalizeSkillDirectory(defaultWorkingDirectory);
        _commonSkillSources = commonSkillSources
            .Where(source => !string.IsNullOrWhiteSpace(source.Directory))
            .Select(source => source with { Directory = NormalizeSkillDirectory(source.Directory) })
            .DistinctBy(source => source.Directory, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SkillItems = [];
        _addSkillCommand = new RelayCommand(AddSkill);

        Initialize(skillAttachments);
    }

    public event EventHandler? SkillDirectorySelectionRequested;

    public event EventHandler<AgentSkillSelectionRequestedEventArgs>? SkillSelectionRequested;

    public ObservableCollection<AgentSkillAttachmentItemViewModel> SkillItems { get; }

    public bool HasSkillItems => SkillItems.Count > 0;

    public bool HasNoSkillItems => !HasSkillItems;

    public string SkillSummary
    {
        get
        {
            int selectedCount = SkillItems.Sum(item => item.SelectedSkills.Count);
            int unrestrictedDirectoryCount = SkillItems.Count(item => item.SelectedSkills.Count == 0);
            if (SkillItems.Count == 0)
            {
                return "No skill directories attached";
            }

            if (unrestrictedDirectoryCount > 0)
            {
                return $"{SkillItems.Count} directories attached, all skills enabled in {unrestrictedDirectoryCount}";
            }

            return selectedCount == 1
                ? $"{SkillItems.Count} directory attached, 1 skill enabled"
                : $"{SkillItems.Count} directories attached, {selectedCount} skills enabled";
        }
    }

    public IReadOnlyList<AgentSkillAttachmentDraft> SkillAttachments => [.. SkillItems
        .Select(item => new AgentSkillAttachmentDraft(item.SkillDirectory, item.SelectedSkills))];

    public ICommand AddSkillCommand => _addSkillCommand;

    internal void RequestSkillSelection(string? additionalDirectory = null)
    {
        SkillSelectionRequested?.Invoke(
            this,
            new AgentSkillSelectionRequestedEventArgs(
                BuildSkillSources(additionalDirectory),
                SkillAttachments));
    }

    internal void ApplySkillSelection(IReadOnlyList<AgentSkillAttachmentDraft> selectedAttachments)
    {
        ArgumentNullException.ThrowIfNull(selectedAttachments);

        List<AgentSkillAttachmentDraft> normalizedAttachments = [];
        HashSet<string> processedDirectories = new(StringComparer.OrdinalIgnoreCase);
        foreach (AgentSkillAttachmentDraft attachment in selectedAttachments)
        {
            string directory = NormalizeSkillDirectory(attachment.SkillDirectory);
            if (directory.Length == 0 || !processedDirectories.Add(directory))
            {
                ShowStatus("The skill selection contains an invalid or duplicate source directory.");
                return;
            }

            normalizedAttachments.Add(new AgentSkillAttachmentDraft(
                directory,
                [.. attachment.SelectedSkills
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)]));
        }

        foreach (AgentSkillAttachmentDraft attachment in normalizedAttachments)
        {
            AgentSkillAttachmentItemViewModel? existingAttachment = SkillItems.FirstOrDefault(item =>
                string.Equals(item.SkillDirectory, attachment.SkillDirectory, StringComparison.OrdinalIgnoreCase));

            if (attachment.SelectedSkills.Count == 0)
            {
                if (existingAttachment is not null)
                {
                    SkillItems.Remove(existingAttachment);
                }

                continue;
            }

            if (existingAttachment is not null)
            {
                existingAttachment.ApplySelection(attachment.SelectedSkills);
                continue;
            }

            SkillItems.Add(CreateAttachment(attachment.SkillDirectory, attachment.SelectedSkills));
        }

        NotifyStateChanged();
        _draftChanged();
    }

    internal void ShowStatus(string status)
        => _reportStatus(status);

    private void Initialize(IReadOnlyList<AgentSkillAttachmentDraft> skillAttachments)
    {
        SkillItems.Clear();
        foreach (AgentSkillAttachmentDraft skillAttachment in skillAttachments)
        {
            string directory = NormalizeSkillDirectory(skillAttachment.SkillDirectory);
            if (directory.Length == 0 || HasSkillDirectory(directory))
            {
                continue;
            }

            SkillItems.Add(CreateAttachment(directory, skillAttachment.SelectedSkills));
        }

        NotifyStateChanged();
    }

    private void RequestSkillDirectorySelection()
        => SkillDirectorySelectionRequested?.Invoke(this, EventArgs.Empty);

    private void AddSkill()
    {
        if (_defaultWorkingDirectory.Length == 0)
        {
            RequestSkillDirectorySelection();
            return;
        }

        RequestSkillSelection();
    }

    private void EditSkill(AgentSkillAttachmentItemViewModel attachment)
        => RequestSkillSelection();

    private void RemoveSkill(AgentSkillAttachmentItemViewModel attachment)
    {
        SkillItems.Remove(attachment);
        NotifyStateChanged();
        _draftChanged();
    }

    private AgentSkillAttachmentItemViewModel CreateAttachment(string skillDirectory, IReadOnlyList<string> selectedSkills)
        => new(
            skillDirectory,
            selectedSkills,
            EditSkill,
            RemoveSkill);

    private bool HasSkillDirectory(string directory)
        => SkillItems.Any(item => string.Equals(item.SkillDirectory, directory, StringComparison.OrdinalIgnoreCase));

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasSkillItems));
        OnPropertyChanged(nameof(HasNoSkillItems));
        OnPropertyChanged(nameof(SkillSummary));
        OnPropertyChanged(nameof(SkillAttachments));
    }

    private IReadOnlyList<AgentSkillSource> BuildSkillSources(string? additionalDirectory)
    {
        List<AgentSkillSource> sources = [];
        HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);

        if (_defaultWorkingDirectory.Length > 0)
        {
            AddSource(new AgentSkillSource("Working directory", _defaultWorkingDirectory));
        }

        if (!string.IsNullOrWhiteSpace(additionalDirectory))
        {
            AddSource(new AgentSkillSource("Selected folder", additionalDirectory));
        }

        foreach (AgentSkillSource source in _commonSkillSources)
        {
            AddSource(source);
        }

        foreach (AgentSkillAttachmentItemViewModel attachment in SkillItems)
        {
            string? directoryName = Path.GetFileName(attachment.SkillDirectory);
            string sourceName = string.IsNullOrWhiteSpace(directoryName)
                ? "Attached folder"
                : $"Attached: {directoryName}";
            AddSource(new AgentSkillSource(sourceName, attachment.SkillDirectory));
        }

        return sources;

        void AddSource(AgentSkillSource source)
        {
            string directory = NormalizeSkillDirectory(source.Directory);
            if (directory.Length == 0 || !directories.Add(directory))
            {
                return;
            }

            sources.Add(source with { Directory = directory });
        }
    }

    private static string NormalizeSkillDirectory(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Path.TrimEndingDirectorySeparator(value.Trim());
}
