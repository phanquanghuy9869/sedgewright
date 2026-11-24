using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Drives the modal agent session picker window.
/// </summary>
public sealed class AgentSessionPickerViewModel : ViewModelBase
{
    private readonly ISharedState _sharedState;
    private readonly AgentDefinition _agentDefinition;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _createNewSessionCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly IAgentSessionRepository _agentSessionRepository;
    private readonly IChatSessionService _chatSessionService;
    private string _statusText = string.Empty;
    private bool _isLoading;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<AgentRuntime?>? CloseRequested;

    /// <summary>
    /// Creates a new session picker view-model.
    /// </summary>
    public AgentSessionPickerViewModel(
        ISharedState sharedState,
        AgentDefinition agentDefinition,
        IAgentSessionRepository agentSessionRepository,
        IChatSessionService chatSessionService)
    {
        _sharedState = sharedState;
        _agentDefinition = agentDefinition;
        _agentSessionRepository = agentSessionRepository;
        _chatSessionService = chatSessionService;

        VisibleSessions = [];

        _refreshCommand = new AsyncRelayCommand(LoadAsync, CanRefresh);
        _createNewSessionCommand = new AsyncRelayCommand(CreateNewSessionAsync, CanCreateNewSession);
        _cancelCommand = new RelayCommand(CancelSelection);
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle => "Choose session";

    /// <summary>
    /// Gets the picker subtitle.
    /// </summary>
    public string SubtitleText => $"{_agentDefinition.Name} uses one explicit session. Pick an existing session or start a new one.";

    /// <summary>
    /// Gets the selected agent summary.
    /// </summary>
    public string SelectedAgentSummary => ChatTargetDisplayInfo.From(_agentDefinition).Summary;

    /// <summary>
    /// Gets the visible session rows.
    /// </summary>
    public ObservableCollection<AgentSessionPickerRowViewModel> VisibleSessions { get; }

    /// <summary>
    /// Gets whether any sessions are visible.
    /// </summary>
    public bool HasVisibleSessions => VisibleSessions.Count > 0;

    /// <summary>
    /// Gets whether no sessions are visible.
    /// </summary>
    public bool HasNoVisibleSessions => !HasVisibleSessions;

    /// <summary>
    /// Gets the empty-state text.
    /// </summary>
    public string EmptyStateText => $"No saved sessions exist yet for {_agentDefinition.Name}.";

    /// <summary>
    /// Gets whether the empty state should be shown.
    /// </summary>
    public bool HasEmptyState => !HasVisibleSessions;

    /// <summary>
    /// Gets or sets the status text.
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
    /// Gets whether a status message is visible.
    /// </summary>
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>
    /// Gets whether the picker is currently loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            _refreshCommand.RaiseCanExecuteChanged();
            _createNewSessionCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the refresh command.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the command that creates a new session.
    /// </summary>
    public ICommand CreateNewSessionCommand => _createNewSessionCommand;

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Loads the shared session catalog and rebuilds the visible rows.
    /// </summary>
    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusText = "Loading sessions...";

        try
        {
            await LoadSessionCatalogAsync();
            StatusText = HasVisibleSessions ? string.Empty : $"No saved sessions are available for {_agentDefinition.Name}.";
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load sessions: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRefresh() => !IsLoading;

    private bool CanCreateNewSession() => !IsLoading;

    private async Task LoadSessionCatalogAsync()
    {
        List<AgentSessionRecord> sessionRecords = await _agentSessionRepository.GetAllAsync();
        List<AgentSessionSnapshot> sessionSnapshots = sessionRecords.Select(session => new AgentSessionSnapshot(
            session.Id,
            session.AgentDefinitionId,
            session.AgentDefinition.Name,
            session.AgentDefinition.Description,
            session.AgentDefinition.Model,
            session.AgentDefinition.Provider.DisplayName,
            session.Summary,
            session.CreatedAtUtc,
            session.LastUsedAtUtc)).ToList();

        _sharedState.SetAgentSessions(sessionSnapshots);
        RefreshVisibleSessions();
    }

    private async Task CreateNewSessionAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        AgentRuntime? runtime = null;
        bool ownershipTransferred = false;

        try
        {
            StatusText = $"Creating a new session for {_agentDefinition.Name}...";
            UnitResult<IApplicationError> createResult = await _chatSessionService.CreateNewAgentSessionAsync(_agentDefinition);
            if (createResult.IsFailure)
            {
                StatusText = string.Join(" | ", createResult.Error.Messages);
                return;
            }

            runtime = _sharedState.AgentRuntime.HasValue
                ? _sharedState.AgentRuntime.Value
                : throw new InvalidOperationException("The new agent session was not activated.");
            ownershipTransferred = true;
            CloseRequested?.Invoke(runtime);
        }
        catch (Exception exception)
        {
            StatusText = $"Could not create a new session: {exception.Message}";
        }
        finally
        {
            if (!ownershipTransferred && runtime is not null)
            {
                await runtime.DisposeAsync();
            }

            IsLoading = false;
        }
    }

    private async Task SelectSessionAsync(AgentSessionSnapshot session)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusText = $"Restoring {session.DisplayLabel}...";

        AgentRuntime? runtime = null;
        bool ownershipTransferred = false;

        try
        {
            Result<AgentRuntime, IApplicationError> restoreResult =
                await _chatSessionService.RestoreAgentSessionAsync(session.Id, _agentDefinition.Id);
            if (restoreResult.IsFailure)
            {
                StatusText = string.Join(" | ", restoreResult.Error.Messages);
                return;
            }
            runtime = restoreResult.Value;
            ownershipTransferred = true;
            CloseRequested?.Invoke(runtime);
        }
        catch (Exception exception)
        {
            StatusText = $"Could not restore the selected session: {exception.Message}";
        }
        finally
        {
            if (!ownershipTransferred && runtime is not null)
            {
                await runtime.DisposeAsync();
            }

            IsLoading = false;
        }
    }

    private async Task DeleteSessionAsync(AgentSessionSnapshot session)
    {
        if (IsLoading)
        {
            return;
        }

        if (IsActiveSession(session))
        {
            StatusText = "The active session cannot be deleted while it is in use.";
            return;
        }

        IsLoading = true;
        StatusText = $"Deleting {session.DisplayLabel}...";

        try
        {
            AgentSessionRecord? sessionRecord = await _agentSessionRepository.GetAsync(session.Id);
            if (sessionRecord is null)
            {
                await LoadSessionCatalogAsync();
                StatusText = "The selected session no longer exists.";
                return;
            }

            _agentSessionRepository.Delete(sessionRecord);
            await _agentSessionRepository.SaveChangesAsync();
            await LoadSessionCatalogAsync();
            StatusText = $"Deleted {session.DisplayLabel}.";
        }
        catch (Exception exception)
        {
            StatusText = $"Could not delete the session: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool IsActiveSession(AgentSessionSnapshot session)
        => _sharedState.AgentRuntime.HasValue
            && _sharedState.AgentRuntime.Value.AgentRecordId == session.Id;

    private void CancelSelection()
        => CloseRequested?.Invoke(null);

    private void RefreshVisibleSessions()
    {
        List<AgentSessionPickerRowViewModel> sessionRows = [];

        IEnumerable<AgentSessionSnapshot> sessions = _sharedState.AgentSessions
            .Where(session => session.AgentDefinitionId == _agentDefinition.Id)
            .OrderByDescending(session => session.LastUsedAtUtc)
            .ThenByDescending(session => session.CreatedAtUtc);

        foreach (AgentSessionSnapshot session in sessions)
        {
            sessionRows.Add(new AgentSessionPickerRowViewModel(
                session,
                !IsActiveSession(session),
                SelectSessionAsync,
                DeleteSessionAsync));
        }

        VisibleSessions.Clear();

        foreach (AgentSessionPickerRowViewModel row in sessionRows)
        {
            VisibleSessions.Add(row);
        }

        OnPropertyChanged(nameof(HasVisibleSessions));
        OnPropertyChanged(nameof(HasNoVisibleSessions));
        OnPropertyChanged(nameof(HasEmptyState));
    }
}
