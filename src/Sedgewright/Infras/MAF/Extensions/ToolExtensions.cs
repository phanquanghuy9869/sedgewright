using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class ToolExtensions
{
    extension(AITool)
    {
        /// <summary>
        /// Converts a fully hydrated workflow tool definition into an invokable AI tool.
        /// </summary>
        /// <returns>An AI function that executes the selected workflow for each invocation.</returns>
        [Obsolete("Use AIFunction.FromToolDefinition instead.")]
        public static AITool From(ToolDefinition tool)
        {
            if (tool.Type != ToolDefinitionType.Workflow)
            {
                throw new NotSupportedException($"Tool definition '{tool.Name}' has unsupported type '{tool.Type}'.");
            }

            List<IOrchestration> orchestrations = [
                .. new IOrchestration?[]
                {
                    tool.SequentialOrchestration,
                    tool.ConcurrentOrchestration,
                    tool.GroupChatOrchestration,
                    tool.MagenticOrchestration,
                    tool.HandoffOrchestration,
                }.OfType<IOrchestration>()
            ];

            if (orchestrations.Count != 1)
            {
                throw new InvalidOperationException($"Workflow tool definition '{tool.Name}' must reference exactly one orchestration.");
            }

            IOrchestration orchestration = orchestrations[0];
            return AIFunctionFactory.Create(
                (List<ChatMessage> messages, CancellationToken cancellationToken) => ExecuteWorkflowAsync(orchestration, messages, cancellationToken),
                name: tool.Name,
                description: GetDescription(orchestration));
        }
    }

    private static string GetDescription(IOrchestration orchestration)
        => orchestration switch
        {
            SequentialOrchestration sequential => sequential.Description ?? string.Empty,
            ConcurrentOrchestration concurrent => concurrent.Description ?? string.Empty,
            GroupChatOrchestration groupChat => groupChat.Description ?? string.Empty,
            MagenticOrchestration magentic => magentic.Description ?? string.Empty,
            HandoffOrchestration handoff => handoff.Description ?? string.Empty,
            _ => throw new NotSupportedException($"Orchestration type '{orchestration.GetType().Name}' is not supported.")
        };

    private static async Task<string> ExecuteWorkflowAsync(
        IOrchestration orchestration,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        Result<WorkflowRuntime, IApplicationError> runtimeResult = orchestration switch
        {
            SequentialOrchestration sequential => await Workflow.From(sequential).ConfigureAwait(false),
            ConcurrentOrchestration concurrent => await Workflow.From(concurrent).ConfigureAwait(false),
            GroupChatOrchestration groupChat => await Workflow.From(groupChat).ConfigureAwait(false),
            MagenticOrchestration magentic => await Workflow.From(magentic).ConfigureAwait(false),
            HandoffOrchestration handoff => await Workflow.From(handoff).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Orchestration type '{orchestration.GetType().Name}' is not supported.")
        };

        if (runtimeResult.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, runtimeResult.Error.Messages));
        }

        await using WorkflowRuntime runtime = runtimeResult.Value;
        await using StreamingRun run = await InProcessExecution
            .RunStreamingAsync(runtime.Workflow, messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);

        await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

        List<MessageModel>? finalOutput = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                finalOutput = outputEvent.ProcessFinalOutput();
                break;
            }

            if (evt is AgentResponseEvent responseEvent)
            {
                string responseText = ExtractResponseText(responseEvent.Response);
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    return responseText;
                }

                break;
            }

            if (evt is WorkflowErrorEvent workflowErrorEvent)
            {
                throw new InvalidOperationException(
                    workflowErrorEvent.Exception?.Message ?? $"The {orchestration.GetType().Name} workflow failed.",
                    workflowErrorEvent.Exception);
            }

            if (evt is ExecutorFailedEvent executorFailedEvent)
            {
                throw new InvalidOperationException(
                    $"Executor '{executorFailedEvent.ExecutorId}' failed: {executorFailedEvent.Data?.ToString() ?? "unknown error"}");
            }

            if (evt is RequestInfoEvent requestInfoEvent)
            {
                if (requestInfoEvent.Request.TryGetDataAs<ToolApprovalRequestContent>(out ToolApprovalRequestContent? approvalRequest) && approvalRequest is not null)
                {
                    ExternalResponse response = requestInfoEvent.Request.CreateResponse(
                        approvalRequest.CreateAlwaysApproveToolResponse("Auto-approved workflow tool call."));
                    await run.SendResponseAsync(response).ConfigureAwait(false);
                    continue;
                }

                if (requestInfoEvent.Request.TryGetDataAs<MagenticPlanReviewRequest>(out MagenticPlanReviewRequest? reviewRequest) && reviewRequest is not null)
                {
                    await run.SendResponseAsync(requestInfoEvent.Request.CreateResponse(reviewRequest.Approve())).ConfigureAwait(false);
                    continue;
                }

                throw new InvalidOperationException(
                    $"The workflow requested unsupported external data '{requestInfoEvent.Request.GetType().FullName}'.");
            }
        }

        string? finalText = finalOutput?
            .LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content))?
            .Content;

        return !string.IsNullOrWhiteSpace(finalText)
            ? finalText
            : throw new InvalidOperationException($"Workflow '{GetName(orchestration)}' completed without a final response.");
    }

    private static string ExtractResponseText(object response)
    {
        object? messagesValue = GetPropertyValue(response, ["Messages", "Message", "Contents", "Content"]);

        if (messagesValue is IEnumerable<ChatMessage> messages)
        {
            return string.Join(Environment.NewLine, messages.Select(ExtractMessageText).Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        if (messagesValue is ChatMessage message)
        {
            return ExtractMessageText(message);
        }

        object? textValue = GetPropertyValue(response, ["Text"]);
        if (textValue is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return response.ToString() ?? string.Empty;
    }

    private static string ExtractMessageText(object message)
    {
        object? textValue = GetPropertyValue(message, ["Text", "Content"]);
        if (textValue is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        object? contentsValue = GetPropertyValue(message, ["Contents"]);
        if (contentsValue is IEnumerable<object> contents)
        {
            return string.Join(
                Environment.NewLine,
                contents.Select(content => content.ToString() ?? string.Empty).Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        return message.ToString() ?? string.Empty;
    }

    private static object? GetPropertyValue(object value, IEnumerable<string> propertyNames)
    {
        Type type = value.GetType();

        foreach (string propertyName in propertyNames)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                continue;
            }

            object? propertyValue = property.GetValue(value);
            if (propertyValue is not null)
            {
                return propertyValue;
            }
        }

        return null;
    }

    private static string GetName(IOrchestration orchestration)
        => orchestration switch
        {
            SequentialOrchestration sequential => sequential.Name,
            ConcurrentOrchestration concurrent => concurrent.Name,
            GroupChatOrchestration groupChat => groupChat.Name,
            MagenticOrchestration magentic => magentic.Name,
            HandoffOrchestration handoff => handoff.Name,
            _ => orchestration.GetType().Name
        };
}
