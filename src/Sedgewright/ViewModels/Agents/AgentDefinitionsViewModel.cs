using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Manages the agent definition list page.
/// </summary>
public sealed class AgentDefinitionsViewModel : ViewModelBase
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly IAgentSkillDiscoveryService _skillDiscoveryService;
    private readonly ISharedState _sharedState;
    private readonly IMcpDefinitionRepository _mcpDefinitionRepository;
    private readonly IToolDefinitionRepository _toolDefinitionRepository;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _addCommand;
    private string _statusText = string.Empty;
    private string _filterText = string.Empty;
    private AgentDefinitionEditorViewModel? _activeEditor;

    /// <summary>
    /// Creates the agent list view-model.
    /// </summary>
    public AgentDefinitionsViewModel(
        IAgentDefinitionRepository repository,
        IAgentSkillDiscoveryService skillDiscoveryService,
        ISharedState sharedState,
        IMcpDefinitionRepository mcpDefinitionRepository,
        IToolDefinitionRepository toolDefinitionRepository)
    {
        _repository = repository;
        _skillDiscoveryService = skillDiscoveryService;
        _sharedState = sharedState;
        _mcpDefinitionRepository = mcpDefinitionRepository;
        _toolDefinitionRepository = toolDefinitionRepository;
        Items = [];
        VisibleItems = [];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _addCommand = new AsyncRelayCommand(AddAsync);

        _refreshCommand.Execute(null);
    }

    /// <summary>
    /// Gets the list rows.
    /// </summary>
    public ObservableCollection<AgentDefinitionListItemViewModel> Items { get; }

    /// <summary>
    /// Gets the rows that match the current filter.
    /// </summary>
    public ObservableCollection<AgentDefinitionListItemViewModel> VisibleItems { get; }

    /// <summary>
    /// Gets whether any items exist.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets whether no items exist.
    /// </summary>
    public bool HasNoItems => !HasItems;

    /// <summary>
    /// Gets whether any rows match the current filter.
    /// </summary>
    public bool HasVisibleItems => VisibleItems.Count > 0;

    /// <summary>
    /// Gets whether no rows match the current filter.
    /// </summary>
    public bool HasNoVisibleItems => !HasVisibleItems;

    /// <summary>
    /// Gets the active full-page editor, if one is open.
    /// </summary>
    public AgentDefinitionEditorViewModel? ActiveEditor
    {
        get => _activeEditor;
        private set
        {
            if (!SetProperty(ref _activeEditor, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsListVisible));
        }
    }

    /// <summary>
    /// Gets whether the editor page is active.
    /// </summary>
    public bool IsEditing => ActiveEditor is not null;

    /// <summary>
    /// Gets whether the agent list page is active.
    /// </summary>
    public bool IsListVisible => !IsEditing;

    /// <summary>
    /// Gets whether the filter hides all existing rows.
    /// </summary>
    public bool HasNoMatches => HasItems && HasNoVisibleItems;

    /// <summary>
    /// Gets the title text.
    /// </summary>
    public string TitleText => "Agent definitions";

    /// <summary>
    /// Gets the subtitle text.
    /// </summary>
    public string SubtitleText => "Manage reusable agents, their provider, and the MCPs they can use.";

    /// <summary>
    /// Gets or sets the text used to filter agent definitions.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            string normalizedValue = NormalizeText(value);
            if (!SetProperty(ref _filterText, normalizedValue))
            {
                return;
            }

            RefreshVisibleItems();
        }
    }

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the add command.
    /// </summary>
    public ICommand AddCommand => _addCommand;

    /// <summary>
    /// Gets the status text.
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
    /// Gets whether the status text is visible.
    /// </summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>
    /// Gets the empty-state title.
    /// </summary>
    public string EmptyStateTitle => "No agent definitions yet.";

    /// <summary>
    /// Gets the empty-state text.
    /// </summary>
    public string EmptyStateText => "Add an agent, choose a provider, and attach only the MCPs it should use.";

    /// <summary>
    /// Reloads agent definitions and reapplies the current filter.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            List<AgentDefinition> definitions = await _repository.GetAllAsync();
            Items.Clear();

            foreach (AgentDefinition definition in definitions)
            {
                bool canDelete = !await _repository.HasReferencesAsync(definition.Id);
                Items.Add(CreateRow(AgentDefinitionSnapshot.From(definition, 0), canDelete));
            }

            RefreshVisibleItems();
            StatusText = string.Empty;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load agent definitions: {exception.Message}";
        }
    }

    private async Task AddAsync()
    {
        await EditAsync(null);
    }

    private async Task EditAsync(int? id)
    {
        if (IsEditing)
        {
            return;
        }

        AgentDefinition? definition = id.HasValue
            ? await _repository.GetAsync(id.Value)
            : null;

        if (id.HasValue && definition is null)
        {
            StatusText = "The selected agent definition no longer exists.";
            return;
        }

        await _sharedState.RefreshProvidersAsync();
        if (_sharedState.Providers.Count == 0)
        {
            StatusText = "No providers are configured yet.";
            return;
        }

        System.Collections.Generic.List<McpDefinitionSnapshot> mcpDefinitions = await _mcpDefinitionRepository.GetAllAsync();
        System.Collections.Generic.List<ToolDefinition> toolDefinitions = await _toolDefinitionRepository.GetAllAsync();

        AgentDefinitionEditorViewModel editor = new(
            definition,
            _repository,
            _skillDiscoveryService,
            _sharedState,
            mcpDefinitions,
            toolDefinitions,
            (name, excludeId) => _repository.NameExistsAsync(name, excludeId));

        editor.CloseRequested += OnEditorCloseRequested;
        ActiveEditor = editor;
        StatusText = string.Empty;
    }

    private async void OnEditorCloseRequested(CSharpFunctionalExtensions.UnitResult<IApplicationError> result)
    {
        AgentDefinitionEditorViewModel? editor = ActiveEditor;
        if (editor is null)
        {
            return;
        }

        editor.CloseRequested -= OnEditorCloseRequested;
        ActiveEditor = null;
        if (result.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        try
        {
            await _sharedState.RefreshAgentsAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"The agent was saved, but shared agent state could not be refreshed: {exception.Message}";
        }
    }

    private async Task DeleteAsync(int id)
    {
        if (await _repository.HasReferencesAsync(id))
        {
            StatusText = "This agent is referenced by one or more workflows and cannot be deleted.";
            return;
        }

        AgentDefinition? definition = await _repository.GetAsync(id);
        if (definition is null)
        {
            StatusText = "The selected agent definition no longer exists.";
            return;
        }

        try
        {
            _repository.Delete(definition);
            await _repository.SaveChangesAsync();
            await RefreshAsync();
            await _sharedState.RefreshAgentsAsync();
        }
        catch (DbUpdateException exception)
        {
            StatusText = $"Could not delete the agent definition: {exception.Message}";
        }
    }

    private AgentDefinitionListItemViewModel CreateRow(AgentDefinitionSnapshot snapshot, bool canDelete)
        => new(
            snapshot,
            canDelete,
            () => EditAsync(snapshot.Id),
            () => DeleteAsync(snapshot.Id));

    private void RefreshVisibleItems()
    {
        string filterText = NormalizeText(FilterText);
        IEnumerable<AgentDefinitionListItemViewModel> filteredItems = string.IsNullOrWhiteSpace(filterText)
            ? Items
            : Items.Where(item =>
                item.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || item.ProviderDisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || item.Model.Contains(filterText, StringComparison.OrdinalIgnoreCase));

        VisibleItems.Clear();
        foreach (AgentDefinitionListItemViewModel item in filteredItems)
        {
            VisibleItems.Add(item);
        }

        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(HasNoVisibleItems));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
