using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Agents.AI.Tools.Shell;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Sedgewright.Entities;
using Sedgewright.Models;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sedgewright.Infras.MAF.Extensions;

public static class AIAgentExtensions
{
    private sealed record ClientTool(ChatClient ChatClient, IEnumerable<AITool> McpTools);
    private sealed record AgentConstruction(AIAgent Agent, ShellExecutor? ShellExecutor, IAsyncDisposable? OwnedResource);

#pragma warning disable GHCP001
    internal static PermissionDecision ApproveCopilotPermission()
        => PermissionDecision.ApproveOnce();
#pragma warning restore GHCP001

    extension(AgentRuntime runtime)
    {
        public async Task<Result<IReadOnlyCollection<ChatMessage>, IApplicationError>> GetMessages(CancellationToken cancellationToken = default)
        {
            if (runtime.AgentSession is not GitHubCopilotAgentSession copilotSession)
            {
                return runtime.Agent.GetMessages(runtime.AgentSession);
            }

            if (runtime.OwnedResource is not CopilotClient copilotClient)
            {
                return new InvalidOperationError(["The GitHub Copilot runtime client is not available."]);
            }

            if (string.IsNullOrWhiteSpace(copilotSession.SessionId))
            {
                return Array.Empty<ChatMessage>();
            }

            try
            {
                await using CopilotSession session = await copilotClient
                    .ResumeSessionAsync(copilotSession.SessionId, new ResumeSessionConfig(), cancellationToken)
                    .ConfigureAwait(false);

                IReadOnlyList<SessionEvent> sessionEvents = await session.GetEventsAsync(cancellationToken).ConfigureAwait(false);
                List<ChatMessage> messages = [];
                foreach (SessionEvent sessionEvent in sessionEvents)
                {
                    if (sessionEvent is UserMessageEvent userMessage
                        && !string.IsNullOrWhiteSpace(userMessage.Data?.Content))
                    {
                        messages.Add(new ChatMessage(ChatRole.User, userMessage.Data.Content));
                    }
                    else if (sessionEvent is AssistantMessageEvent assistantMessage
                        && !string.IsNullOrWhiteSpace(assistantMessage.Data?.Content))
                    {
                        messages.Add(new ChatMessage(ChatRole.Assistant, assistantMessage.Data.Content));
                    }
                }

                return messages;
            }
            catch (Exception exception)
            {
                return new ExceptionError(["Could not load GitHub Copilot chat history."], exception);
            }
        }
    }

    extension(AIAgent agent)
    {
        public Result<IReadOnlyCollection<ChatMessage>, IApplicationError> GetMessages(AgentSession session)
        {
            InMemoryChatHistoryProvider? historyProvider = agent.GetService<InMemoryChatHistoryProvider>();
            if (historyProvider is null)
            {
                return new InvalidOperationError(["Agent does not have a chat history provider."]);
            }
            List<ChatMessage> messages = historyProvider.GetMessages(session) ?? [];
            return messages;
        }

        public static Task<Result<AgentRuntime, IApplicationError>> From(
            AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
            => BuildAgentWithResources(agentDefinition, cancellationToken)
                .Map(construction => CreateSession(construction, cancellationToken));

        private static Task<Result<AgentConstruction, IApplicationError>> BuildAgentWithResources(
            AgentDefinition agentDefinition,
            CancellationToken cancellationToken)
        {
            if (agentDefinition.Provider.ProviderKind == LlmProviderKind.Copilot)
            {
                // if (agentDefinition is HarnessAgentDefinition)
                // {
                //     return Task.FromResult(Result.Failure<AgentConstruction, IApplicationError>(
                //         new InvalidOperationError(["GitHub Copilot does not support Harness agents."])));
                // }

                return FromCopilot(agentDefinition, cancellationToken);
            }

            if (agentDefinition is HarnessAgentDefinition harnessAgentDefinition)
            {
                return From(harnessAgentDefinition);
            }

            return AITool
                .FromAgentDefinition(agentDefinition)
                .Bind(GetClientTools)
                .Map(BuildAgent);

            Result<ClientTool, IApplicationError> GetClientTools(IEnumerable<AITool> mcpTools) =>
                ChatClient.FromAgentDefinition(agentDefinition)
                    .Map(chatClient => new ClientTool(chatClient, mcpTools));

            AgentConstruction BuildAgent(ClientTool clientTool) =>
                From(clientTool.ChatClient, agentDefinition, clientTool.McpTools);
        }

        private static Task<Result<AgentConstruction, IApplicationError>> From(
            HarnessAgentDefinition agentDefinition)
        {
            return AITool
                .FromAgentDefinition(agentDefinition)
                .Bind(GetClientTools)
                .Map(BuildAgent);

            Result<ClientTool, IApplicationError> GetClientTools(IEnumerable<AITool> mcpTools) =>
                ChatClient.FromAgentDefinition(agentDefinition)
                    .Map(chatClient => new ClientTool(chatClient, mcpTools));

            AgentConstruction BuildAgent(ClientTool clientTool) =>
                From(clientTool.ChatClient, agentDefinition, clientTool.McpTools);
        }

        private static async Task<AgentRuntime> CreateSession(
            AgentConstruction construction, CancellationToken cancellationToken)
        {
            try
            {
                AgentSession session = await construction.Agent
                    .CreateSessionAsync(cancellationToken)
                    .ConfigureAwait(false);

                return new AgentRuntime(construction.Agent, session, construction.ShellExecutor, construction.OwnedResource);
            }
            catch
            {
                await DisposeResourcesAsync(construction.ShellExecutor, construction.OwnedResource).ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<Result<AgentConstruction, IApplicationError>> FromCopilot(
            AgentDefinition agentDefinition,
            CancellationToken cancellationToken)
        {
            Result<IEnumerable<AITool>, IApplicationError> toolsResult = await AITool
                .FromAgentDefinition(agentDefinition)
                .ConfigureAwait(false);
            if (toolsResult.IsFailure)
            {
                return Result.Failure<AgentConstruction, IApplicationError>(toolsResult.Error);
            }

            CopilotClient copilotClient = new(new CopilotClientOptions { UseLoggedInUser = true });
            ShellExecutor? shellExecutor = null;
            try
            {
                List<AIFunctionDeclaration> copilotTools = [.. toolsResult.Value.Select(ToCopilotTool)];
                await copilotClient.StartAsync(cancellationToken).ConfigureAwait(false);

                if (agentDefinition.ShellConfiguration.IsActive)
                {
                    shellExecutor = ShellExecutorExtensions.From(agentDefinition.ShellConfiguration);
                    copilotTools.Add(ToCopilotTool(shellExecutor.AsAIFunction()));
                }

                List<AgentSkillDefinition> skillDefinitions = agentDefinition.Skills;
                string[] skillDirectories = [.. skillDefinitions
                    .Select(skill => skill.SkillDirectory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
                string[] selectedSkills = [.. skillDefinitions
                    .SelectMany(skill => skill.SelectedSkills)
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
                CustomAgentConfig? customAgent = skillDirectories.Length == 0
                    ? null
                    : new CustomAgentConfig
                    {
                        Name = $"sentinel-{agentDefinition.Id}",
                        DisplayName = agentDefinition.Name,
                        Description = agentDefinition.Description,
                        Skills = selectedSkills.Length == 0 ? null : selectedSkills,
                    };

                SessionConfig sessionConfig = new()
                {
                    Model = agentDefinition.Model,
                    SystemMessage = new SystemMessageConfig
                    {
                        Mode = SystemMessageMode.Append,
                        Content = agentDefinition.SystemPrompt ?? string.Empty,
                    },
                    OnPermissionRequest = static (_, _) =>
                        Task.FromResult(ApproveCopilotPermission()),
                    Tools = [.. copilotTools],
                    WorkingDirectory = agentDefinition.ShellConfiguration.IsActive
                        ? agentDefinition.ShellConfiguration.WorkingDirectory
                        : null,
                    EnableSkills = skillDirectories.Length > 0,
                    SkillDirectories = skillDirectories,
                    CustomAgents = customAgent is null ? null : [customAgent],
                    Agent = customAgent?.Name,
                };

                AIAgent builtAgent = copilotClient
                    .AsAIAgent(
                        sessionConfig,
                        ownsClient: false,
                        id: $"sentinel-{agentDefinition.Id}",
                        name: agentDefinition.Name,
                        description: agentDefinition.Description)
                    .AsBuilder()
                    .Build();
                return Result.Success<AgentConstruction, IApplicationError>(
                    new AgentConstruction(builtAgent, shellExecutor, copilotClient));
            }
            catch (NotSupportedException exception)
            {
                await DisposeResourcesAsync(shellExecutor, copilotClient).ConfigureAwait(false);
                return Result.Failure<AgentConstruction, IApplicationError>(
                    new InvalidOperationError([exception.Message]));
            }
            catch (Exception exception)
            {
                await DisposeResourcesAsync(shellExecutor, copilotClient).ConfigureAwait(false);
                return Result.Failure<AgentConstruction, IApplicationError>(
                    new ExceptionError(
                        ["Could not start GitHub Copilot. Check that the Copilot runtime is installed and signed in."],
                        exception));
            }

            static AIFunctionDeclaration ToCopilotTool(AITool tool)
                => tool as AIFunctionDeclaration
                    ?? throw new NotSupportedException(
                        $"Tool '{tool.GetType().Name}' cannot be registered with the GitHub Copilot runtime.");
        }

        private static async ValueTask DisposeResourcesAsync(ShellExecutor? shellExecutor, IAsyncDisposable? ownedResource)
        {
            try
            {
                if (ownedResource is not null)
                {
                    await ownedResource.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (shellExecutor is not null)
                {
                    await shellExecutor.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static AgentConstruction From(
            ChatClient chatClient, AgentDefinition agentDefinition, IEnumerable<AITool> mcpTools)
        {
            ShellExecutor? shellExecutor = null;
            try
            {
                if (agentDefinition.ShellConfiguration.IsActive)
                {
                    shellExecutor = ShellExecutorExtensions.From(agentDefinition.ShellConfiguration);
                    mcpTools = mcpTools.Append(shellExecutor.AsAIFunction());
                }

                AIAgent builtAgent = chatClient
                    .AsIChatClient()
                    .AsAIAgent(
                        new ChatClientAgentOptions
                        {
                            Name = agentDefinition.Name,
                            Description = agentDefinition.Description,
                            ChatOptions = new ChatOptions
                            {
                                Instructions = agentDefinition.SystemPrompt ?? string.Empty,
                                Tools = [.. mcpTools],
                                Reasoning = new() { Effort = ReasoningEffort.None }
                            },
                            ChatHistoryProvider = new InMemoryChatHistoryProvider(),
                            AIContextProviders = AIContextProvider.From(agentDefinition),
                        }
                    )
                    .AsBuilder()
                    // .UseToolApproval(
                    //     new ToolApprovalAgentOptions
                    //     {
                    //         AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
                    //     }
                    // )
                    .Build();

                return new AgentConstruction(builtAgent, shellExecutor, null);
            }
            catch
            {
                if (shellExecutor is not null)
                {
                    shellExecutor.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                throw;
            }
        }

        private static AgentConstruction From(
            ChatClient chatClient, HarnessAgentDefinition agentDefinition, IEnumerable<AITool> mcpTools)
        {
            ShellExecutor? shellExecutor = null;
            try
            {
                if (agentDefinition.ShellConfiguration.IsActive)
                {
                    shellExecutor = ShellExecutorExtensions.From(agentDefinition.ShellConfiguration);
                    mcpTools = mcpTools.Append(shellExecutor.AsAIFunction());
                }

                AIAgent builtAgent = chatClient
                    .AsIChatClient()
#pragma warning disable MAAI001
                    .AsHarnessAgent(
                        new HarnessAgentOptions
                        {
                            Name = agentDefinition.Name,
                            Description = agentDefinition.Description,
                            ChatOptions = new ChatOptions
                            {
                                Instructions = agentDefinition.SystemPrompt ?? string.Empty,
                                Tools = [.. mcpTools],
                                Reasoning = new() { Effort = ReasoningEffort.None }
                            },
                            AIContextProviders = AIContextProvider.From(agentDefinition),
                            // ToolApprovalAgentOptions = new ToolApprovalAgentOptions
                            // {
                            //     AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
                            // },
                            DisableTodoProvider = true,
                            DisableAgentModeProvider = true,
                            DisableFileMemory = false,
                            DisableWebSearch = true,
                            // DisableTodoProvider = agentDefinition.DisableTodoProvider,
                            // DisableAgentModeProvider = agentDefinition.DisableAgentModeProvider,
                            // DisableFileMemory = agentDefinition.DisableFileMemory,
                            // DisableWebSearch = agentDefinition.DisableWebSearch,
                            DisableToolAutoApproval = agentDefinition.DisableToolAutoApproval,
                            DisableAgentSkillsProvider = agentDefinition.DisableAgentSkillsProvider,
                            DisableOpenTelemetry = agentDefinition.DisableOpenTelemetry,
                            MaxContextWindowTokens = agentDefinition.MaxContextWindowTokens,
                            MaxOutputTokens = agentDefinition.MaxOutputTokens,
                            FileAccessStore = string.IsNullOrWhiteSpace(agentDefinition.FileAccessWorkingDirectory)
                                ? null
                                : new FileSystemAgentFileStore(agentDefinition.FileAccessWorkingDirectory),
                        })
#pragma warning restore MAAI001
                    .AsBuilder()
                    .Build();

                return new AgentConstruction(builtAgent, shellExecutor, null);
            }
            catch
            {
                if (shellExecutor is not null)
                {
                    shellExecutor.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                throw;
            }
        }
    }
}
