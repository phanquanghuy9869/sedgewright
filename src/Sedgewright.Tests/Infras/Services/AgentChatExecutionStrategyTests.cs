using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Services;

[TestFixture]
public sealed class AgentChatExecutionStrategyTests
{
    [Test]
    public async Task Execute_StreamsMessageListAndPublishesFinalOutput()
    {
        ScriptedAgent agent = new(
        [
            [
                new AgentResponseUpdate(ChatRole.Assistant, "Hello "),
                new AgentResponseUpdate(ChatRole.Assistant, "world") { FinishReason = ChatFinishReason.Stop },
            ],
        ]);
        List<ChatMessage> messages = [new(ChatRole.User, "Say hello")];
        List<IntermediateUpdate> intermediateUpdates = [];
        List<List<MessageModel>> finalOutputs = [];

        UnitResult<IApplicationError> result = await new AgentChatExecutionStrategy(NullLogger<AgentChatExecutionStrategy>.Instance).Execute(
            agent,
            new TestAgentSession(),
            messages,
            update =>
            {
                intermediateUpdates.Add(update);
                return Task.CompletedTask;
            },
            output =>
            {
                finalOutputs.Add(output);
                return Task.CompletedTask;
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(agent.ReceivedMessages, Has.Count.EqualTo(1));
            Assert.That(agent.ReceivedMessages[0].Single().Text, Is.EqualTo("Say hello"));
            Assert.That(intermediateUpdates.Select(update => update.Update).ToList(), Has.Count.EqualTo(3));
            Assert.That(intermediateUpdates.Take(2).All(update => update.Kind == IntermediateUpdateKind.Text), Is.True);
            Assert.That(intermediateUpdates[2].Kind, Is.EqualTo(IntermediateUpdateKind.ResponseCompleted));
            Assert.That(finalOutputs, Has.Count.EqualTo(1));
            Assert.That(finalOutputs[0][0].Content, Is.EqualTo("Hello world"));
        }
    }

    [Test]
    public async Task Execute_SeparatesTextFromToolActivityAndEmitsProviderReasoning()
    {
        AgentResponseUpdate update = new(
            ChatRole.Assistant,
            new List<AIContent>
            {
                new TextContent("User-facing answer."),
                new TextReasoningContent("private provider reasoning"),
                new FunctionCallContent(
                    "call-42",
                    "search_files",
                    new Dictionary<string, object?> { ["query"] = "private tool argument" }),
                new FunctionResultContent(
                    "call-42",
                    new Dictionary<string, object?> { ["matches"] = 3, ["path"] = "src/Program.cs" }),
            })
        {
            AgentId = "agent-7",
            ResponseId = "response-42",
            MessageId = "message-42",
            FinishReason = ChatFinishReason.Stop,
        };
        ScriptedAgent agent = new([[update]]);
        List<IntermediateUpdate> intermediateUpdates = [];
        List<List<MessageModel>> finalOutputs = [];

        UnitResult<IApplicationError> result = await new AgentChatExecutionStrategy(NullLogger<AgentChatExecutionStrategy>.Instance).Execute(
            agent,
            new TestAgentSession(),
            [new ChatMessage(ChatRole.User, "Search safely")],
            update =>
            {
                intermediateUpdates.Add(update);
                return Task.CompletedTask;
            },
            output =>
            {
                finalOutputs.Add(output);
                return Task.CompletedTask;
            });

        IntermediateUpdate textUpdate = intermediateUpdates.Single(item => item.Kind == IntermediateUpdateKind.Text);
        IntermediateUpdate reasoningUpdate = intermediateUpdates.Single(item => item.Kind == IntermediateUpdateKind.Reasoning);
        IntermediateUpdate toolUpdate = intermediateUpdates.Single(item => item.Kind == IntermediateUpdateKind.ToolCall);
        IntermediateUpdate toolResultUpdate = intermediateUpdates.Single(item => item.Kind == IntermediateUpdateKind.ToolResult);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(finalOutputs.Single().Single().Content, Is.EqualTo("User-facing answer."));
            Assert.That(textUpdate.ExecutorId, Is.EqualTo("agent-7"));
            Assert.That(textUpdate.ResponseId, Is.EqualTo("response-42"));
            Assert.That(textUpdate.MessageId, Is.EqualTo("message-42"));
            Assert.That(reasoningUpdate.Update, Is.EqualTo("private provider reasoning"));
            Assert.That(reasoningUpdate.ResponseId, Is.EqualTo("response-42"));
            Assert.That(toolUpdate.CallId, Is.EqualTo("call-42"));
            Assert.That(toolUpdate.Update, Does.Contain("search_files"));
            Assert.That(toolUpdate.Details, Does.Contain("\"query\": \"private tool argument\""));
            Assert.That(toolResultUpdate.CallId, Is.EqualTo("call-42"));
            Assert.That(toolResultUpdate.Details, Does.Contain("\"matches\": 3"));
            Assert.That(toolResultUpdate.Details, Does.Contain("\"path\": \"src/Program.cs\""));
            Assert.That(finalOutputs.Single().Single().Content, Does.Not.Contain("private provider reasoning"));
        }
    }

    [Test]
    public async Task Execute_AutoApprovesToolRequestWithoutInvokingApprovalCallback()
    {
        ToolApprovalRequestContent approvalRequest = new(
            "request-1",
            new FunctionCallContent(
                "call-1",
                "search",
                new Dictionary<string, object?> { ["query"] = "weather" }));
        ScriptedAgent agent = new(
        [
            [new AgentResponseUpdate(
                ChatRole.Assistant,
                new List<AIContent> { approvalRequest })],
            [new AgentResponseUpdate(ChatRole.Assistant, "after") { FinishReason = ChatFinishReason.Stop }],
        ]);
        bool callbackWasInvoked = false;

        UnitResult<IApplicationError> result = await new AgentChatExecutionStrategy(NullLogger<AgentChatExecutionStrategy>.Instance).Execute(
            agent,
            new TestAgentSession(),
            [new ChatMessage(ChatRole.User, "Use the tool")],
            onApprovalRequired: request =>
            {
                callbackWasInvoked = true;
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(request.RequestId, Is.EqualTo("request-1"));
                    Assert.That(request.CallId, Is.EqualTo("call-1"));
                    Assert.That(request.ToolName, Is.EqualTo("search"));
                    Assert.That(request.Arguments, Does.Contain("\"query\": \"weather\""));
                    Assert.That(request.Summary, Is.EqualTo("Approve search?"));
                }
                return Task.FromResult(false);
            },
            onFinalOutput: output =>
            {
                Assert.That(output.Select(message => message.Content).ToList(), Is.EqualTo(["after"]));
                return Task.CompletedTask;
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(callbackWasInvoked, Is.False);
            Assert.That(agent.ReceivedMessages, Has.Count.EqualTo(2));
            Assert.That(agent.ReceivedMessages[1], Has.Count.EqualTo(1));
            Assert.That(agent.ReceivedMessages[1][0].Role, Is.EqualTo(ChatRole.User));
            ToolApprovalResponseContent approvalResponse = agent.ReceivedMessages[1][0].Contents.Single() as ToolApprovalResponseContent;
            Assert.That(approvalResponse, Is.Not.Null);
            Assert.That(approvalResponse!.Approved, Is.True);
        }
    }

    [Test]
    public async Task Execute_RecursivelyStreamsApprovalResponsesInConversationOrder()
    {
        ToolApprovalRequestContent approvalRequest = new("request-1", new ToolCallContent("call-1"));
        ScriptedAgent agent = new(
        [
            [new AgentResponseUpdate(
                ChatRole.Assistant,
                new List<AIContent> { new TextContent("Before "), approvalRequest })],
            [new AgentResponseUpdate(ChatRole.Assistant, "after") { FinishReason = ChatFinishReason.Stop }],
        ]);
        List<List<MessageModel>> finalOutputs = [];

        UnitResult<IApplicationError> result = await new AgentChatExecutionStrategy(NullLogger<AgentChatExecutionStrategy>.Instance).Execute(
            agent,
            new TestAgentSession(),
            [new ChatMessage(ChatRole.User, "Use the tool")],
            onFinalOutput: output =>
            {
                finalOutputs.Add(output);
                return Task.CompletedTask;
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(agent.ReceivedMessages, Has.Count.EqualTo(2));
            Assert.That(agent.ReceivedMessages[1], Has.Count.EqualTo(1));
            Assert.That(agent.ReceivedMessages[1][0].Role, Is.EqualTo(ChatRole.User));
            Assert.That(agent.ReceivedMessages[1][0].Contents.Single(), Is.TypeOf<ToolApprovalResponseContent>());
            Assert.That(finalOutputs.SelectMany(output => output).Select(message => message.Content).ToList(), Is.EqualTo(["Before after"]));
        }
    }

    [Test]
    public async Task Execute_ContinuesThroughMultipleApprovalRounds()
    {
        ToolApprovalRequestContent firstApproval = new("request-1", new ToolCallContent("call-1"));
        ToolApprovalRequestContent secondApproval = new("request-2", new ToolCallContent("call-2"));
        ToolApprovalRequestContent thirdApproval = new("request-3", new ToolCallContent("call-3"));
        ScriptedAgent agent = new(
        [
            [new AgentResponseUpdate(
                ChatRole.Assistant,
                new List<AIContent> { firstApproval, secondApproval })],
            [new AgentResponseUpdate(ChatRole.Assistant, new List<AIContent> { thirdApproval })],
            [new AgentResponseUpdate(ChatRole.Assistant, "done")],
        ]);

        UnitResult<IApplicationError> result = await new AgentChatExecutionStrategy(NullLogger<AgentChatExecutionStrategy>.Instance).Execute(
            agent,
            new TestAgentSession(),
            [new ChatMessage(ChatRole.User, "Start")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(agent.ReceivedMessages, Has.Count.EqualTo(3));
            Assert.That(agent.ReceivedMessages[1], Has.Count.EqualTo(2));
            Assert.That(agent.ReceivedMessages[2], Has.Count.EqualTo(1));
            Assert.That(agent.ReceivedMessages[1].All(message => message.Role == ChatRole.User), Is.True);
            Assert.That(agent.ReceivedMessages[2][0].Contents.Single(), Is.TypeOf<ToolApprovalResponseContent>());
        }
    }

    private sealed class TestAgentSession : AgentSession;

    private sealed class ScriptedAgent(IReadOnlyList<IReadOnlyList<AgentResponseUpdate>> responses) : AIAgent
    {
        private readonly Queue<IReadOnlyList<AgentResponseUpdate>> _responses = new(responses);

        public List<List<ChatMessage>> ReceivedMessages { get; } = [];

        public override string? Name => "Test Agent";

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSession>(new TestAgentSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(JsonDocument.Parse("{}").RootElement.Clone());

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSession>(new TestAgentSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new System.NotSupportedException();

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ReceivedMessages.Add([.. messages]);
            IReadOnlyList<AgentResponseUpdate> response = _responses.Dequeue();
            foreach (AgentResponseUpdate update in response)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
            }

            await Task.CompletedTask;
        }
    }
}
