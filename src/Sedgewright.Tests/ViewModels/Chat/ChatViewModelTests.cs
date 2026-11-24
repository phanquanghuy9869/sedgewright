using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Chat;

namespace Sedgewright.Tests.ViewModels.Chat;

[TestFixture]
public sealed class ChatViewModelTests
{
    private const string FailureSummary = "The chat turn failed.";

    [Test]
    public void SendCommand_SeparatesWorkflowActivityAndGroupsTextByResponseIdentity()
    {
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, _) =>
            {
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate(
                    "Writer",
                    "Started working.",
                    false,
                    IntermediateUpdateKind.ExecutorStarted,
                    "writer-executor"));
                await update(new IntermediateUpdate(
                    "Writer",
                    "First ",
                    false,
                    ResponseId: "response-1",
                    MessageId: "message-1"));
                await update(new IntermediateUpdate(
                    "Writer",
                    "response.",
                    false,
                    ResponseId: "response-1",
                    MessageId: "message-1"));
                await update(new IntermediateUpdate(
                    "Writer",
                    "Separate response.",
                    false,
                    ResponseId: "response-2",
                    MessageId: "message-2"));
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.WorkflowActivities, Is.Empty);
            Assert.That(viewModel.TimelineItems.Where(item => item.IsActivity).Select(item => item.Activity!.Update), Is.EqualTo(["Turn started.", "Started working.", "Turn completed."]));
            Assert.That(viewModel.StreamingMessages, Is.Empty);
            MessageModel[] writerMessages = viewModel.TimelineItems
                .Where(item => item.Message?.AuthorLabel == "Writer")
                .Select(item => item.Message!)
                .ToArray();
            Assert.That(writerMessages.Select(message => message.Content).ToArray(), Is.EqualTo(["First response.", "Separate response."]));
            ChatTimelineItem firstResponse = viewModel.TimelineItems.Single(item => item.Message?.Content == "First response.");
            ChatTimelineItem secondResponse = viewModel.TimelineItems.Single(item => item.Message?.Content == "Separate response.");
            Assert.That(viewModel.TimelineItems.IndexOf(firstResponse), Is.LessThan(viewModel.TimelineItems.IndexOf(secondResponse)));
        }
    }

    [Test]
    public void SendCommand_HidesUsageAndShowsProviderReasoningByDefault()
    {
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, onFinalOutput) =>
            {
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate("Writer", "Tokens reported: input 12, output 8, total 20.", false, IntermediateUpdateKind.Usage));
                await update(new IntermediateUpdate("Writer", "provider reasoning that is not opted in", false, IntermediateUpdateKind.Reasoning));
                Func<List<MessageModel>, Task> complete = onFinalOutput ?? throw new InvalidOperationException("Final-output callback was not provided.");
                await complete([new MessageModel(false, "Final answer.", DateTime.UtcNow, "Writer")]);
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.TimelineItems.Any(item => item.Activity?.Kind == IntermediateUpdateKind.Usage), Is.False);
            MessageModel reasoningMessage = viewModel.TimelineItems.Single(item => item.Message?.IsReasoning == true).Message!;
            Assert.That(reasoningMessage.Content, Is.EqualTo("provider reasoning that is not opted in"));
            Assert.That(reasoningMessage.IsInProgress, Is.False);
        }
    }

    [Test]
    public void SendCommand_GroupsProviderReasoningAsOneMessage()
    {
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, onFinalOutput) =>
            {
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate(
                    "Writer",
                    "First ",
                    false,
                    IntermediateUpdateKind.Reasoning,
                    ResponseId: "response-1",
                    MessageId: "message-1"));
                await update(new IntermediateUpdate(
                    "Writer",
                    "second thought.",
                    false,
                    IntermediateUpdateKind.Reasoning,
                    ResponseId: "response-1",
                    MessageId: "message-1"));
                await update(new IntermediateUpdate("Writer", "Tokens reported: total 20.", false, IntermediateUpdateKind.Usage));
                Func<List<MessageModel>, Task> complete = onFinalOutput ?? throw new InvalidOperationException("Final-output callback was not provided.");
                await complete([new MessageModel(false, "Final answer.", DateTime.UtcNow, "Writer")]);
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        MessageModel reasoningMessage = viewModel.TimelineItems.Single(item => item.Message?.IsReasoning == true).Message!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reasoningMessage.Content, Is.EqualTo("First second thought."));
            Assert.That(reasoningMessage.AuthorLabel, Does.Contain("provider-supplied"));
            Assert.That(reasoningMessage.IsInProgress, Is.False);
            Assert.That(viewModel.TimelineItems.Any(item => item.Activity?.Kind == IntermediateUpdateKind.Usage), Is.False);
        }
    }

    [Test]
    public void MessageModel_UsesThinkingLabelForStreamingReasoning()
    {
        MessageModel reasoningMessage = new(false, "Considering the request...", DateTime.UtcNow, "Writer")
        {
            IsReasoning = true,
            IsInProgress = true,
        };
        MessageModel assistantMessage = new(false, "Forming a response...", DateTime.UtcNow, "Writer")
        {
            IsInProgress = true,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reasoningMessage.InProgressLabel, Is.EqualTo("thinking..."));
            Assert.That(assistantMessage.InProgressLabel, Is.EqualTo("streaming..."));
        }

        reasoningMessage.IsInProgress = false;
        Assert.That(reasoningMessage.InProgressLabel, Is.EqualTo("thinking..."));
    }

    [Test]
    public void SendCommand_PromotesStreamedMessageAndKeepsActivityInTimelineOrder()
    {
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, onFinalOutput) =>
            {
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate("Writer", "The answer.", false, ResponseId: "response-1", MessageId: "message-1"));
                await update(new IntermediateUpdate("Workflow", "A tool was automatically approved.", false, IntermediateUpdateKind.ApprovalAutoApproved));

                Func<List<MessageModel>, Task> complete = onFinalOutput ?? throw new InvalidOperationException("Final-output callback was not provided.");
                await complete([new MessageModel(false, "The answer.", DateTime.UtcNow, "Writer")]);
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        ChatTimelineItem answer = viewModel.TimelineItems.Single(item => item.Message?.Content == "The answer.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.TimelineItems.Count(item => item.Message?.Content == "The answer."), Is.EqualTo(1));
            Assert.That(viewModel.TimelineItems.Where(item => item.IsActivity).Select(item => item.Activity!.Kind), Is.EqualTo([IntermediateUpdateKind.RunStarted, IntermediateUpdateKind.RunCompleted]));
            Assert.That(viewModel.WorkflowActivities, Is.Empty);
            Assert.That(answer.Message?.IsInProgress, Is.False);
            Assert.That(viewModel.StreamingMessages, Is.Empty);
            Assert.That(viewModel.StatusText, Is.EqualTo("Completed Test agent."));
            Assert.That(viewModel.TimelineItems.Last(item => item.Message is not null).Message?.Content, Is.EqualTo("The answer."));
        }
    }

    [Test]
    public void SendCommand_RetainsPlansHidesApprovalsAndCapsOtherActivities()
    {
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, onFinalOutput) =>
            {
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate("Planner", "Initial plan", false, IntermediateUpdateKind.Plan));
                for (int index = 1; index <= 7; index++)
                {
                    await update(new IntermediateUpdate("Worker", $"Tool activity {index}.", false, IntermediateUpdateKind.ToolCall));
                }

                await update(new IntermediateUpdate("Workflow", "Approval requested.", false, IntermediateUpdateKind.ApprovalRequested));
                await update(new IntermediateUpdate("Workflow", "Approval decided.", false, IntermediateUpdateKind.ApprovalDecided));
                await update(new IntermediateUpdate("Workflow", "Tool auto-approved.", false, IntermediateUpdateKind.ApprovalAutoApproved));
                await update(new IntermediateUpdate("Workflow", "Plan auto-approved.", false, IntermediateUpdateKind.PlanAutoApproved));
                await update(new IntermediateUpdate("Planner", "Revised plan", false, IntermediateUpdateKind.Replan));

                Func<List<MessageModel>, Task> complete = onFinalOutput ?? throw new InvalidOperationException("Final-output callback was not provided.");
                await complete([new MessageModel(false, "Final answer.", DateTime.UtcNow, "Worker")]);
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        IntermediateUpdate[] retainedActivities = viewModel.TimelineItems
            .Where(item => item.IsActivity)
            .Select(item => item.Activity!)
            .ToArray();
        ChatTimelineItem revisedPlan = viewModel.TimelineItems.Single(item => item.Activity?.Update == "Revised plan");
        ChatTimelineItem finalAnswer = viewModel.TimelineItems.Single(item => item.Message?.Content == "Final answer.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(retainedActivities.Select(activity => activity.Update), Is.EqualTo([
                "Initial plan",
                "Tool activity 4.",
                "Tool activity 5.",
                "Tool activity 6.",
                "Tool activity 7.",
                "Revised plan",
                "Turn completed.",
            ]));
            Assert.That(retainedActivities.Count(activity => activity.Kind is not IntermediateUpdateKind.Plan and not IntermediateUpdateKind.Replan), Is.EqualTo(5));
            Assert.That(retainedActivities.Any(activity => activity.Kind is IntermediateUpdateKind.ApprovalRequested or IntermediateUpdateKind.ApprovalDecided or IntermediateUpdateKind.ApprovalAutoApproved or IntermediateUpdateKind.PlanAutoApproved), Is.False);
            Assert.That(viewModel.TimelineItems.IndexOf(revisedPlan), Is.LessThan(viewModel.TimelineItems.IndexOf(finalAnswer)));
            Assert.That(viewModel.TimelineItems.Last(item => item.Message is not null).Message?.Content, Is.EqualTo("Final answer."));
            Assert.That(viewModel.WorkflowActivities, Is.Empty);
        }
    }

    [Test]
    public void SendCommand_AppliesActivityLimitIndependentlyPerTurn()
    {
        int turn = 0;
        FakeChatService chatService = new(
            () => Task.FromResult(UnitResult.Success<IApplicationError>()),
            async (onIntermediateUpdate, onFinalOutput) =>
            {
                int currentTurn = ++turn;
                Func<IntermediateUpdate, Task> update = onIntermediateUpdate ?? throw new InvalidOperationException("Intermediate callback was not provided.");
                await update(new IntermediateUpdate("Planner", $"Turn {currentTurn} plan", false, IntermediateUpdateKind.Plan));
                for (int index = 1; index <= 6; index++)
                {
                    await update(new IntermediateUpdate("Worker", $"Turn {currentTurn} event {index}", false, IntermediateUpdateKind.ToolCall));
                }

                Func<List<MessageModel>, Task> complete = onFinalOutput ?? throw new InvalidOperationException("Final-output callback was not provided.");
                await complete([new MessageModel(false, $"Turn {currentTurn} answer", DateTime.UtcNow, "Worker")]);
            });
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);
        viewModel.InputText = "Second prompt";
        viewModel.SendCommand.Execute(null);

        string[] retainedUpdates = viewModel.TimelineItems
            .Where(item => item.Activity is not null)
            .Select(item => item.Activity!.Update)
            .ToArray();
        using (Assert.EnterMultipleScope())
        {
            foreach (int currentTurn in new[] { 1, 2 })
            {
                Assert.That(retainedUpdates, Does.Contain($"Turn {currentTurn} plan"));
                Assert.That(retainedUpdates, Does.Contain($"Turn {currentTurn} event 3"));
                Assert.That(retainedUpdates, Does.Contain($"Turn {currentTurn} event 4"));
                Assert.That(retainedUpdates, Does.Contain($"Turn {currentTurn} event 5"));
                Assert.That(retainedUpdates, Does.Contain($"Turn {currentTurn} event 6"));
                Assert.That(retainedUpdates, Does.Contain("Turn completed."));
                Assert.That(retainedUpdates, Does.Not.Contain($"Turn {currentTurn} event 1"));
                Assert.That(retainedUpdates, Does.Not.Contain($"Turn {currentTurn} event 2"));
            }

            Assert.That(viewModel.TimelineItems.Any(item => item.Message?.Content == "Turn 1 answer"), Is.True);
            Assert.That(viewModel.TimelineItems.Last(item => item.Message is not null).Message?.Content, Is.EqualTo("Turn 2 answer"));
            Assert.That(viewModel.WorkflowActivities, Is.Empty);
        }
    }

    [Test]
    public void SendCommand_WhenServiceReturnsExceptionError_AddsCollapsedErrorWithFullDetails()
    {
        InvalidOperationException exception = new("Sensitive exception message.");
        FakeChatService chatService = new(() => Task.FromResult(
            UnitResult.Failure<IApplicationError>(new ExceptionError([exception.Message], exception))));
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        MessageModel message = viewModel.TimelineItems.Last(item => item.Message is not null).Message!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo(FailureSummary));
            Assert.That(message.IsError, Is.True);
            Assert.That(message.ErrorDetails, Does.Contain(exception.Message));
            Assert.That(message.ErrorDetails, Does.Contain(nameof(InvalidOperationException)));
            Assert.That(message.IsExpanded, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo(FailureSummary));
        }
    }

    [Test]
    public void SendCommand_WhenServiceReturnsDetailedInvalidOperationError_HidesDetailsUntilExpanded()
    {
        const string diagnostics = "Sensitive executor exception and stack trace.";
        InvalidOperationError error = new(["Executor 'MagenticOrchestrator' failed."])
        {
            Details = diagnostics,
        };
        FakeChatService chatService = new(() => Task.FromResult(
            UnitResult.Failure<IApplicationError>(error)));
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        MessageModel message = viewModel.TimelineItems.Last(item => item.Message is not null).Message!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo(FailureSummary));
            Assert.That(message.Content, Does.Not.Contain(diagnostics));
            Assert.That(message.ErrorDetails, Is.EqualTo(diagnostics));
            Assert.That(message.IsExpanded, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo(FailureSummary));
        }
    }

    [Test]
    public void SendCommand_WhenServiceThrows_AddsCollapsedErrorWithFullDetails()
    {
        InvalidOperationException exception = new("Sensitive thrown exception.");
        FakeChatService chatService = new(() => Task.FromException<UnitResult<IApplicationError>>(exception));
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        MessageModel message = viewModel.TimelineItems.Last(item => item.Message is not null).Message!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo(FailureSummary));
            Assert.That(message.IsError, Is.True);
            Assert.That(message.ErrorDetails, Does.Contain(exception.Message));
            Assert.That(message.IsExpanded, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo(FailureSummary));
        }
    }

    [Test]
    public void SendCommand_WhenServiceReturnsNonExceptionError_PreservesFailureMessage()
    {
        const string failure = "The selected agent is unavailable.";
        FakeChatService chatService = new(() => Task.FromResult(
            UnitResult.Failure<IApplicationError>(new GenericError([failure]))));
        ChatViewModel viewModel = CreateViewModel(chatService);

        viewModel.SendCommand.Execute(null);

        MessageModel message = viewModel.TimelineItems.Last(item => item.Message is not null).Message!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo(failure));
            Assert.That(message.IsError, Is.False);
            Assert.That(message.ErrorDetails, Is.Null);
            Assert.That(viewModel.StatusText, Is.EqualTo(failure));
        }
    }

    [Test]
    public void NewSessionCommand_CreatesAgentSessionAndResetsChatScreen()
    {
        FakeChatSessionService sessionService = new();
        ChatViewModel viewModel = CreateViewModel(new FakeChatService(
            () => Task.FromResult(UnitResult.Success<IApplicationError>())), sessionService);
        viewModel.TimelineItems.Add(ChatTimelineItem.ForMessage(
            new MessageModel(true, "Old prompt", DateTime.UtcNow)));

        viewModel.NewSessionCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sessionService.CreatedAgentDefinitionIds, Is.EqualTo([1]));
            Assert.That(sessionService.SavedTarget, Is.SameAs(viewModel.SelectedChatTarget));
            Assert.That(viewModel.InputText, Is.Empty);
            Assert.That(viewModel.TimelineItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.TimelineItems[0].Message?.Content, Is.EqualTo("What are you looking for?"));
        }
    }

    [Test]
    public void NewSessionLoadingFeedback_HidesButtonAndDisablesCommand()
    {
        ChatViewModel viewModel = CreateViewModel(new FakeChatService(
            () => Task.FromResult(UnitResult.Success<IApplicationError>())));

        Assert.That(viewModel.IsNewSessionButtonVisible, Is.True);

        viewModel.LoadingFeedback.Begin("Starting a new session...");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsNewSessionButtonVisible, Is.False);
            Assert.That(viewModel.NewSessionCommand.CanExecute(null), Is.False);
        }

        viewModel.LoadingFeedback.Complete();

        Assert.That(viewModel.IsNewSessionButtonVisible, Is.True);
    }

    [Test]
    public async Task InitializeAsync_RestoresLastSelectedTarget()
    {
        AgentDefinition expectedTarget = new()
        {
            Id = 7,
            Name = "Restored agent",
            Description = "Previously selected agent",
            Model = "test-model",
        };
        FakeChatSessionService sessionService = new() { RestoredTarget = expectedTarget };
        ChatViewModel viewModel = new(
            new FakeChatTargetDialogService(),
            sessionService,
            new FakeChatService(() => Task.FromResult(UnitResult.Success<IApplicationError>())),
            NullLogger<ChatViewModel>.Instance);

        await viewModel.InitializeAsync();

        Assert.That(viewModel.SelectedChatTarget, Is.SameAs(expectedTarget));
    }

    [Test]
    public void NewSessionCommand_ResetsWorkflowTranscriptAndRetainsWorkflowSelection()
    {
        SequentialOrchestration workflow = new()
        {
            Id = 12,
            Name = "Test workflow",
        };
        FakeChatSessionService sessionService = new();
        ChatViewModel viewModel = new(
            new FakeChatTargetDialogService(),
            sessionService,
            new FakeChatService(() => Task.FromResult(UnitResult.Success<IApplicationError>())),
            NullLogger<ChatViewModel>.Instance)
        {
            SelectedChatTarget = workflow,
            InputText = "Old workflow prompt",
        };
        viewModel.TimelineItems.Add(ChatTimelineItem.ForMessage(
            new MessageModel(true, "Old workflow prompt", DateTime.UtcNow)));

        viewModel.NewSessionCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sessionService.CreatedAgentDefinitionIds, Is.Empty);
            Assert.That(sessionService.SavedTarget, Is.SameAs(workflow));
            Assert.That(viewModel.SelectedChatTarget, Is.SameAs(workflow));
            Assert.That(viewModel.InputText, Is.Empty);
            Assert.That(viewModel.TimelineItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.TimelineItems[0].Message?.Content, Is.EqualTo("What are you looking for?"));
        }
    }

    private static ChatViewModel CreateViewModel(IChatService chatService)
        => CreateViewModel(chatService, new FakeChatSessionService());

    private static ChatViewModel CreateViewModel(IChatService chatService, IChatSessionService chatSessionService)
    {
        ChatViewModel viewModel = new(
            new FakeChatTargetDialogService(),
            chatSessionService,
            chatService,
            NullLogger<ChatViewModel>.Instance)
        {
            SelectedChatTarget = new AgentDefinition
            {
                Id = 1,
                Name = "Test agent",
                Description = "Test agent description",
                Model = "test-model",
            },
            InputText = "Test prompt",
        };
        return viewModel;
    }

    private sealed class FakeChatSessionService : IChatSessionService
    {
        public List<int> CreatedAgentDefinitionIds { get; } = [];

        public IChatTarget? RestoredTarget { get; init; }

        public IChatTarget? SavedTarget { get; private set; }

        public Task<Result<IChatTarget?, IApplicationError>> RestoreLastTargetAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success<IChatTarget?, IApplicationError>(RestoredTarget));

        public Task<UnitResult<IApplicationError>> SaveLastTargetAsync(
            IChatTarget? target,
            CancellationToken cancellationToken = default)
        {
            SavedTarget = target;
            return Task.FromResult(UnitResult.Success<IApplicationError>());
        }

        public Task<UnitResult<IApplicationError>> CreateNewAgentSessionAsync(
            AgentDefinition agentDefinition,
            CancellationToken cancellationToken = default)
        {
            CreatedAgentDefinitionIds.Add(agentDefinition.Id);
            return Task.FromResult(UnitResult.Success<IApplicationError>());
        }

        public Task<Result<AgentRuntime, IApplicationError>> RestoreAgentSessionAsync(
            int sessionId,
            int agentDefinitionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure<AgentRuntime, IApplicationError>(
                new InvalidOperationError(["Fake session restoration is not configured."])));
    }

    private sealed class FakeChatTargetDialogService : IChatTargetDialogService
    {
        public Task<IChatTarget?> ShowAsync(IChatTarget? selectedTarget = null)
            => Task.FromResult<IChatTarget?>(null);
    }

    private sealed class FakeChatService(
        Func<Task<UnitResult<IApplicationError>>> sendAsync,
        Func<Func<IntermediateUpdate, Task>?, Func<List<MessageModel>, Task>?, Task>? publishUpdates = null) : IChatService
    {
        public async Task<UnitResult<IApplicationError>> SendAsync(
            IChatTarget selectedTarget,
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
            CancellationToken cancellationToken = default)
        {
            if (publishUpdates is not null)
            {
                await publishUpdates(onIntermediateUpdate, onFinalOutput);
            }

            return await sendAsync();
        }

        public Task<Result<List<MessageModel>, IApplicationError>> GetSelectedAgentSessionMessagesAsync(
            IChatTarget selectedTarget,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success<List<MessageModel>, IApplicationError>([]));
    }
}
