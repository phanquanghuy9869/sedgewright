using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Provides the minimal chat surface state for Sedgewright.
/// </summary>
public sealed class ChatViewModel : ViewModelBase
{
    private const int MaxRetainedWorkflowActivitiesPerTurn = 5;

    private readonly IChatTargetDialogService _chatTargetDialogService;
    private readonly IChatSessionService _chatSessionService;
    private readonly IChatService _chatService;
    private readonly ILogger<ChatViewModel> _logger;
    private readonly AsyncRelayCommand _sendCommand;
    private readonly AsyncRelayCommand _cancelCommand;
    private readonly AsyncRelayCommand _openTargetPickerCommand;
    private readonly AsyncRelayCommand _newSessionCommand;
    private readonly AsyncRelayCommand _approveToolCommand;
    private readonly AsyncRelayCommand _rejectToolCommand;
    private readonly Dictionary<(string Source, string? ExecutorId, string? ResponseId, string? MessageId), MessageModel> _streamingMessagesByIdentity = [];
    private readonly Dictionary<(string Source, string? ExecutorId, string? ResponseId, string? MessageId), MessageModel> _streamingReasoningMessagesByIdentity = [];
    private CancellationTokenSource? _activeCancellationTokenSource;
    private TaskCompletionSource<bool>? _pendingApprovalTaskSource;
    private ToolApprovalRequestModel? _pendingApprovalRequest;
    private string _inputText = string.Empty;
    private string _statusText = "Select an agent or orchestration to begin.";
    private bool _isBusy;
    private bool _isInitialized;
    private IChatTarget? _selectedChatTarget;
    private ChatTargetIdentity? _selectedChatTargetIdentity;

    /// <summary>
    /// Creates the chat view-model.
    /// </summary>
    public ChatViewModel(
        IChatTargetDialogService chatTargetDialogService,
        IChatSessionService chatSessionService,
        IChatService chatService,
        ILogger<ChatViewModel> logger)
    {
        _chatTargetDialogService = chatTargetDialogService;
        _chatSessionService = chatSessionService;
        _chatService = chatService;
        _logger = logger;

        TimelineItems =
        [
            ChatTimelineItem.ForMessage(new MessageModel(false, "What are you looking for?", DateTime.UtcNow, "Sedgewright")),
        ];

        _sendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        _cancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
        _openTargetPickerCommand = new AsyncRelayCommand(OpenTargetPickerAsync, CanOpenTargetPicker);
        _newSessionCommand = new AsyncRelayCommand(CreateNewSessionAsync, CanCreateNewSession);
        _approveToolCommand = new AsyncRelayCommand(ApprovePendingToolAsync, CanApprovePendingTool);
        _rejectToolCommand = new AsyncRelayCommand(RejectPendingToolAsync, CanApprovePendingTool);
        LoadingFeedback.PropertyChanged += OnLoadingFeedbackPropertyChanged;
    }

    /// <summary>
    /// Gets the transcript messages and activities in their display order.
    /// </summary>
    public ObservableCollection<ChatTimelineItem> TimelineItems { get; }

    /// <summary>
    /// Temporary, in-progress messages streamed by each agent currently running in the active
    /// orchestration turn. Keyed internally by agent source name so concurrent agents each get
    /// their own concentrated bubble instead of interleaving into a single message. Cleared once
    /// the turn produces a final output, fails, or is canceled.
    /// </summary>
    public ObservableCollection<MessageModel> StreamingMessages { get; } = [];

    /// <summary>
    /// Gets activity updates for the active chat turn.
    /// </summary>
    public ObservableCollection<IntermediateUpdate> WorkflowActivities { get; } = [];

    /// <summary>
    /// Gets whether any streaming messages are visible.
    /// </summary>
    public bool HasStreamingMessages => StreamingMessages.Count > 0;

    /// <summary>
    /// Gets whether the active chat turn has workflow activity updates.
    /// </summary>
    public bool HasWorkflowActivities => WorkflowActivities.Count > 0;

    /// <summary>
    /// Gets the currently selected chat target.
    /// </summary>
    public IChatTarget? SelectedChatTarget
    {
        get => _selectedChatTarget;
        set
        {
            ChatTargetIdentity? newIdentity = value is null ? null : ChatTargetIdentity.From(value);
            if (Equals(_selectedChatTargetIdentity, newIdentity) && ReferenceEquals(_selectedChatTarget, value))
            {
                if (value is not null && value.IsAgent(out _))
                {
                    _ = LoadSelectedAgentSessionHistoryAsync(value);
                }

                return;
            }

            bool selectionChanged = !Equals(_selectedChatTargetIdentity, newIdentity);
            bool targetInstanceChanged = !ReferenceEquals(_selectedChatTarget, value);
            _selectedChatTarget = value;
            _selectedChatTargetIdentity = newIdentity;

            OnPropertyChanged(nameof(SelectedChatTarget));
            OnPropertyChanged(nameof(HasSelectedChatTarget));
            OnPropertyChanged(nameof(SelectedChatTargetSummary));
            OnPropertyChanged(nameof(IsNewSessionButtonVisible));
            _sendCommand.RaiseCanExecuteChanged();
            _newSessionCommand.RaiseCanExecuteChanged();

            if (selectionChanged || targetInstanceChanged)
            {
                ResetTranscript();
            }

            if (value is not null && value.IsAgent(out _))
            {
                _ = LoadSelectedAgentSessionHistoryAsync(value);
            }
        }
    }

    /// <summary>
    /// Gets whether a chat target has been selected.
    /// </summary>
    public bool HasSelectedChatTarget => _selectedChatTarget is not null;

    /// <summary>
    /// Gets whether the New session button should be shown instead of its loading indicator.
    /// </summary>
    public bool IsNewSessionButtonVisible => HasSelectedChatTarget && !LoadingFeedback.IsLoading;

    /// <summary>
    /// Gets loading feedback for the New session action.
    /// </summary>
    public LoadingFeedbackViewModel LoadingFeedback { get; } = new();

    /// <summary>
    /// Gets the compact label shown for the selected target.
    /// </summary>
    public string SelectedChatTargetSummary => _selectedChatTarget is null
        ? "Choose a target to send a prompt."
        : ChatTargetDisplayInfo.From(_selectedChatTarget).Summary;

    /// <summary>
    /// Gets the command that opens the chat target picker.
    /// </summary>
    public ICommand OpenTargetPickerCommand => _openTargetPickerCommand;

    /// <summary>
    /// Gets the command that starts a clean chat session for the selected target.
    /// </summary>
    public ICommand NewSessionCommand => _newSessionCommand;

    /// <summary>
    /// Gets the command that approves the pending tool request.
    /// </summary>
    public ICommand ApproveToolCommand => _approveToolCommand;

    /// <summary>
    /// Gets the command that rejects the pending tool request.
    /// </summary>
    public ICommand RejectToolCommand => _rejectToolCommand;

    /// <summary>
    /// Gets the command that sends the current prompt.
    /// </summary>
    public ICommand SendCommand => _sendCommand;

    /// <summary>
    /// Gets the command that cancels the active prompt.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets whether a chat turn is currently active.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNotBusy));
            _sendCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
            _openTargetPickerCommand.RaiseCanExecuteChanged();
            _newSessionCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets whether the chat is idle.
    /// </summary>
    public bool IsNotBusy => !_isBusy;

    /// <summary>
    /// Gets the status text displayed in the footer.
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
    /// Restores the last selected target and agent session before the chat view is shown.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        try
        {
            Result<IChatTarget?, IApplicationError> restoreResult =
                await _chatSessionService.RestoreLastTargetAsync();
            if (restoreResult.IsFailure)
            {
                StatusText = string.Join(" | ", restoreResult.Error.Messages);
                return;
            }

            if (restoreResult.Value is not null)
            {
                SelectedChatTarget = restoreResult.Value;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Could not restore the previous chat selection.");
            StatusText = $"Could not restore the previous chat selection: {exception.Message}";
        }
    }

    /// <summary>
    /// Gets or sets the prompt input text.
    /// </summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (!SetProperty(ref _inputText, value))
            {
                return;
            }

            _sendCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets whether the user is currently being asked to approve a tool call.
    /// </summary>
    public bool HasApprovalRequest => _pendingApprovalRequest is not null;

    /// <summary>
    /// Gets the approval prompt summary shown to the user.
    /// </summary>
    public string ApprovalRequestSummary => _pendingApprovalRequest is null
        ? string.Empty
        : _pendingApprovalRequest.Summary;

    /// <summary>
    /// Gets the details of the tool call awaiting approval.
    /// </summary>
    public string ApprovalRequestDetails => _pendingApprovalRequest?.Details ?? string.Empty;

    private bool CanSend()
        => !IsBusy
            && HasSelectedChatTarget
            && !string.IsNullOrWhiteSpace(InputText);

    private bool CanCreateNewSession() => !IsBusy && !LoadingFeedback.IsLoading && HasSelectedChatTarget;

    private bool CanCancel() => IsBusy;

    private bool CanOpenTargetPicker() => !IsBusy;

    private bool CanApprovePendingTool() => HasApprovalRequest;

    private void ResetTranscript()
    {
        ClearStreamingMessages();
        ClearWorkflowActivities();
        TimelineItems.Clear();
        MessageModel welcomeMessage = new(false, "What are you looking for?", DateTime.UtcNow, "Sedgewright");
        TimelineItems.Add(ChatTimelineItem.ForMessage(welcomeMessage));
    }

    private async Task OpenTargetPickerAsync()
    {
        IChatTarget? selectedTarget = await _chatTargetDialogService.ShowAsync(SelectedChatTarget);
        if (selectedTarget is not null)
        {
            if (ReferenceEquals(_selectedChatTarget, selectedTarget)
                && Equals(_selectedChatTargetIdentity, ChatTargetIdentity.From(selectedTarget)))
            {
                ResetTranscript();
            }

            SelectedChatTarget = selectedTarget;
            await SaveSelectedTargetAsync(selectedTarget);
        }
    }

    private async Task CreateNewSessionAsync()
    {
        IChatTarget? selectedTarget = SelectedChatTarget;
        if (selectedTarget is null)
        {
            return;
        }

        CancellationToken cancellationToken = LoadingFeedback.Begin("Starting a new session...");
        try
        {
            if (selectedTarget is AgentDefinition agentDefinition)
            {
                UnitResult<IApplicationError> createResult =
                    await _chatSessionService.CreateNewAgentSessionAsync(agentDefinition, cancellationToken);
                if (createResult.IsFailure)
                {
                    StatusText = string.Join(" | ", createResult.Error.Messages);
                    return;
                }
            }

            InputText = string.Empty;
            ResetTranscript();

            UnitResult<IApplicationError> saveResult = await _chatSessionService.SaveLastTargetAsync(
                selectedTarget,
                cancellationToken);
            StatusText = saveResult.IsFailure
                ? string.Join(" | ", saveResult.Error.Messages)
                : selectedTarget is AgentDefinition
                    ? "New agent session ready."
                    : $"{ChatTargetDisplayInfo.From(selectedTarget).Label} is ready for a new run.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Starting a new session was canceled.";
        }
        catch (Exception exception)
        {
            _logger.LogError("Could not start a new chat session.");
            StatusText = $"Could not start a new chat session: {exception.Message}";
        }
        finally
        {
            LoadingFeedback.Complete();
        }
    }

    private void OnLoadingFeedbackPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LoadingFeedbackViewModel.IsLoading))
        {
            return;
        }

        OnPropertyChanged(nameof(IsNewSessionButtonVisible));
        _newSessionCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveSelectedTargetAsync(IChatTarget? target)
    {
        try
        {
            UnitResult<IApplicationError> saveResult = await _chatSessionService.SaveLastTargetAsync(target);
            if (saveResult.IsFailure)
            {
                StatusText = string.Join(" | ", saveResult.Error.Messages);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Could not save the current chat selection.");
            StatusText = $"Could not save the current chat selection: {exception.Message}";
        }
    }

    private async Task LoadSelectedAgentSessionHistoryAsync(IChatTarget selectedTarget)
    {
        if (!selectedTarget.IsAgent(out _))
        {
            return;
        }

        ChatTargetIdentity selectedIdentity = ChatTargetIdentity.From(selectedTarget);

        try
        {
            Result<List<MessageModel>, IApplicationError> historyResult = await _chatService.GetSelectedAgentSessionMessagesAsync(selectedTarget);
            if (historyResult.IsFailure || historyResult.Value.Count == 0)
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (!Equals(_selectedChatTargetIdentity, selectedIdentity))
                {
                    return;
                }

                TimelineItems.Clear();
                foreach (MessageModel message in historyResult.Value)
                {
                    TimelineItems.Add(ChatTimelineItem.ForMessage(message));
                }
            });
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = $"Could not load session history: {exception.Message}");
        }
    }

    private async Task SendAsync()
    {
        IChatTarget? selectedTarget = SelectedChatTarget;
        if (selectedTarget is null)
        {
            await RunOnUiThreadAsync(() => StatusText = "Choose a target before sending a prompt.");
            return;
        }

        string prompt = InputText.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        InputText = string.Empty;
        IsBusy = true;

        CancellationTokenSource cancellationTokenSource = new();
        _activeCancellationTokenSource = cancellationTokenSource;
        ChatTargetDisplayInfo displayInfo = ChatTargetDisplayInfo.From(selectedTarget);
        long startedAt = Stopwatch.GetTimestamp();
        string outcome = "success";
        using Activity? activity = SedgewrightTelemetry.StartChatTurn();
        _logger.LogInformation("Chat turn started.");
        try
        {
            await RunOnUiThreadAsync(() =>
            {
                MessageModel userMessage = new(true, prompt, DateTime.UtcNow);
                TimelineItems.Add(ChatTimelineItem.ForMessage(userMessage));
                AddTimelineActivity(new IntermediateUpdate(
                    displayInfo.Label,
                    "Turn started.",
                    false,
                    IntermediateUpdateKind.RunStarted));
                StatusText = $"Running {displayInfo.Label}...";
            });

            UnitResult<IApplicationError> result = await _chatService.SendAsync(
                selectedTarget,
                prompt,
                onIntermediateUpdate: update => ApplyIntermediateUpdate(update),
                onFinalOutput: messages => ApplyFinalOutput(messages, prompt),
                onApprovalRequired: request => HandleApprovalRequiredAsync(request),
                cancellationToken: cancellationTokenSource.Token);

            if (result.IsFailure)
            {
                outcome = "failure";
                activity?.SetStatus(ActivityStatusCode.Error);
                _logger.LogError($"Chat turn returned an application failure. {string.Join(" | ", result.Error.Messages)}");
                string failureText = result.Error.Messages.Length > 0
                    ? string.Join(" | ", result.Error.Messages)
                    : $"{displayInfo.Label} failed.";
                string? failureDetails = GetFailureDetails(result.Error);
                string displayedFailureText = failureDetails is null
                    ? failureText
                    : "The chat turn failed.";

                await RunOnUiThreadAsync(() =>
                {
                    ClearStreamingMessages();
                    StatusText = displayedFailureText;
                    AddTimelineActivity(new IntermediateUpdate(
                        displayInfo.Label,
                        "The chat turn failed.",
                        true,
                        IntermediateUpdateKind.RunFailed));
                    MessageModel failureMessage = CreateFailureMessage(displayedFailureText, failureDetails);
                    TimelineItems.Add(ChatTimelineItem.ForMessage(failureMessage));
                });
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                ClearStreamingMessages();
                StatusText = $"Completed {displayInfo.Label}.";
                AddTimelineActivity(new IntermediateUpdate(
                    displayInfo.Label,
                    "Turn completed.",
                    false,
                    IntermediateUpdateKind.RunCompleted));
            });
            _logger.LogInformation("Chat turn completed.");
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            _logger.LogInformation("Chat turn was canceled.");
            await RunOnUiThreadAsync(() =>
            {
                ClearStreamingMessages();
                StatusText = "The chat turn was canceled.";
                AddTimelineActivity(new IntermediateUpdate(
                    displayInfo.Label,
                    "Turn canceled.",
                    false,
                    IntermediateUpdateKind.RunCanceled));
            });
        }
        catch (Exception exception)
        {
            outcome = "error";
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError("Chat turn failed with exception type {ExceptionType}.", exception.GetType().Name);
            const string failureSummary = "The chat turn failed.";
            await RunOnUiThreadAsync(() =>
            {
                ClearStreamingMessages();
                StatusText = failureSummary;
                AddTimelineActivity(new IntermediateUpdate(
                    displayInfo.Label,
                    failureSummary,
                    true,
                    IntermediateUpdateKind.RunFailed));
                MessageModel failureMessage = CreateFailureMessage(failureSummary, exception.ToString());
                TimelineItems.Add(ChatTimelineItem.ForMessage(failureMessage));
            });
        }
        finally
        {
            if (ReferenceEquals(_activeCancellationTokenSource, cancellationTokenSource))
            {
                _activeCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
            activity?.SetTag("sedgewright.chat.outcome", outcome);
            SedgewrightTelemetry.RecordChatTurn(outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            await RunOnUiThreadAsync(() =>
            {
                ClearWorkflowActivities();
                IsBusy = false;
            });
        }
    }

    private async Task CancelAsync()
    {
        CancellationTokenSource? cancellationTokenSource = _activeCancellationTokenSource;
        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        ResolvePendingApproval(false, false);
        await RunOnUiThreadAsync(() => StatusText = "Canceling the current chat turn...");
    }

    private async Task<bool> HandleApprovalRequiredAsync(ToolApprovalRequestModel request)
    {
        TaskCompletionSource<bool> approvalTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await RunOnUiThreadAsync(() =>
        {
            _pendingApprovalRequest = request;
            _pendingApprovalTaskSource = approvalTaskSource;
            StatusText = "Approval required";
            OnPropertyChanged(nameof(HasApprovalRequest));
            OnPropertyChanged(nameof(ApprovalRequestSummary));
            OnPropertyChanged(nameof(ApprovalRequestDetails));
            _approveToolCommand.RaiseCanExecuteChanged();
            _rejectToolCommand.RaiseCanExecuteChanged();
        });

        return await approvalTaskSource.Task.ConfigureAwait(false);
    }

    private async Task ApprovePendingToolAsync()
    {
        await ResolvePendingApprovalAsync(true).ConfigureAwait(false);
    }

    private async Task RejectPendingToolAsync()
    {
        await ResolvePendingApprovalAsync(false).ConfigureAwait(false);
    }

    private async Task ResolvePendingApprovalAsync(bool isApproved)
    {
        if (_pendingApprovalTaskSource is null)
        {
            return;
        }

        TaskCompletionSource<bool> approvalTaskSource = _pendingApprovalTaskSource;
        _pendingApprovalTaskSource = null;
        _pendingApprovalRequest = null;

        await RunOnUiThreadAsync(() =>
        {
            StatusText = isApproved ? "Tool approved." : "Tool rejected.";
            OnPropertyChanged(nameof(HasApprovalRequest));
            OnPropertyChanged(nameof(ApprovalRequestSummary));
            OnPropertyChanged(nameof(ApprovalRequestDetails));
            _approveToolCommand.RaiseCanExecuteChanged();
            _rejectToolCommand.RaiseCanExecuteChanged();
        });

        approvalTaskSource.TrySetResult(isApproved);
    }

    private void ResolvePendingApproval(bool isApproved, bool raiseUiState)
    {
        if (_pendingApprovalTaskSource is null)
        {
            return;
        }

        TaskCompletionSource<bool> approvalTaskSource = _pendingApprovalTaskSource;
        _pendingApprovalTaskSource = null;
        _pendingApprovalRequest = null;

        if (raiseUiState)
        {
            StatusText = isApproved ? "Tool approved." : "Tool rejected.";
            OnPropertyChanged(nameof(HasApprovalRequest));
            OnPropertyChanged(nameof(ApprovalRequestSummary));
            OnPropertyChanged(nameof(ApprovalRequestDetails));
            _approveToolCommand.RaiseCanExecuteChanged();
            _rejectToolCommand.RaiseCanExecuteChanged();
        }

        approvalTaskSource.TrySetResult(isApproved);
    }

    private async Task ApplyIntermediateUpdate(IntermediateUpdate update)
    {
        await RunOnUiThreadAsync(() =>
        {
            if (update.Kind == IntermediateUpdateKind.Reasoning)
            {
                string reasoningSource = NormalizeSource(update.Source);
                (string Source, string? ExecutorId, string? ResponseId, string? MessageId) reasoningIdentity =
                    (reasoningSource, update.ExecutorId, update.ResponseId, update.MessageId);
                if (_streamingReasoningMessagesByIdentity.TryGetValue(reasoningIdentity, out MessageModel? reasoningMessage))
                {
                    reasoningMessage.AppendContent(update.Update);
                }
                else
                {
                    reasoningMessage = new MessageModel(false, update.Update, DateTime.UtcNow, reasoningSource)
                    {
                        IsInProgress = true,
                        IsReasoning = true,
                    };
                    _streamingReasoningMessagesByIdentity[reasoningIdentity] = reasoningMessage;
                    TimelineItems.Add(ChatTimelineItem.ForMessage(reasoningMessage));
                }

                return;
            }

            if (update.IsError)
            {
                StatusText = update.Update;
            }

            if (update.Kind == IntermediateUpdateKind.ResponseCompleted)
            {
                (string Source, string? ExecutorId, string? ResponseId, string? MessageId) completedIdentity =
                    (NormalizeSource(update.Source), update.ExecutorId, update.ResponseId, update.MessageId);
                if (_streamingMessagesByIdentity.Remove(completedIdentity, out MessageModel? completedMessage))
                {
                    completedMessage.IsInProgress = false;
                }

                if (_streamingReasoningMessagesByIdentity.Remove(completedIdentity, out MessageModel? completedReasoningMessage))
                {
                    completedReasoningMessage.IsInProgress = false;
                }

                AddTimelineActivity(update);
                return;
            }

            if (update.Kind != IntermediateUpdateKind.Text)
            {
                AddTimelineActivity(update);
                return;
            }

            string source = NormalizeSource(update.Source);
            (string Source, string? ExecutorId, string? ResponseId, string? MessageId) identity =
                (source, update.ExecutorId, update.ResponseId, update.MessageId);

            if (_streamingMessagesByIdentity.TryGetValue(identity, out MessageModel? streamingMessage))
            {
                streamingMessage.AppendContent(update.Update);
                return;
            }

            streamingMessage = new MessageModel(false, update.Update, DateTime.UtcNow, source)
            {
                IsInProgress = true,
            };

            _streamingMessagesByIdentity[identity] = streamingMessage;
            StreamingMessages.Add(streamingMessage);
            TimelineItems.Add(ChatTimelineItem.ForMessage(streamingMessage));
            OnPropertyChanged(nameof(HasStreamingMessages));
        });
    }

    private void AddTimelineActivity(IntermediateUpdate update)
    {
        if (update.Kind is IntermediateUpdateKind.Usage or IntermediateUpdateKind.Reasoning
            || IsApprovalActivity(update.Kind))
        {
            return;
        }

        if (!IsPlanActivity(update.Kind)
            && WorkflowActivities.Count(activity => !IsPlanActivity(activity.Kind)) >= MaxRetainedWorkflowActivitiesPerTurn)
        {
            for (int activityIndex = 0; activityIndex < WorkflowActivities.Count; activityIndex++)
            {
                IntermediateUpdate oldestActivity = WorkflowActivities[activityIndex];
                if (IsPlanActivity(oldestActivity.Kind))
                {
                    continue;
                }

                WorkflowActivities.RemoveAt(activityIndex);
                for (int timelineIndex = 0; timelineIndex < TimelineItems.Count; timelineIndex++)
                {
                    if (ReferenceEquals(TimelineItems[timelineIndex].Activity, oldestActivity))
                    {
                        TimelineItems.RemoveAt(timelineIndex);
                        break;
                    }
                }

                break;
            }
        }

        WorkflowActivities.Add(update);
        TimelineItems.Add(ChatTimelineItem.ForActivity(update));
        OnPropertyChanged(nameof(HasWorkflowActivities));
    }

    private static bool IsPlanActivity(IntermediateUpdateKind kind)
        => kind is IntermediateUpdateKind.Plan or IntermediateUpdateKind.Replan;

    private static bool IsApprovalActivity(IntermediateUpdateKind kind)
        => kind is IntermediateUpdateKind.ApprovalRequested
            or IntermediateUpdateKind.ApprovalDecided
            or IntermediateUpdateKind.ApprovalAutoApproved
            or IntermediateUpdateKind.PlanAutoApproved;

    private static string NormalizeSource(string? source)
        => string.IsNullOrWhiteSpace(source) ? "Assistant" : source;

    private void ClearStreamingMessages()
    {
        foreach (MessageModel streamingMessage in _streamingMessagesByIdentity.Values)
        {
            streamingMessage.IsInProgress = false;
        }

        foreach (MessageModel reasoningMessage in _streamingReasoningMessagesByIdentity.Values)
        {
            reasoningMessage.IsInProgress = false;
        }

        _streamingMessagesByIdentity.Clear();
        _streamingReasoningMessagesByIdentity.Clear();
        StreamingMessages.Clear();
        OnPropertyChanged(nameof(HasStreamingMessages));
    }

    private void ClearWorkflowActivities()
    {
        WorkflowActivities.Clear();
        OnPropertyChanged(nameof(HasWorkflowActivities));
    }

    private static string? GetFailureDetails(IApplicationError error)
        => error switch
        {
            ExceptionError exceptionError => exceptionError.Exception.ToString(),
            InvalidOperationError { Details: string details } when !string.IsNullOrWhiteSpace(details) => details,
            _ => null,
        };

    private static MessageModel CreateFailureMessage(string summary, string? details)
        => new(false, summary, DateTime.UtcNow, "Sedgewright")
        {
            IsError = details is not null,
            ErrorDetails = details,
        };

    private async Task ApplyFinalOutput(List<MessageModel> finalOutput, string prompt)
    {
        await RunOnUiThreadAsync(() =>
        {
            List<MessageModel> messagesToAppend = [.. finalOutput];

            if (messagesToAppend.Count > 0 && messagesToAppend[0].IsUser && string.Equals(messagesToAppend[0].Content.Trim(), prompt, StringComparison.Ordinal))
            {
                messagesToAppend.RemoveAt(0);
            }

            List<MessageModel> unmatchedStreamingMessages = [.. StreamingMessages];
            foreach (MessageModel message in messagesToAppend)
            {
                MessageModel? matchingStreamingMessage = unmatchedStreamingMessages.FirstOrDefault(streamingMessage =>
                    string.Equals(streamingMessage.Content, message.Content, StringComparison.Ordinal)
                    && string.Equals(streamingMessage.AuthorLabel, message.AuthorLabel, StringComparison.Ordinal));
                matchingStreamingMessage ??= unmatchedStreamingMessages.FirstOrDefault(streamingMessage =>
                    string.Equals(streamingMessage.Content, message.Content, StringComparison.Ordinal));

                if (matchingStreamingMessage is null)
                {
                    TimelineItems.Add(ChatTimelineItem.ForMessage(message));
                    continue;
                }

                matchingStreamingMessage.Content = message.Content;
                matchingStreamingMessage.IsInProgress = false;
                unmatchedStreamingMessages.Remove(matchingStreamingMessage);
            }

            ClearStreamingMessages();
        });
    }

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
