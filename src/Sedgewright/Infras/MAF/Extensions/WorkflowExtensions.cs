#pragma warning disable MAAIW001
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WorkflowChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using ToolApprovalRequestContent = Microsoft.Extensions.AI.ToolApprovalRequestContent;
using ToolApprovalResponseContent = Microsoft.Extensions.AI.ToolApprovalResponseContent;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class WorkflowExtensions
{
    extension(Workflow workflow)
    {
        public async Task<Result<string?, IApplicationError>> Run(
            List<string> messages,
            OrchestrationKind orchestrationKind,
            CancellationToken cancellationToken = default)
        {
            List<WorkflowChatMessage> chatMessages = [.. messages.Select(message => new WorkflowChatMessage(ChatRole.User, message))];
            string finalText = string.Empty;
            try
            {
                UnitResult<IApplicationError> executionResult = orchestrationKind switch
                {
                    OrchestrationKind.Sequential => await workflow.ExecuteSequentialAsync(
                        string.Join(Environment.NewLine + Environment.NewLine, messages),
                        onFinalOutput: AppendFinalOutputAsync,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    OrchestrationKind.Concurrent => await workflow.ExecuteConcurrentAsync(
                        string.Join(Environment.NewLine + Environment.NewLine, messages),
                        onFinalOutput: AppendFinalOutputAsync,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    OrchestrationKind.GroupChat => await workflow.ExecuteGroupChatAsync(
                        string.Join(Environment.NewLine + Environment.NewLine, messages),
                        onFinalOutput: AppendFinalOutputAsync,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    OrchestrationKind.Magentic => await workflow.ExecuteMagenticAsync(
                        string.Join(Environment.NewLine + Environment.NewLine, messages),
                        onFinalOutput: AppendFinalOutputAsync,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    OrchestrationKind.Handoff => await workflow.ExecuteHandoffAsync(
                        string.Join(Environment.NewLine + Environment.NewLine, messages),
                        onFinalOutput: AppendFinalOutputAsync,
                        cancellationToken: cancellationToken).ConfigureAwait(false),
                    _ => new InvalidOperationError([$"Unsupported orchestration kind '{orchestrationKind}'."]),
                };

                return executionResult.IsSuccess
                    ? finalText
                    : Result.Failure<string?, IApplicationError>(executionResult.Error);

                Task AppendFinalOutputAsync(List<MessageModel> result)
                {
                    finalText += string.Join(
                        Environment.NewLine + Environment.NewLine,
                        result
                            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                            .Select(message => $"{message.AuthorLabel}: {message.Content.Trim()}"));
                    return Task.CompletedTask;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred while running the workflow:");
                Console.WriteLine(e);
                return new ExceptionError([e.Message], e);
            }
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(ToolDefinition toolDefinition)
        {
            if (toolDefinition.SequentialOrchestration is not null)
            {
                return await Workflow.From(toolDefinition.SequentialOrchestration, toolDefinition.ToolDescription).ConfigureAwait(false);
            }

            if (toolDefinition.ConcurrentOrchestration is not null)
            {
                return await Workflow.From(toolDefinition.ConcurrentOrchestration, toolDefinition.ToolDescription).ConfigureAwait(false);
            }

            if (toolDefinition.GroupChatOrchestration is not null)
            {
                return await Workflow.From(toolDefinition.GroupChatOrchestration, toolDefinition.ToolDescription).ConfigureAwait(false);
            }

            if (toolDefinition.MagenticOrchestration is not null)
            {
                return await Workflow.From(toolDefinition.MagenticOrchestration, toolDefinition.ToolDescription).ConfigureAwait(false);
            }

            if (toolDefinition.HandoffOrchestration is not null)
            {
                return await Workflow.From(toolDefinition.HandoffOrchestration, toolDefinition.ToolDescription).ConfigureAwait(false);
            }

            return new InvalidOperationError([$"Tool definition '{toolDefinition.Name}' has no associated orchestration."]);
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(SequentialOrchestration orchestration, string? toolDescription = null)
        {
            List<SequentialOrchestrationAgent> orderedSteps = [.. orchestration.Steps.OrderBy(step => step.StepOrder)];

            if (orderedSteps.Count == 0)
            {
                return new ValidationError([$"Sequential orchestration '{orchestration.Name}' must contain at least one step."]);
            }

            Result<AgentRuntime, IApplicationError>[] agentResults = await Task.WhenAll(orderedSteps.Select(step => AIAgent.From(step.AgentDefinition))).ConfigureAwait(false);

            if (agentResults.Any(result => result.IsFailure))
            {
                await DisposeRuntimesAsync(agentResults).ConfigureAwait(false);
                return new GenericError(agentResults.Where(result => result.IsFailure).Select(result => result.Error).GetMessages());
            }

            AgentRuntime[] runtimes = [.. agentResults.Select(result => result.Value)];
            Workflow builtWorkflow = AgentWorkflowBuilder.BuildSequential(runtimes.Select(runtime => runtime.Agent));
            return new WorkflowRuntime(builtWorkflow, runtimes, orchestration.Name, orchestration.Description, OrchestrationKind.Sequential, toolDescription);
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(ConcurrentOrchestration orchestration, string? toolDescription = null)
        {
            List<ConcurrentOrchestrationParticipant> participants = [.. orchestration.Participants];

            if (participants.Count == 0)
            {
                return new ValidationError([$"Concurrent orchestration '{orchestration.Name}' must contain at least one participant."]);
            }

            Result<AgentRuntime, IApplicationError>[] agentResults = await Task.WhenAll(participants.Select(participant => AIAgent.From(participant.AgentDefinition))).ConfigureAwait(false);

            if (agentResults.Any(result => result.IsFailure))
            {
                await DisposeRuntimesAsync(agentResults).ConfigureAwait(false);
                return new GenericError(agentResults.Where(result => result.IsFailure).Select(result => result.Error).GetMessages());
            }

            AgentRuntime[] runtimes = [.. agentResults.Select(result => result.Value)];
            Workflow builtWorkflow = AgentWorkflowBuilder.BuildConcurrent(runtimes.Select(runtime => runtime.Agent));
            return new WorkflowRuntime(builtWorkflow, runtimes, orchestration.Name, orchestration.Description, OrchestrationKind.Concurrent, toolDescription);
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(GroupChatOrchestration orchestration, string? toolDescription = null)
        {
            List<GroupChatOrchestrationParticipant> participants = [.. orchestration.Participants.OrderBy(participant => participant.StepOrder)];

            if (participants.Count == 0)
            {
                return new ValidationError([$"Group chat orchestration '{orchestration.Name}' must contain at least one participant."]);
            }

            if (orchestration.MaximumIterationCount <= 0)
            {
                return new ValidationError([$"Group chat orchestration '{orchestration.Name}' must have a positive maximum iteration count."]);
            }

            HashSet<int> participantIds = [];
            HashSet<int> stepOrders = [];

            foreach (GroupChatOrchestrationParticipant participant in participants)
            {
                if (!participantIds.Add(participant.AgentDefinitionId))
                {
                    return new ValidationError([$"Group chat orchestration '{orchestration.Name}' contains duplicate participant agent definitions."]);
                }

                if (!stepOrders.Add(participant.StepOrder))
                {
                    return new ValidationError([$"Group chat orchestration '{orchestration.Name}' contains duplicate participant step orders."]);
                }
            }

            Result<AgentRuntime, IApplicationError>[] agentResults = await Task.WhenAll(participants.Select(participant => AIAgent.From(participant.AgentDefinition))).ConfigureAwait(false);

            if (agentResults.Any(result => result.IsFailure))
            {
                await DisposeRuntimesAsync(agentResults).ConfigureAwait(false);
                return new GenericError(agentResults.Where(result => result.IsFailure).Select(result => result.Error).GetMessages());
            }

            AgentRuntime[] runtimes = [.. agentResults.Select(result => result.Value)];
            List<AIAgent> orderedAgents = [.. runtimes.Select(runtime => runtime.Agent)];

            Workflow builtWorkflow = AgentWorkflowBuilder
                .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents)
                {
                    MaximumIterationCount = orchestration.MaximumIterationCount,
                })
                .AddParticipants(orderedAgents.ToArray())
                .Build();
            return new WorkflowRuntime(builtWorkflow, runtimes, orchestration.Name, orchestration.Description, OrchestrationKind.GroupChat, toolDescription);
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(MagenticOrchestration orchestration, string? toolDescription = null)
        {
            List<MagenticOrchestrationParticipant> participants = [.. orchestration.Participants.OrderBy(participant => participant.StepOrder)];

            if (participants.Count == 0)
            {
                return new ValidationError([$"Magentic orchestration '{orchestration.Name}' must contain at least one participant."]);
            }

            if (orchestration.MaximumRoundCount <= 0)
            {
                return new ValidationError([$"Magentic orchestration '{orchestration.Name}' must have a positive maximum round count."]);
            }

            if (orchestration.MaximumStallCount < 0)
            {
                return new ValidationError([$"Magentic orchestration '{orchestration.Name}' must have a non-negative maximum stall count."]);
            }

            if (orchestration.MaximumResetCount < 0)
            {
                return new ValidationError([$"Magentic orchestration '{orchestration.Name}' must have a non-negative maximum reset count."]);
            }

            HashSet<int> participantIds = [];
            HashSet<int> stepOrders = [];

            foreach (MagenticOrchestrationParticipant participant in participants)
            {
                if (!participantIds.Add(participant.AgentDefinitionId))
                {
                    return new ValidationError([$"Magentic orchestration '{orchestration.Name}' contains duplicate participant agent definitions."]);
                }

                if (!stepOrders.Add(participant.StepOrder))
                {
                    return new ValidationError([$"Magentic orchestration '{orchestration.Name}' contains duplicate participant step orders."]);
                }
            }

            if (participantIds.Contains(orchestration.ManagerAgentDefinitionId))
            {
                return new ValidationError([$"Magentic orchestration '{orchestration.Name}' must use a manager agent that is distinct from its participants."]);
            }

            Result<AgentRuntime, IApplicationError> managerResult = await AIAgent.From(orchestration.ManagerAgentDefinition).ConfigureAwait(false);
            if (managerResult.IsFailure)
            {
                return Result.Failure<WorkflowRuntime, IApplicationError>(managerResult.Error);
            }

            Result<AgentRuntime, IApplicationError>[] participantResults = await Task.WhenAll(participants.Select(participant => AIAgent.From(participant.AgentDefinition))).ConfigureAwait(false);

            if (participantResults.Any(result => result.IsFailure))
            {
                await managerResult.Value.DisposeAsync().ConfigureAwait(false);
                await DisposeRuntimesAsync(participantResults).ConfigureAwait(false);
                return new GenericError(participantResults.Where(result => result.IsFailure).Select(result => result.Error).GetMessages());
            }

            AgentRuntime[] runtimes = [managerResult.Value, .. participantResults.Select(result => result.Value)];
            List<AIAgent> orderedParticipants = [.. participantResults.Select(result => result.Value.Agent)];
            Dictionary<string, string> executorDisplayNames = new(StringComparer.Ordinal);
            string managerExecutorId = managerResult.Value.Agent.BindAsExecutor(emitEvents: true).Id;
            executorDisplayNames[managerExecutorId] = orchestration.ManagerAgentDefinition.Name;
            for (int index = 0; index < participantResults.Length; index++)
            {
                executorDisplayNames[participantResults[index].Value.Agent.BindAsExecutor(emitEvents: true).Id] = participants[index].AgentDefinition.Name;
            }

            Workflow builtWorkflow = new MagenticWorkflowBuilder(managerResult.Value.Agent)
                .AddParticipants(orderedParticipants.ToArray())
                .WithName(orchestration.Name)
                .WithDescription(orchestration.Description ?? string.Empty)
                .RequirePlanSignoff(false)
                .WithMaxRounds(orchestration.MaximumRoundCount)
                .WithMaxStalls(orchestration.MaximumStallCount)
                .WithMaxResets(orchestration.MaximumResetCount)
                .Build();
            return new WorkflowRuntime(
                builtWorkflow,
                runtimes,
                orchestration.Name,
                orchestration.Description,
                OrchestrationKind.Magentic,
                toolDescription,
                executorDisplayNames,
                managerExecutorId);
        }

        public static async Task<Result<WorkflowRuntime, IApplicationError>> From(HandoffOrchestration orchestration, string? toolDescription = null)
        {
            List<HandoffOrchestrationParticipant> participants = [.. orchestration.Participants];

            if (participants.Count == 0)
            {
                return new ValidationError([$"Handoff orchestration '{orchestration.Name}' must contain at least one participant."]);
            }

            HashSet<int> participantIds = [];
            foreach (HandoffOrchestrationParticipant participant in participants)
            {
                if (!participantIds.Add(participant.AgentDefinitionId))
                {
                    return new ValidationError([$"Handoff orchestration '{orchestration.Name}' contains duplicate participant agent definitions."]);
                }
            }

            if (!participantIds.Contains(orchestration.StartAgentDefinitionId))
            {
                return new ValidationError([$"Handoff orchestration '{orchestration.Name}' must include the configured start agent as a participant."]);
            }

            foreach (HandoffOrchestrationRoute route in orchestration.Routes)
            {
                if (!participantIds.Contains(route.SourceAgentDefinitionId) || !participantIds.Contains(route.TargetAgentDefinitionId))
                {
                    return new ValidationError([$"Handoff orchestration '{orchestration.Name}' contains a route that references an agent outside of the participant list."]);
                }

                if (route.SourceAgentDefinitionId == route.TargetAgentDefinitionId)
                {
                    return new ValidationError([$"Handoff orchestration '{orchestration.Name}' contains a self-route for agent definition id {route.SourceAgentDefinitionId}."]);
                }
            }

            Result<AgentRuntime, IApplicationError>[] agentResults = await Task.WhenAll(participants.Select(participant => AIAgent.From(participant.AgentDefinition))).ConfigureAwait(false);

            if (agentResults.Any(result => result.IsFailure))
            {
                await DisposeRuntimesAsync(agentResults).ConfigureAwait(false);
                return new GenericError(agentResults.Where(result => result.IsFailure).Select(result => result.Error).GetMessages());
            }

            AgentRuntime[] runtimes = [.. agentResults.Select(result => result.Value)];
            Dictionary<int, AIAgent> agentsByDefinitionId = [];
            for (int index = 0; index < participants.Count; index++)
            {
                agentsByDefinitionId[participants[index].AgentDefinitionId] = runtimes[index].Agent;
            }

            AIAgent startAgent = agentsByDefinitionId[orchestration.StartAgentDefinitionId];
            HandoffWorkflowBuilder builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(startAgent);

            if (orchestration.IsAutonomousModeEnabled)
            {
                builder = builder.WithAutonomousMode();
            }

            if (orchestration.Routes.Count == 0)
            {
                foreach (KeyValuePair<int, AIAgent> sourceAgentPair in agentsByDefinitionId)
                {
                    List<AIAgent> targetAgents = [.. agentsByDefinitionId.Where(targetAgentPair => targetAgentPair.Key != sourceAgentPair.Key).Select(targetAgentPair => targetAgentPair.Value)];

                    if (targetAgents.Count > 0)
                    {
                        builder = builder.WithHandoffs(sourceAgentPair.Value, targetAgents);
                    }
                }
            }
            else
            {
                foreach (IGrouping<int, HandoffOrchestrationRoute> routeGroup in orchestration.Routes.GroupBy(route => route.SourceAgentDefinitionId))
                {
                    List<AIAgent> targetAgents = [.. routeGroup.Select(route => agentsByDefinitionId[route.TargetAgentDefinitionId]).DistinctBy(agent => agent)];

                    if (targetAgents.Count > 0)
                    {
                        builder = builder.WithHandoffs(agentsByDefinitionId[routeGroup.Key], targetAgents);
                    }
                }
            }

            return new WorkflowRuntime(builder.Build(), runtimes, orchestration.Name, orchestration.Description, OrchestrationKind.Handoff, toolDescription);
        }

        private static async Task DisposeRuntimesAsync(IEnumerable<Result<AgentRuntime, IApplicationError>> results)
        {
            foreach (AgentRuntime runtime in results.Where(result => result.IsSuccess).Select(result => result.Value))
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<UnitResult<IApplicationError>> ExecuteSequentialAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            List<WorkflowChatMessage> messages = [new(ChatRole.User, prompt)];
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    await PublishAgentResponseUpdateAsync(responseEvent, onIntermediateUpdate).ConfigureAwait(false);
                    continue;
                }

                if (await TryPublishWorkflowActivityAsync(evt, onIntermediateUpdate).ConfigureAwait(false))
                {
                    continue;
                }

                if (evt is ExecutorFailedEvent executorFailedEvent)
                {
                    await PublishExecutorFailureAsync(executorFailedEvent, onIntermediateUpdate).ConfigureAwait(false);
                    return CreateExecutorFailure(executorFailedEvent);
                }

                if (evt is WorkflowErrorEvent workflowErrorEvent)
                {
                    return CreateWorkflowError("sequential", workflowErrorEvent.Exception);
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    if (outputEvent.HasTag(OutputTag.Intermediate))
                    {
                        continue;
                    }

                    List<MessageModel> finalOutput = outputEvent.ProcessFinalOutput();
                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is RequestInfoEvent requestInfoEvent)
                {
                    if (await TryAutoApproveRequestAsync(run, requestInfoEvent, onIntermediateUpdate, "sequential").ConfigureAwait(false))
                    {
                        continue;
                    }

                    return CreateUnsupportedRequestFailure("sequential", requestInfoEvent);
                }
            }

            return UnitResult.Success<IApplicationError>();
        }

        public async Task<UnitResult<IApplicationError>> ExecuteConcurrentAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            List<WorkflowChatMessage> messages = [new(ChatRole.User, prompt)];
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    await PublishAgentResponseUpdateAsync(responseEvent, onIntermediateUpdate).ConfigureAwait(false);
                    continue;
                }

                if (await TryPublishWorkflowActivityAsync(evt, onIntermediateUpdate).ConfigureAwait(false))
                {
                    continue;
                }

                if (evt is ExecutorFailedEvent executorFailedEvent)
                {
                    await PublishExecutorFailureAsync(executorFailedEvent, onIntermediateUpdate).ConfigureAwait(false);
                    return CreateExecutorFailure(executorFailedEvent);
                }

                if (evt is WorkflowErrorEvent workflowErrorEvent)
                {
                    return CreateWorkflowError("concurrent", workflowErrorEvent.Exception);
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    if (outputEvent.HasTag(OutputTag.Intermediate))
                    {
                        continue;
                    }

                    List<MessageModel> finalOutput = outputEvent.ProcessFinalOutput();
                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is RequestInfoEvent requestInfoEvent)
                {
                    if (await TryAutoApproveRequestAsync(run, requestInfoEvent, onIntermediateUpdate, "concurrent").ConfigureAwait(false))
                    {
                        continue;
                    }

                    return CreateUnsupportedRequestFailure("concurrent", requestInfoEvent);
                }
            }

            return UnitResult.Success<IApplicationError>();
        }

        public async Task<UnitResult<IApplicationError>> ExecuteGroupChatAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            List<WorkflowChatMessage> messages = [new(ChatRole.User, prompt)];
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    await PublishAgentResponseUpdateAsync(responseEvent, onIntermediateUpdate).ConfigureAwait(false);
                    continue;
                }

                if (await TryPublishWorkflowActivityAsync(evt, onIntermediateUpdate).ConfigureAwait(false))
                {
                    continue;
                }

                if (evt is ExecutorFailedEvent executorFailedEvent)
                {
                    await PublishExecutorFailureAsync(executorFailedEvent, onIntermediateUpdate).ConfigureAwait(false);
                    return CreateExecutorFailure(executorFailedEvent);
                }

                if (evt is WorkflowErrorEvent workflowErrorEvent)
                {
                    return CreateWorkflowError("group chat", workflowErrorEvent.Exception);
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    if (outputEvent.HasTag(OutputTag.Intermediate))
                    {
                        continue;
                    }

                    List<MessageModel> finalOutput = outputEvent.ProcessFinalOutput();
                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is RequestInfoEvent requestInfoEvent)
                {
                    if (await TryAutoApproveRequestAsync(run, requestInfoEvent, onIntermediateUpdate, "group chat").ConfigureAwait(false))
                    {
                        continue;
                    }

                    return CreateUnsupportedRequestFailure("group chat", requestInfoEvent);
                }

                if (evt.GetType().Name.EndsWith("HaltEvent", StringComparison.Ordinal))
                {
                    break;
                }

                if (
                    evt.GetType().Name.EndsWith("StartedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("CompletedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("EndedEvent", StringComparison.Ordinal))
                {
                    continue;
                }

                continue;
            }

            return UnitResult.Success<IApplicationError>();
        }

        public async Task<UnitResult<IApplicationError>> ExecuteMagenticAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default,
            ILogger? logger = null,
            IReadOnlyDictionary<string, string>? executorDisplayNames = null,
            string? managerExecutorId = null)
        {
            List<WorkflowChatMessage> messages = [new(ChatRole.User, prompt)];
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);
            string managerName = ResolveMagenticExecutorName(executorDisplayNames, managerExecutorId);

            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (evt is ExecutorInvokedEvent invokedEvent)
                {
                    if (TryGetMagenticExecutorDisplayName(executorDisplayNames, invokedEvent.ExecutorId, out string invokedAgentName)
                        && onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            invokedAgentName,
                            "Started working.",
                            false,
                            IntermediateUpdateKind.ExecutorStarted,
                            invokedEvent.ExecutorId)).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is ExecutorCompletedEvent completedEvent)
                {
                    if (TryGetMagenticExecutorDisplayName(executorDisplayNames, completedEvent.ExecutorId, out string completedAgentName)
                        && onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            completedAgentName,
                            "Completed.",
                            false,
                            IntermediateUpdateKind.ExecutorCompleted,
                            completedEvent.ExecutorId)).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is WorkflowWarningEvent warningEvent)
                {
                    string warningText = warningEvent.Data as string ?? "The workflow reported a warning.";
                    logger?.LogWarning("Magentic workflow warning: {WarningText}", warningText);
                    if (onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            "Workflow",
                            warningText,
                            false,
                            IntermediateUpdateKind.Warning)).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is RequestInfoEvent requestInfoEvent)
                {
                    if (await TryAutoApproveRequestAsync(run, requestInfoEvent, onIntermediateUpdate, "Magentic").ConfigureAwait(false))
                    {
                        logger?.LogInformation("Magentic workflow automatically approved an external request of type {RequestType}.", requestInfoEvent.Request.GetType().Name);
                        continue;
                    }

                    string requestType = requestInfoEvent.Request.GetType().FullName ?? requestInfoEvent.Request.GetType().Name;
                    logger?.LogWarning("Magentic workflow received unsupported external request {RequestType}.", requestType);
                    return new InvalidOperationError([$"The Magentic workflow requested unsupported external data '{requestType}'."]);
                }

                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    string agentName = ResolveMagenticExecutorName(
                        executorDisplayNames,
                        responseEvent.ExecutorId);

                    logger?.LogTrace(
                        "Magentic workflow received an agent response update from {AgentName} ({ExecutorId}) containing {CharacterCount} characters.",
                        agentName,
                        responseEvent.ExecutorId,
                        responseEvent.Update.Text.Length);

                    await PublishAgentResponseUpdateAsync(
                        responseEvent,
                        onIntermediateUpdate,
                        executorDisplayNames).ConfigureAwait(false);

                    continue;
                }

                if (evt is MagenticPlanCreatedEvent planCreatedEvent)
                {
                    logger?.LogInformation(
                        "Magentic workflow created an initial plan containing {PlanCharacterCount} characters.",
                        planCreatedEvent.FullTaskLedger.Text.Length);
                    if (onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            managerName,
                            "Initial plan created.",
                            false,
                            IntermediateUpdateKind.Plan,
                            managerExecutorId,
                            Details: planCreatedEvent.FullTaskLedger.Text)).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is MagenticReplannedEvent replannedEvent)
                {
                    logger?.LogInformation(
                        "Magentic workflow created a revised plan containing {PlanCharacterCount} characters.",
                        replannedEvent.FullTaskLedger.Text.Length);
                    if (onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            managerName,
                            "Plan revised.",
                            false,
                            IntermediateUpdateKind.Replan,
                            managerExecutorId,
                            Details: replannedEvent.FullTaskLedger.Text)).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is MagenticProgressLedgerUpdatedEvent progressEvent)
                {
                    MagenticProgressLedger ledger = progressEvent.ProgressLedger;
                    string nextSpeaker = ResolveMagenticExecutorName(executorDisplayNames, ledger.NextSpeaker);
                    logger?.LogDebug(
                        "Magentic workflow progress updated: requestSatisfied={RequestSatisfied}, inLoop={IsInLoop}, progressing={IsProgressing}, nextSpeaker={NextSpeaker}, instructionCharacterCount={InstructionCharacterCount}.",
                        ledger.IsRequestSatisfied,
                        ledger.IsInLoop,
                        ledger.IsProgressBeingMade,
                        ledger.NextSpeaker,
                        ledger.InstructionOrQuestion?.Length ?? 0);
                    if (onIntermediateUpdate is not null)
                    {
                        await onIntermediateUpdate(new IntermediateUpdate(
                            managerName,
                            "Progress updated.",
                            false,
                            IntermediateUpdateKind.Progress,
                            managerExecutorId,
                            Progress: new MagenticProgressSnapshot(
                                ledger.IsRequestSatisfied,
                                ledger.IsInLoop,
                                ledger.IsProgressBeingMade,
                                nextSpeaker,
                                ledger.InstructionOrQuestion))).ConfigureAwait(false);
                    }

                    continue;
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    if (outputEvent.HasTag(OutputTag.Intermediate))
                    {
                        logger?.LogTrace("Magentic workflow emitted an intermediate output from {ExecutorId}.", outputEvent.ExecutorId);
                        continue;
                    }

                    string outputAuthorName = ResolveMagenticExecutorName(executorDisplayNames, outputEvent.ExecutorId);
                    List<MessageModel>? finalOutput = outputEvent is AgentResponseEvent finalResponseEvent
                        ? ProcessFinalOutput(finalResponseEvent.Response, outputAuthorName)
                        : outputEvent.Is<List<WorkflowChatMessage>>(out List<WorkflowChatMessage>? finalMessages) && finalMessages is not null
                            ? [.. finalMessages.Select(message => ConvertChatMessage(message, outputAuthorName))]
                            : null;
                    if (finalOutput is null)
                    {
                        logger?.LogTrace(
                            "Magentic workflow emitted a non-final output from {ExecutorId} with payload type {PayloadType}.",
                            outputEvent.ExecutorId,
                            outputEvent.Data?.GetType().FullName ?? "null");
                        continue;
                    }

                    logger?.LogInformation(
                        "Magentic workflow produced final output with {MessageCount} messages and {VisibleMessageCount} non-empty messages.",
                        finalOutput.Count,
                        finalOutput.Count(message => !string.IsNullOrWhiteSpace(message.Content)));
                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is WorkflowErrorEvent workflowErrorEvent)
                {
                    logger?.LogError(workflowErrorEvent.Exception, "Magentic workflow reported an execution error.");
                    return CreateWorkflowError("magentic", workflowErrorEvent.Exception);
                }

                if (evt is ExecutorFailedEvent executorFailedEvent)
                {
                    string failureDetails = executorFailedEvent.Data?.ToString() ?? "Unknown error.";
                    logger?.LogError(
                        "Magentic workflow executor {ExecutorId} failed: {FailureDetails}",
                        executorFailedEvent.ExecutorId,
                        failureDetails);
                    await PublishExecutorFailureAsync(executorFailedEvent, onIntermediateUpdate).ConfigureAwait(false);
                    return CreateExecutorFailure(executorFailedEvent);
                }

                if (evt.GetType().Name.EndsWith("HaltEvent", StringComparison.Ordinal))
                {
                    logger?.LogInformation("Magentic workflow halted.");
                    break;
                }

                if (
                    evt.GetType().Name.EndsWith("StartedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("CompletedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("EndedEvent", StringComparison.Ordinal))
                {
                    logger?.LogTrace("Magentic workflow lifecycle event: {EventType}.", evt.GetType().Name);
                    continue;
                }

                logger?.LogTrace("Magentic workflow received unhandled event {EventType}.", evt.GetType().FullName ?? evt.GetType().Name);
                continue;
            }

            return UnitResult.Success<IApplicationError>();
        }

        public async Task<UnitResult<IApplicationError>> ExecuteHandoffAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            List<WorkflowChatMessage> messages = [new(ChatRole.User, prompt)];
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            await using StreamingRunCancellationRegistration cancellationRegistration = new(run, cancellationToken);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            string? lastExecutorId = null;
            string? streamedExecutorId = null;
            List<MessageModel> streamedMessages = [];
            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    if (lastExecutorId is not null && responseEvent.ExecutorId != lastExecutorId)
                    {
                        if (onIntermediateUpdate is not null)
                        {
                            string targetName = string.IsNullOrWhiteSpace(responseEvent.Update.AuthorName)
                                ? responseEvent.ExecutorId
                                : responseEvent.Update.AuthorName;
                            await onIntermediateUpdate(new IntermediateUpdate(
                                targetName,
                                $"Work handed off to {targetName}.",
                                false,
                                IntermediateUpdateKind.Handoff,
                                responseEvent.ExecutorId)).ConfigureAwait(false);
                        }
                    }

                    lastExecutorId = responseEvent.ExecutorId;
                    if (!string.IsNullOrWhiteSpace(responseEvent.Update.Text))
                    {
                        string authorLabel = responseEvent.Update.AuthorName ?? responseEvent.ExecutorId;
                        if (streamedMessages.Count == 0 || !string.Equals(streamedExecutorId, responseEvent.ExecutorId, StringComparison.Ordinal))
                        {
                            streamedMessages.Add(new MessageModel(false, responseEvent.Update.Text, DateTime.UtcNow, authorLabel));
                            streamedExecutorId = responseEvent.ExecutorId;
                        }
                        else
                        {
                            streamedMessages[^1].AppendContent(responseEvent.Update.Text);
                        }

                    }
                    await PublishAgentResponseUpdateAsync(responseEvent, onIntermediateUpdate).ConfigureAwait(false);
                    continue;
                }

                if (await TryPublishWorkflowActivityAsync(evt, onIntermediateUpdate).ConfigureAwait(false))
                {
                    continue;
                }

                if (evt is ExecutorFailedEvent executorFailedEvent)
                {
                    await PublishExecutorFailureAsync(executorFailedEvent, onIntermediateUpdate).ConfigureAwait(false);
                    return CreateExecutorFailure(executorFailedEvent);
                }

                if (evt is WorkflowErrorEvent workflowErrorEvent)
                {
                    return CreateWorkflowError("handoff", workflowErrorEvent.Exception);
                }

                if (evt is AgentResponseEvent finalResponseEvent)
                {
                    List<MessageModel> finalOutput = ProcessFinalOutput(finalResponseEvent.Response);
                    if (!HasVisibleContent(finalOutput))
                    {
                        finalOutput = streamedMessages;
                    }

                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    if (outputEvent.HasTag(OutputTag.Intermediate))
                    {
                        continue;
                    }

                    List<MessageModel> finalOutput = outputEvent.ProcessFinalOutput();
                    if (!HasVisibleContent(finalOutput))
                    {
                        finalOutput = streamedMessages;
                    }

                    if (onFinalOutput is not null)
                    {
                        await onFinalOutput(finalOutput).ConfigureAwait(false);
                    }

                    break;
                }

                if (evt is RequestInfoEvent requestInfoEvent)
                {
                    if (await TryAutoApproveRequestAsync(run, requestInfoEvent, onIntermediateUpdate, "handoff").ConfigureAwait(false))
                    {
                        continue;
                    }

                    return CreateUnsupportedRequestFailure("handoff", requestInfoEvent);
                }

                if (evt.GetType().Name.EndsWith("HaltEvent", StringComparison.Ordinal))
                {
                    break;
                }

                if (
                    evt.GetType().Name.EndsWith("StartedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("CompletedEvent", StringComparison.Ordinal)
                    || evt.GetType().Name.EndsWith("EndedEvent", StringComparison.Ordinal))
                {
                    continue;
                }

                continue;
            }

            return UnitResult.Success<IApplicationError>();
        }
    }

    extension(WorkflowRuntime runtime)
    {
        public async Task<UnitResult<IApplicationError>> ExecuteSequentialAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await runtime.Workflow.ExecuteSequentialAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<UnitResult<IApplicationError>> ExecuteConcurrentAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await runtime.Workflow.ExecuteConcurrentAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<UnitResult<IApplicationError>> ExecuteGroupChatAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await runtime.Workflow.ExecuteGroupChatAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<UnitResult<IApplicationError>> ExecuteMagenticAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default,
            ILogger? logger = null)
        {
            try
            {
                return await runtime.Workflow.ExecuteMagenticAsync(
                    prompt,
                    onIntermediateUpdate,
                    onFinalOutput,
                    cancellationToken,
                    logger,
                    runtime.ExecutorDisplayNames,
                    runtime.ManagerExecutorId).ConfigureAwait(false);
            }
            finally
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<UnitResult<IApplicationError>> ExecuteHandoffAsync(
            string prompt,
            Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
            Func<List<MessageModel>, Task>? onFinalOutput = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await runtime.Workflow.ExecuteHandoffAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    extension(WorkflowOutputEvent outputEvent)
    {
        public List<MessageModel> ProcessFinalOutput(string? fallbackAuthorName = null)
            => outputEvent.As<List<WorkflowChatMessage>>()?.Select(message => ConvertChatMessage(message, fallbackAuthorName)).ToList() ?? [];
    }

    private static async Task PublishAgentResponseUpdateAsync(
        AgentResponseUpdateEvent responseEvent,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate,
        IReadOnlyDictionary<string, string>? executorDisplayNames = null)
    {
        if (onIntermediateUpdate is null)
        {
            return;
        }

        string source = string.IsNullOrWhiteSpace(responseEvent.Update.AuthorName)
            ? ResolveMagenticExecutorName(executorDisplayNames, responseEvent.ExecutorId)
            : responseEvent.Update.AuthorName;
        if (!string.IsNullOrEmpty(responseEvent.Update.Text))
        {
            await onIntermediateUpdate(new IntermediateUpdate(
                source,
                responseEvent.Update.Text,
                false,
                ExecutorId: responseEvent.ExecutorId,
                ResponseId: responseEvent.Update.ResponseId,
                MessageId: responseEvent.Update.MessageId)).ConfigureAwait(false);
        }

        if (responseEvent.Update.FinishReason is not null)
        {
            await onIntermediateUpdate(new IntermediateUpdate(
                source,
                "Agent response finished.",
                false,
                IntermediateUpdateKind.ResponseCompleted,
                responseEvent.ExecutorId,
                responseEvent.Update.ResponseId,
                responseEvent.Update.MessageId,
                Details: $"Finish reason: {responseEvent.Update.FinishReason}")).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryPublishWorkflowActivityAsync(
        WorkflowEvent workflowEvent,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate)
    {
        IntermediateUpdate? update = workflowEvent switch
        {
            ExecutorInvokedEvent started => new IntermediateUpdate(
                started.ExecutorId,
                "Execution started.",
                false,
                IntermediateUpdateKind.ExecutorStarted,
                started.ExecutorId),
            ExecutorCompletedEvent completed => new IntermediateUpdate(
                completed.ExecutorId,
                "Execution completed.",
                false,
                IntermediateUpdateKind.ExecutorCompleted,
                completed.ExecutorId),
            WorkflowWarningEvent warning => new IntermediateUpdate(
                "Workflow",
                warning.Data as string ?? "The workflow reported a warning.",
                false,
                IntermediateUpdateKind.Warning),
            _ => null,
        };

        if (update is null)
        {
            return false;
        }

        if (onIntermediateUpdate is not null)
        {
            await onIntermediateUpdate(update).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task PublishExecutorFailureAsync(
        ExecutorFailedEvent executorFailedEvent,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate)
    {
        if (onIntermediateUpdate is null)
        {
            return;
        }

        string details = executorFailedEvent.Data?.ToString() ?? "No diagnostic details were supplied.";
        await onIntermediateUpdate(new IntermediateUpdate(
            executorFailedEvent.ExecutorId,
            "Execution failed.",
            true,
            IntermediateUpdateKind.ExecutorFailed,
            executorFailedEvent.ExecutorId,
            Details: details)).ConfigureAwait(false);
    }

    private static InvalidOperationError CreateExecutorFailure(ExecutorFailedEvent executorFailedEvent)
        => new([$"Executor '{executorFailedEvent.ExecutorId}' failed."])
        {
            Details = executorFailedEvent.Data?.ToString() ?? "No diagnostic details were supplied.",
        };

    private static async Task<bool> TryAutoApproveRequestAsync(
        StreamingRun run,
        RequestInfoEvent requestInfoEvent,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate,
        string workflowName)
    {
        if (requestInfoEvent.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequest) && approvalRequest is not null)
        {
            ExternalResponse response = requestInfoEvent.Request.CreateResponse(
                approvalRequest.CreateResponse(true, $"Auto-approved {workflowName} tool call."));
            await run.SendResponseAsync(response).ConfigureAwait(false);

            if (onIntermediateUpdate is not null)
            {
                string toolName = approvalRequest.ToolCall is FunctionCallContent functionCall
                    ? functionCall.Name ?? "tool"
                    : "tool";
                await onIntermediateUpdate(new IntermediateUpdate(
                    workflowName,
                    $"{toolName} was automatically approved.",
                    false,
                    IntermediateUpdateKind.ApprovalAutoApproved,
                    CallId: approvalRequest.ToolCall.CallId)).ConfigureAwait(false);
            }

            return true;
        }

        if (requestInfoEvent.Request.TryGetDataAs(out MagenticPlanReviewRequest? reviewRequest) && reviewRequest is not null)
        {
            await run.SendResponseAsync(requestInfoEvent.Request.CreateResponse(reviewRequest.Approve())).ConfigureAwait(false);
            if (onIntermediateUpdate is not null)
            {
                await onIntermediateUpdate(new IntermediateUpdate(
                    workflowName,
                    "The Magentic plan review was automatically approved.",
                    false,
                    IntermediateUpdateKind.PlanAutoApproved)).ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }

    private static InvalidOperationError CreateUnsupportedRequestFailure(string workflowName, RequestInfoEvent requestInfoEvent)
    {
        string requestType = requestInfoEvent.Request.GetType().FullName ?? requestInfoEvent.Request.GetType().Name;
        return new InvalidOperationError([$"The {workflowName} workflow requested unsupported external data '{requestType}'."]);
    }

    private static List<MessageModel> ProcessFinalOutput(object response, string? fallbackAuthorName = null)
    {
        object? messagesValue = GetPropertyValue(response, ["Messages", "Message", "Contents", "Content"]);

        if (messagesValue is IEnumerable<WorkflowChatMessage> messages)
        {
            return [.. messages.Select(message => ConvertChatMessage(message, fallbackAuthorName))];
        }

        if (messagesValue is WorkflowChatMessage message)
        {
            return [ConvertChatMessage(message, fallbackAuthorName)];
        }

        object? textValue = GetPropertyValue(response, ["Text"]);
        if (textValue is string text && !string.IsNullOrWhiteSpace(text))
        {
            return [new MessageModel(false, text, DateTime.UtcNow, fallbackAuthorName)];
        }

        string responseText = response.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return [new MessageModel(false, responseText, DateTime.UtcNow, fallbackAuthorName)];
        }

        return [];
    }

    private static bool HasVisibleContent(IEnumerable<MessageModel> messages)
        => messages.Any(message => !string.IsNullOrWhiteSpace(message.Content));

    private static string SerializeRequestData(object? requestData)
    {
        if (requestData is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(requestData, requestData.GetType());
        }
        catch (Exception)
        {
            return requestData.ToString() ?? requestData.GetType().FullName ?? requestData.GetType().Name;
        }
    }

    private static UnitResult<IApplicationError> CreateWorkflowError(string workflowType, Exception? exception)
        => exception is null
        ? new GenericError([$"The {workflowType} workflow failed."])
        : new ExceptionError([exception.Message, exception.InnerException?.Message ?? string.Empty], exception);

    /// <summary>
    /// Resolves a Magentic update's display name, preferring a configured name for known executors.
    /// </summary>
    /// <param name="executorDisplayNames">Configured names indexed by workflow executor identifier.</param>
    /// <param name="executorId">The executor that emitted the update.</param>
    /// <returns>The configured agent name, executor identifier, or <c>Unknown</c>.</returns>
    public static string ResolveMagenticExecutorName(
        IReadOnlyDictionary<string, string>? executorDisplayNames,
        string? executorId)
    {
        if (TryGetMagenticExecutorDisplayName(executorDisplayNames, executorId, out string configuredName))
        {
            return configuredName;
        }

        return string.IsNullOrWhiteSpace(executorId) ? "Unknown" : executorId;
    }

    private static bool TryGetMagenticExecutorDisplayName(
        IReadOnlyDictionary<string, string>? executorDisplayNames,
        string? executorId,
        out string displayName)
    {
        if (
            !string.IsNullOrWhiteSpace(executorId)
            && executorDisplayNames is not null
            && executorDisplayNames.TryGetValue(executorId, out string? configuredName)
            && !string.IsNullOrWhiteSpace(configuredName))
        {
            displayName = configuredName;
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    private static MessageModel ConvertChatMessage(WorkflowChatMessage message, string? fallbackAuthorName = null)
    {
        object? roleValue = GetPropertyValue(message, ["Role"]);
        string roleText = roleValue?.ToString() ?? string.Empty;
        bool isUser = string.Equals(roleText, ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase);
        string content = ExtractMessageText(message);

        string? authorLabel = message.AuthorName;
        if (string.IsNullOrWhiteSpace(authorLabel))
        {
            authorLabel = fallbackAuthorName;
        }

        if (string.IsNullOrWhiteSpace(authorLabel))
        {
            authorLabel = string.IsNullOrWhiteSpace(roleText) ? null : roleText;
        }

        return new MessageModel(isUser, content, DateTime.UtcNow, authorLabel);
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
            List<string> contentParts = [];
            foreach (object contentItem in contents)
            {
                string contentText = contentItem.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(contentText))
                {
                    contentParts.Add(contentText);
                }
            }

            if (contentParts.Count > 0)
            {
                return string.Join(Environment.NewLine, contentParts);
            }
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
}
