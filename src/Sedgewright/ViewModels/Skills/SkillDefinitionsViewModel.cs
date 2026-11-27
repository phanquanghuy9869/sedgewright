using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Skills;

/// <summary>
/// Manages the read-only Skills page.
/// </summary>
public sealed class SkillDefinitionsViewModel : ViewModelBase
{
    private readonly IAgentSkillDiscoveryService _skillDiscoveryService;
    private readonly ISharedState _sharedState;
    private readonly AsyncRelayCommand _refreshCommand;
    private string _filterText = string.Empty;
    private string _statusText = string.Empty;

    /// <summary>
    /// Creates the Skills page view-model.
    /// </summary>
    /// <param name="skillDiscoveryService">The service used to discover SKILL.md folders.</param>
    /// <param name="sharedState">The shared application state containing the default working directory.</param>
    public SkillDefinitionsViewModel(
        IAgentSkillDiscoveryService skillDiscoveryService,
        ISharedState sharedState)
    {
        ArgumentNullException.ThrowIfNull(skillDiscoveryService);
        ArgumentNullException.ThrowIfNull(sharedState);

        _skillDiscoveryService = skillDiscoveryService;
        _sharedState = sharedState;
        SkillSources = [];
        VisibleSkillSources = [];
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _sharedState.ApplicationSettingsChanged += OnApplicationSettingsChanged;

        _refreshCommand.Execute(null);
    }

    /// <summary>
    /// Gets all discovered source groups.
    /// </summary>
    public ObservableCollection<SkillSourceViewModel> SkillSources { get; }

    /// <summary>
    /// Gets source groups containing skills that match the current filter.
    /// </summary>
    public ObservableCollection<SkillSourceViewModel> VisibleSkillSources { get; }

    /// <summary>
    /// Gets or sets the text used to filter skill names and paths.
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

            RefreshVisibleSkillSources();
        }
    }

    /// <summary>
    /// Gets status feedback from skill discovery.
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
    /// Gets whether any skills were discovered.
    /// </summary>
    public bool HasSkills => SkillSources.Any(source => source.Skills.Count > 0);

    /// <summary>
    /// Gets whether no skills were discovered.
    /// </summary>
    public bool HasNoSkills => !HasSkills;

    /// <summary>
    /// Gets whether skills exist but none match the current filter.
    /// </summary>
    public bool HasNoMatches => HasSkills && VisibleSkillSources.Count == 0;

    /// <summary>
    /// Gets the number of discovered skills.
    /// </summary>
    public int SkillCount => SkillSources.Sum(source => source.Skills.Count);

    /// <summary>
    /// Gets the number of discovered source locations.
    /// </summary>
    public int SourceCount => SkillSources.Count;

    /// <summary>
    /// Gets the compact summary shown beside the skill filter.
    /// </summary>
    public string SkillSummaryText => $"{SkillCount} skills · {SourceCount} locations";

    /// <summary>
    /// Gets the page title.
    /// </summary>
    public string TitleText => "Skills";

    /// <summary>
    /// Gets the page subtitle.
    /// </summary>
    public string SubtitleText => "Read-only skills discovered from your working directory and common agent locations.";

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Reloads discovered skills from all configured read-only sources.
    /// </summary>
    public async Task RefreshAsync()
    {
        List<AgentSkillSource> sources = BuildSkillSources();
        List<string> discoveryErrors = [];
        SkillSources.Clear();

        foreach (AgentSkillSource source in sources)
        {
            Result<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError> result =
                _skillDiscoveryService.Discover(source.Directory);

            if (result.IsFailure)
            {
                discoveryErrors.Add(
                    $"{source.Name} ({source.Directory}): {string.Join(Environment.NewLine, result.Error.Messages)}");
                continue;
            }

            if (result.Value.Count == 0)
            {
                continue;
            }

            SkillSourceViewModel sourceViewModel = new(
                source.Name,
                source.Directory,
                result.Value.Select(skill =>
                    new SkillItemViewModel(skill.Name, skill.Directory, skill.RelativePath)));
            SkillSources.Add(sourceViewModel);
        }

        StatusText = discoveryErrors.Count == 0
            ? string.Empty
            : $"Some skill locations could not be scanned:{Environment.NewLine}{string.Join(Environment.NewLine, discoveryErrors)}";

        RefreshVisibleSkillSources();
        OnPropertyChanged(nameof(HasSkills));
        OnPropertyChanged(nameof(HasNoSkills));
        OnPropertyChanged(nameof(SkillCount));
        OnPropertyChanged(nameof(SourceCount));
        OnPropertyChanged(nameof(SkillSummaryText));
    }

    private List<AgentSkillSource> BuildSkillSources()
    {
        List<AgentSkillSource> sources = [];
        HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);

        string? workingDirectory = _sharedState.ApplicationSettings?.DefaultWorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        AddSource(new AgentSkillSource("Working directory", workingDirectory));

        foreach (AgentSkillSource source in _skillDiscoveryService.GetCommonSkillSources())
        {
            AddSource(source);
        }

        return sources;

        void AddSource(AgentSkillSource source)
        {
            string directory = NormalizeDirectory(source.Directory);
            if (directory.Length > 0 && directories.Add(directory))
            {
                sources.Add(source with { Directory = directory });
            }
        }
    }

    private void RefreshVisibleSkillSources()
    {
        VisibleSkillSources.Clear();
        foreach (SkillSourceViewModel source in SkillSources)
        {
            source.ApplyFilter(FilterText);
            if (source.VisibleSkills.Count > 0)
            {
                VisibleSkillSources.Add(source);
            }
        }

        OnPropertyChanged(nameof(HasNoMatches));
    }

    private void OnApplicationSettingsChanged(object? sender, EventArgs e)
        => _refreshCommand.Execute(null);

    private static string NormalizeDirectory(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Path.TrimEndingDirectorySeparator(value.Trim());

    private static string NormalizeFilterText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
