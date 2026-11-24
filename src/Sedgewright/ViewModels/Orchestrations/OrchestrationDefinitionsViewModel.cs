using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations.Concurrent;
using Sedgewright.ViewModels.Orchestrations.Sequential;
using Sedgewright.ViewModels.Orchestrations.GroupChat;
using Sedgewright.ViewModels.Orchestrations.Magentic;
using Sedgewright.ViewModels.Orchestrations.Handoff;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Manages the orchestration list page.
/// </summary>
public sealed class OrchestrationDefinitionsViewModel : ViewModelBase
{
    private readonly ISharedState _sharedState;
    private readonly IOrchestrationQueryRepository _queryRepository;
    private readonly ISequentialOrchestrationRepository _repository;
    private readonly IConcurrentOrchestrationRepository _concurrentRepository;
    private readonly IGroupChatOrchestrationRepository _groupChatRepository;
    private readonly IMagenticOrchestrationRepository _magenticRepository;
    private readonly IHandoffOrchestrationRepository _handoffRepository;
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly IOrchestrationDialogService _dialogService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _addCommand;
    private string _statusText = string.Empty;

    /// <summary>
    /// Creates the orchestration list view-model.
    /// </summary>
    public OrchestrationDefinitionsViewModel(
        ISharedState sharedState,
        IOrchestrationQueryRepository queryRepository,
        ISequentialOrchestrationRepository repository,
        IConcurrentOrchestrationRepository concurrentRepository,
        IGroupChatOrchestrationRepository groupChatRepository,
        IMagenticOrchestrationRepository magenticRepository,
        IHandoffOrchestrationRepository handoffRepository,
        IAgentDefinitionRepository agentRepository,
        IOrchestrationDialogService dialogService)
    {
        _sharedState = sharedState;
        _queryRepository = queryRepository;
        _repository = repository;
        _concurrentRepository = concurrentRepository;
        _groupChatRepository = groupChatRepository;
        _magenticRepository = magenticRepository;
        _handoffRepository = handoffRepository;
        _agentRepository = agentRepository;
        _dialogService = dialogService;
        Items = [];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _addCommand = new AsyncRelayCommand(AddAsync);

        _refreshCommand.Execute(null);
    }

    /// <summary>
    /// Gets the list rows.
    /// </summary>
    public ObservableCollection<OrchestrationListItemViewModel> Items { get; }

    /// <summary>
    /// Gets whether any items exist.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets whether no items exist.
    /// </summary>
    public bool HasNoItems => !HasItems;

    /// <summary>
    /// Gets the title text.
    /// </summary>
    public string TitleText => "Agent orchestrations";

    /// <summary>
    /// Gets the subtitle text.
    /// </summary>
    public string SubtitleText => "Browse every persisted orchestration type in one compact list.";

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
    public string EmptyStateTitle => "No orchestrations yet.";

    /// <summary>
    /// Gets the empty-state text.
    /// </summary>
    public string EmptyStateText => "Choose an orchestration type to create your first workflow.";

    private async Task RefreshAsync()
    {
        try
        {
            List<OrchestrationListItemSnapshot> snapshots = await _queryRepository.GetAllAsync();
            Items.Clear();

            foreach (OrchestrationListItemSnapshot snapshot in snapshots)
            {
                Items.Add(new OrchestrationListItemViewModel(
                    snapshot,
                    true,
                    true,
                    () => EditAsync(snapshot.Kind, snapshot.Id),
                    () => DeleteAsync(snapshot.Kind, snapshot.Id)));
            }

            StatusText = string.Empty;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load orchestrations: {exception.Message}";
        }
    }

    private async Task AddAsync()
    {
        OrchestrationKind? selectedKind = await _dialogService.ShowKindPickerAsync(new OrchestrationKindPickerViewModel());
        if (selectedKind is null)
        {
            return;
        }

        await EditAsync(selectedKind.Value, null);
    }

    private async Task EditAsync(OrchestrationKind kind, int? id)
    {
        if (kind == OrchestrationKind.Sequential)
        {
            await EditSequentialAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Concurrent)
        {
            await EditConcurrentAsync(id);
            return;
        }

        if (kind == OrchestrationKind.GroupChat)
        {
            await EditGroupChatAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Magentic)
        {
            await EditMagenticAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Handoff)
        {
            await EditHandoffAsync(id);
            return;
        }

        StatusText = $"{kind} orchestration editing is not available yet.";
    }

    private async Task EditSequentialAsync(int? id)
    {
        SequentialOrchestration? orchestration = id.HasValue
            ? await _repository.GetAsync(id.Value)
            : null;

        if (id.HasValue && orchestration is null)
        {
            StatusText = "The selected sequential orchestration no longer exists.";
            return;
        }

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync();
        if (agents.Count == 0)
        {
            StatusText = "No agent definitions are configured yet.";
            return;
        }

        SequentialOrchestrationEditorViewModel editor = new(
            orchestration,
            agents,
            _repository,
            (name, excludeId) => _repository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowSequentialAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        await RefreshSharedStateAsync();
    }

    private async Task EditConcurrentAsync(int? id)
    {
        ConcurrentOrchestration? orchestration = id.HasValue
            ? await _concurrentRepository.GetAsync(id.Value)
            : null;

        if (id.HasValue && orchestration is null)
        {
            StatusText = "The selected concurrent orchestration no longer exists.";
            return;
        }

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync();
        if (agents.Count == 0)
        {
            StatusText = "No agent definitions are configured yet.";
            return;
        }

        ConcurrentOrchestrationEditorViewModel editor = new(
            orchestration,
            agents,
            _concurrentRepository,
            (name, excludeId) => _concurrentRepository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowConcurrentAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        await RefreshSharedStateAsync();
    }

    private async Task EditGroupChatAsync(int? id)
    {
        GroupChatOrchestration? orchestration = id.HasValue
            ? await _groupChatRepository.GetAsync(id.Value)
            : null;

        if (id.HasValue && orchestration is null)
        {
            StatusText = "The selected group chat orchestration no longer exists.";
            return;
        }

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync();
        if (agents.Count == 0)
        {
            StatusText = "No agent definitions are configured yet.";
            return;
        }

        GroupChatOrchestrationEditorViewModel editor = new(
            orchestration,
            agents,
            _groupChatRepository,
            (name, excludeId) => _groupChatRepository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowGroupChatAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        await RefreshSharedStateAsync();
    }

    private async Task EditMagenticAsync(int? id)
    {
        MagenticOrchestration? orchestration = id.HasValue
            ? await _magenticRepository.GetAsync(id.Value)
            : null;

        if (id.HasValue && orchestration is null)
        {
            StatusText = "The selected Magentic orchestration no longer exists.";
            return;
        }

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync();
        if (agents.Count < 2)
        {
            StatusText = "At least two agent definitions are required for a Magentic orchestration.";
            return;
        }

        MagenticOrchestrationEditorViewModel editor = new(
            orchestration,
            agents,
            _magenticRepository,
            (name, excludeId) => _magenticRepository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowMagenticAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        await RefreshSharedStateAsync();
    }

    private async Task EditHandoffAsync(int? id)
    {
        HandoffOrchestration? orchestration = id.HasValue
            ? await _handoffRepository.GetAsync(id.Value)
            : null;

        if (id.HasValue && orchestration is null)
        {
            StatusText = "The selected handoff orchestration no longer exists.";
            return;
        }

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync();
        if (agents.Count == 0)
        {
            StatusText = "No agent definitions are configured yet.";
            return;
        }

        HandoffOrchestrationEditorViewModel editor = new(
            orchestration,
            agents,
            _handoffRepository,
            (name, excludeId) => _handoffRepository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowHandoffAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
        await RefreshSharedStateAsync();
    }

    private async Task DeleteAsync(OrchestrationKind kind, int id)
    {
        if (kind == OrchestrationKind.Sequential)
        {
            await DeleteSequentialAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Concurrent)
        {
            await DeleteConcurrentAsync(id);
            return;
        }

        if (kind == OrchestrationKind.GroupChat)
        {
            await DeleteGroupChatAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Magentic)
        {
            await DeleteMagenticAsync(id);
            return;
        }

        if (kind == OrchestrationKind.Handoff)
        {
            await DeleteHandoffAsync(id);
            return;
        }

        StatusText = $"{kind} orchestration deletion is not available yet.";
    }

    private async Task DeleteSequentialAsync(int id)
    {
        SequentialOrchestration? orchestration = await _repository.GetAsync(id);
        if (orchestration is null)
        {
            StatusText = "The selected sequential orchestration no longer exists.";
            return;
        }

        try
        {
            _repository.Delete(orchestration);
            await _repository.SaveChangesAsync();
            await RefreshAsync();
            await RefreshSharedStateAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the sequential orchestration: {exception.Message}";
        }
    }

    private async Task DeleteConcurrentAsync(int id)
    {
        ConcurrentOrchestration? orchestration = await _concurrentRepository.GetAsync(id);
        if (orchestration is null)
        {
            StatusText = "The selected concurrent orchestration no longer exists.";
            return;
        }

        try
        {
            _concurrentRepository.Delete(orchestration);
            await _concurrentRepository.SaveChangesAsync();
            await RefreshAsync();
            await RefreshSharedStateAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the concurrent orchestration: {exception.Message}";
        }
    }

    private async Task DeleteGroupChatAsync(int id)
    {
        GroupChatOrchestration? orchestration = await _groupChatRepository.GetAsync(id);
        if (orchestration is null)
        {
            StatusText = "The selected group chat orchestration no longer exists.";
            return;
        }

        try
        {
            _groupChatRepository.Delete(orchestration);
            await _groupChatRepository.SaveChangesAsync();
            await RefreshAsync();
            await RefreshSharedStateAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the group chat orchestration: {exception.Message}";
        }
    }

    private async Task DeleteMagenticAsync(int id)
    {
        MagenticOrchestration? orchestration = await _magenticRepository.GetAsync(id);
        if (orchestration is null)
        {
            StatusText = "The selected Magentic orchestration no longer exists.";
            return;
        }

        try
        {
            _magenticRepository.Delete(orchestration);
            await _magenticRepository.SaveChangesAsync();
            await RefreshAsync();
            await RefreshSharedStateAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the Magentic orchestration: {exception.Message}";
        }
    }

    private async Task DeleteHandoffAsync(int id)
    {
        HandoffOrchestration? orchestration = await _handoffRepository.GetAsync(id);
        if (orchestration is null)
        {
            StatusText = "The selected handoff orchestration no longer exists.";
            return;
        }

        try
        {
            _handoffRepository.Delete(orchestration);
            await _handoffRepository.SaveChangesAsync();
            await RefreshAsync();
            await RefreshSharedStateAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the handoff orchestration: {exception.Message}";
        }
    }

    private Task RefreshSharedStateAsync()
        => _sharedState.RefreshOrchestrationsAsync();
}
