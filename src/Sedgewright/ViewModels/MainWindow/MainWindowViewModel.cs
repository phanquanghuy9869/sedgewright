using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Sedgewright.ViewModels.Agents;
using Sedgewright.ViewModels.Chat;
using Sedgewright.ViewModels.Mcp;
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.ViewModels.ScheduledJobs;
using Sedgewright.ViewModels.Settings;
using Sedgewright.ViewModels.Tools;
using Sedgewright.ViewModels.Workspace;

namespace Sedgewright.ViewModels.MainWindow;

public sealed class MainWindowViewModel : ViewModelBase
{
    private sealed record ShellSection(string Key, string Title, Geometry Icon);

    private static readonly ShellSection[] Sections =
    [
        new("chat", "Chat", Geometry.Parse("M20 11.5a7.5 7.5 0 0 1-7.5 7.5H6l-3 2v-9.5A7.5 7.5 0 0 1 10.5 4h2A7.5 7.5 0 0 1 20 11.5Z M8 11h8 M8 14.5h5")),
        new("settings", "Settings", Geometry.Parse("M9.7 3h4.6l.5 2.2c.6.2 1.1.5 1.6.9l2-.7 2.3 4-.5 1.6a7 7 0 0 1 0 1.9l.5 1.6-2.3 4-2-.7c-.5.4-1 .7-1.6.9l-.5 2.2H9.7l-.5-2.2c-.6-.2-1.1-.5-1.6-.9l-2 .7-2.3-4 .5-1.6a7 7 0 0 1 0-1.9l-.5-1.6 2.3-4 2 .7c.5-.4 1-.7 1.6-.9L9.7 3Z M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z")),
        new("mcp", "MCP", Geometry.Parse("M8.5 8.5 15.5 15.5 M15.5 8.5 8.5 15.5 M9 5.5H7a4 4 0 0 0 0 8h2 M15 13.5h2a4 4 0 0 0 0-8h-2")),
        new("skills", "Skills", Geometry.Parse("M12 3l1.7 5.3L19 10l-5.3 1.7L12 17l-1.7-5.3L5 10l5.3-1.7L12 3Z M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8L19 15Z")),
        new("agent-orchestration", "Orchestration", Geometry.Parse("M9 3h6v5H9z M4 16h6v5H4z M14 16h6v5h-6z M12 8v4 M7 12h10 M7 12v4 M17 12v4")),
        new("agent-definitions", "Agents", Geometry.Parse("M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z M4 21a8 8 0 0 1 16 0")),
        new("tool-definitions", "Tools", Geometry.Parse("M21 7.5a5.5 5.5 0 0 1-7.8 5L7 19.7a2.1 2.1 0 0 1-3-3l6.9-6.2a5.5 5.5 0 0 1 5-7.8l-3.4 3.4 2.8 2.8 3.4-3.4A5.5 5.5 0 0 1 21 7.5Z")),
        new("scheduled-jobs", "Jobs", Geometry.Parse("M12 3a9 9 0 1 0 9 9 M12 7v5l3.5 2 M16.5 3.5v4h-4")),
    ];

    private string _selectedSectionKey = Sections[0].Key;
    private bool _isRailOpen;
    private bool _isWorkspaceTreeOpen;

    public MainWindowViewModel(
        ChatViewModel chat,
        SettingsViewModel settings,
        McpDefinitionsViewModel mcpDefinitions,
        OrchestrationDefinitionsViewModel orchestrationDefinitions,
        AgentDefinitionsViewModel agentDefinitions,
        ToolDefinitionsViewModel toolDefinitions,
        ScheduledJobsViewModel scheduledJobs,
        WorkspaceTreePanelViewModel workspaceTree)
    {
        Chat = chat;
        Settings = settings;
        McpDefinitions = mcpDefinitions;
        OrchestrationDefinitions = orchestrationDefinitions;
        AgentDefinitions = agentDefinitions;
        ToolDefinitions = toolDefinitions;
        ScheduledJobs = scheduledJobs;
        WorkspaceTree = workspaceTree;
        SectionsView =
        [
            new ShellSectionViewModel(Sections[0].Key, Sections[0].Title, Sections[0].Icon),
            new ShellSectionViewModel(Sections[1].Key, Sections[1].Title, Sections[1].Icon),
            new ShellSectionViewModel(Sections[2].Key, Sections[2].Title, Sections[2].Icon),
            new ShellSectionViewModel(Sections[3].Key, Sections[3].Title, Sections[3].Icon),
            new ShellSectionViewModel(Sections[4].Key, Sections[4].Title, Sections[4].Icon),
            new ShellSectionViewModel(Sections[5].Key, Sections[5].Title, Sections[5].Icon),
            new ShellSectionViewModel(Sections[6].Key, Sections[6].Title, Sections[6].Icon),
            new ShellSectionViewModel(Sections[7].Key, Sections[7].Title, Sections[7].Icon),
        ];
        UpdateSectionSelection();

        SelectSectionCommand = new RelayCommand(SelectSection);
        ToggleRailCommand = new RelayCommand(_ => ToggleRail());
        ToggleWorkspaceTreeCommand = new RelayCommand(_ => ToggleWorkspaceTree());
    }

    public ChatViewModel Chat { get; }

    public McpDefinitionsViewModel McpDefinitions { get; }

    public OrchestrationDefinitionsViewModel OrchestrationDefinitions { get; }

    public AgentDefinitionsViewModel AgentDefinitions { get; }

    public ToolDefinitionsViewModel ToolDefinitions { get; }

    public ScheduledJobsViewModel ScheduledJobs { get; }

    public SettingsViewModel Settings { get; }

    public WorkspaceTreePanelViewModel WorkspaceTree { get; }

    public ObservableCollection<ShellSectionViewModel> SectionsView { get; }

    public string SelectedSectionKey
    {
        get => _selectedSectionKey;
        set
        {
            if (!SetProperty(ref _selectedSectionKey, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedSectionTitle));
            UpdateSectionSelection();
            OnPropertyChanged(nameof(IsChatSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
            OnPropertyChanged(nameof(IsMcpSelected));
            OnPropertyChanged(nameof(IsSkillsSelected));
            OnPropertyChanged(nameof(IsAgentOrchestrationSelected));
            OnPropertyChanged(nameof(IsAgentDefinitionsSelected));
            OnPropertyChanged(nameof(IsToolDefinitionsSelected));
            OnPropertyChanged(nameof(IsScheduledJobsSelected));
        }
    }

    public string SelectedSectionTitle => GetSectionTitle(SelectedSectionKey);

    public bool IsChatSelected => SelectedSectionKey == Sections[0].Key;

    public bool IsSettingsSelected => SelectedSectionKey == Sections[1].Key;

    public bool IsMcpSelected => SelectedSectionKey == Sections[2].Key;

    public bool IsSkillsSelected => SelectedSectionKey == Sections[3].Key;

    public bool IsAgentOrchestrationSelected => SelectedSectionKey == Sections[4].Key;

    public bool IsAgentDefinitionsSelected => SelectedSectionKey == Sections[5].Key;

    public bool IsToolDefinitionsSelected => SelectedSectionKey == Sections[6].Key;

    public bool IsScheduledJobsSelected => SelectedSectionKey == Sections[7].Key;

    public bool IsRailOpen
    {
        get => _isRailOpen;
        set => SetProperty(ref _isRailOpen, value);
    }

    public ICommand SelectSectionCommand { get; }

    public bool IsWorkspaceTreeOpen
    {
        get => _isWorkspaceTreeOpen;
        set => SetProperty(ref _isWorkspaceTreeOpen, value);
    }

    public ICommand ToggleRailCommand { get; }

    public ICommand ToggleWorkspaceTreeCommand { get; }

    private string GetSectionTitle(string key)
    {
        for (int index = 0; index < Sections.Length; index++)
        {
            if (Sections[index].Key == key)
            {
                return Sections[index].Title;
            }
        }

        return Sections[0].Title;
    }

    private void SelectSection(object? parameter)
    {
        if (parameter is not string key)
        {
            return;
        }

        bool isKnownSection = key == Sections[0].Key
            || key == Sections[1].Key
            || key == Sections[2].Key
            || key == Sections[3].Key
            || key == Sections[4].Key
            || key == Sections[5].Key
            || key == Sections[6].Key
            || key == Sections[7].Key;

        if (!isKnownSection)
        {
            return;
        }

        SelectedSectionKey = key;

        if (string.Equals(key, Sections[2].Key, System.StringComparison.Ordinal))
        {
            McpDefinitions.RefreshCommand.Execute(null);
        }

            if (string.Equals(key, Sections[4].Key, System.StringComparison.Ordinal))
            {
                OrchestrationDefinitions.RefreshCommand.Execute(null);
            }

        if (string.Equals(key, Sections[5].Key, System.StringComparison.Ordinal))
        {
            AgentDefinitions.RefreshCommand.Execute(null);
        }

        if (string.Equals(key, Sections[6].Key, System.StringComparison.Ordinal))
        {
            ToolDefinitions.RefreshCommand.Execute(null);
        }

        if (string.Equals(key, Sections[7].Key, System.StringComparison.Ordinal))
        {
            ScheduledJobs.RefreshCommand.Execute(null);
        }
    }

    private void ToggleRail()
        => IsRailOpen = !IsRailOpen;

    private void ToggleWorkspaceTree()
        => IsWorkspaceTreeOpen = !IsWorkspaceTreeOpen;

    private void UpdateSectionSelection()
    {
        foreach (ShellSectionViewModel section in SectionsView)
        {
            section.IsSelected = section.Key == SelectedSectionKey;
        }
    }
}

public sealed class ShellSectionViewModel(string key, string title, Geometry icon) : ViewModelBase
{
    private bool _isSelected;

    public string Key { get; } = key;

    public string Title { get; } = title;

    public Geometry Icon { get; } = icon;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
