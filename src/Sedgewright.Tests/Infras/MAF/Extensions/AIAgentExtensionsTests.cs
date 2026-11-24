using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Tests.Configurations;
using Sedgewright.Models;
using CSharpFunctionalExtensions;

namespace Sedgewright.Tests.Infras.MAF.Extensions;

[TestFixture]
public sealed class AIAgentExtensionsTests
{
    [Test]
    public void ToActivityUpdate_ForFunctionCallHidesArguments()
    {
        FunctionCallContent content = new(
            "call-123",
            "search_files",
            new Dictionary<string, object?>
            {
                ["query"] = "Agent Framework",
            });

        IntermediateUpdate update = content.ToActivityUpdate("Research Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Kind, Is.EqualTo(IntermediateUpdateKind.ToolCall));
            Assert.That(update.Update, Does.Contain("search_files"));
            Assert.That(update.CallId, Is.EqualTo("call-123"));
            Assert.That(update.Details, Does.Not.Contain("Agent Framework"));
        }
    }

    [Test]
    public void ToActivityUpdate_ForProviderReasoningOmitsReasoningText()
    {
        TextReasoningContent content = new("The provider supplied this explanation.");

        Assert.That(content.ToActivityUpdate("Research Agent"), Is.Null);
    }

    [Test]
    public void ToActivityUpdate_ForRawRepresentationOnlyEnvelope_ReturnsNull()
    {
        AIContent content = new() { RawRepresentation = new object() };

        Assert.That(content.ToActivityUpdate("Copilot Agent"), Is.Null);
    }

    [Test]
    public void ToActivityUpdate_ForCopilotEvent_ShowsEventTypeAndMessageId()
    {
        AssistantMessageEvent copilotEvent = new()
        {
            Data = new AssistantMessageData { Content = string.Empty, MessageId = "message-123" },
        };
        AIContent content = new() { RawRepresentation = copilotEvent };

        IntermediateUpdate update = content.ToActivityUpdate("Copilot Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Kind, Is.EqualTo(IntermediateUpdateKind.OtherContent));
            Assert.That(update.Update, Is.EqualTo("Copilot assistant response completed."));
            Assert.That(update.Details, Does.Contain("Event type: assistant.message"));
            Assert.That(update.Details, Does.Contain("Message ID: message-123"));
        }
    }

    [Test]
    public void ToActivityUpdate_ForCopilotInfoEvent_ShowsMessageCategoryAndTip()
    {
        SessionInfoEvent copilotEvent = new()
        {
            Data = new SessionInfoData
            {
                InfoType = "context_window",
                Message = "Conversation context was compacted.",
                Tip = "Start a new chat for a clean context.",
            },
        };
        AIContent content = new() { RawRepresentation = copilotEvent };

        IntermediateUpdate update = content.ToActivityUpdate("Copilot Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Update, Is.EqualTo("Conversation context was compacted."));
            Assert.That(update.Details, Does.Contain("Event type: session.info"));
            Assert.That(update.Details, Does.Contain("Category: context_window"));
            Assert.That(update.Details, Does.Contain("Tip: Start a new chat for a clean context."));
        }
    }

    [Test]
    public void ToActivityUpdate_ForCopilotWarningEvent_ShowsWarningCategoryAndMessage()
    {
        SessionWarningEvent copilotEvent = new()
        {
            Data = new SessionWarningData
            {
                WarningType = "mcp",
                Message = "An MCP server could not be reached.",
            },
        };
        AIContent content = new() { RawRepresentation = copilotEvent };

        IntermediateUpdate update = content.ToActivityUpdate("Copilot Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Kind, Is.EqualTo(IntermediateUpdateKind.Warning));
            Assert.That(update.Update, Is.EqualTo("An MCP server could not be reached."));
            Assert.That(update.Details, Does.Contain("Warning type: mcp"));
        }
    }

    [Test]
    public void ToActivityUpdate_ForCopilotModelChange_ShowsPreviousNewModelAndCause()
    {
        SessionModelChangeEvent copilotEvent = new()
        {
            Data = new SessionModelChangeData
            {
                PreviousModel = "model-a",
                NewModel = "model-b",
                Cause = "rate_limit_auto_switch",
            },
        };
        AIContent content = new() { RawRepresentation = copilotEvent };

        IntermediateUpdate update = content.ToActivityUpdate("Copilot Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Update, Is.EqualTo("Copilot model changed from model-a to model-b."));
            Assert.That(update.Details, Does.Contain("Cause: rate_limit_auto_switch"));
        }
    }

    [Test]
    public void ToActivityUpdate_ForUntypedContentWithoutRawRepresentationKeepsFallback()
    {
        AIContent content = new();

        IntermediateUpdate update = content.ToActivityUpdate("Research Agent")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.Kind, Is.EqualTo(IntermediateUpdateKind.OtherContent));
            Assert.That(update.Update, Is.EqualTo("Additional agent content received (AIContent)."));
        }
    }

#pragma warning disable GHCP001
    [Test]
    public void ApproveCopilotPermission_ReturnsOneShotApproval()
    {
        PermissionDecision decision = Sedgewright.Infras.MAF.Extensions.AIAgentExtensions.ApproveCopilotPermission();

        Assert.That(decision, Is.TypeOf<PermissionDecisionApproveOnce>());
    }
#pragma warning restore GHCP001

    [Test]
    public void ChatClientFactory_ForCopilotExplainsNativeAgentRoute()
    {
        LLMProvider provider = new()
        {
            ProviderKind = LlmProviderKind.Copilot,
            DisplayName = "GitHub Copilot",
            BaseUrl = string.Empty,
            ApiKey = string.Empty,
            EnabledModelsJson = "[]",
        };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => ChatClient.FromProvider(provider, "copilot-model"))!;

        Assert.That(exception.Message, Does.Contain("native Copilot agent runtime"));
    }

    [Test]
    public async Task From_WithValidAgentDefinition_CreatesAgentWithExpectedMetadata()
    {
        AgentDefinition agentDefinition = new AgentDefinitionBuilder()
            .With(
                name: "Search Agent",
                description: "Searches the web for current information.",
                systemPrompt: "You are a helpful assistant for searching the web.",
                model: "gpt-5-search-api")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            })
            .Build();

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Agent, Is.InstanceOf<ChatClientAgent>());
        }

        await using AgentRuntime runtime = result.Value;
        ChatClientAgent agent = (ChatClientAgent)runtime.Agent;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(agent.Description, Is.EqualTo(agentDefinition.Description));
            Assert.That(agent.Instructions, Is.EqualTo(agentDefinition.SystemPrompt));
            Assert.That(agent.ChatClient, Is.Not.Null);
        }
    }

    [Test]
    public async Task AgentRuntime_DisposesOwnedResource()
    {
        AgentDefinition agentDefinition = new AgentDefinitionBuilder()
            .With(
                name: "Resource Agent",
                description: "Exercises runtime resource ownership.",
                systemPrompt: "You are a helpful assistant.",
                model: "gpt-5-search-api")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            })
            .Build();
        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);
        Assert.That(result.IsSuccess, Is.True);

        await using AgentRuntime sourceRuntime = result.Value;
        AgentSession session = await sourceRuntime.Agent.CreateSessionAsync();
        AsyncDisposableProbe ownedResource = new();
        AgentRuntime runtime = new(sourceRuntime.Agent, session, null, ownedResource);

        await runtime.DisposeAsync();

        Assert.That(ownedResource.IsDisposed, Is.True);
    }

    [Test]
    public async Task From_WithValidHarnessAgentDefinition_CreatesHarnessAgent()
    {
        HarnessAgentDefinition agentDefinition = new HarnessAgentDefinition
        {
            Name = "Harness Search Agent",
            Description = "Searches the web with the Harness pipeline.",
            SystemPrompt = "You are a helpful assistant for searching the web.",
            Model = "gpt-5-search-api",
            Provider = new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            },
            LLMProviderId = 1,
        };

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Agent, Is.InstanceOf<HarnessAgent>());
        }
    }

    [Test]
    public async Task From_WithCopilotHarnessAgentDefinition_ReturnsUnsupportedError()
    {
        HarnessAgentDefinition agentDefinition = new HarnessAgentDefinition
        {
            Name = "Copilot Harness Agent",
            Description = "Uses an unsupported provider with the Harness pipeline.",
            SystemPrompt = "You are a helpful assistant.",
            Model = "copilot-model",
            Provider = new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.Copilot,
                DisplayName = "GitHub Copilot",
            },
            LLMProviderId = 1,
        };

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Messages, Does.Contain("GitHub Copilot does not support Harness agents."));
        }
    }

    [Test]
    public async Task From_WithHarnessAgentDefinitionWithSkills_CreatesHarnessAgent()
    {
        HarnessAgentDefinition agentDefinition = new HarnessAgentDefinition
        {
            Name = "Harness Skills Agent",
            Description = "Uses selected skills with the Harness pipeline.",
            SystemPrompt = "You are a helpful assistant.",
            Model = "gpt-5-search-api",
            Provider = new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            },
            LLMProviderId = 1,
            Skills =
            [
                new AgentSkillDefinition
                {
                    SkillDirectory = ".agents/skills",
                    SelectedSkills = ["testing"],
                },
            ],
        };

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Agent, Is.InstanceOf<HarnessAgent>());
        }
    }

    [Test]
    public async Task From_WithHarnessAgentDefinitionTypedAsBaseAgentDefinition_CreatesHarnessAgent()
    {
        AgentDefinition agentDefinition = new HarnessAgentDefinition
        {
            Name = "Harness Search Agent",
            Description = "Searches the web with the Harness pipeline.",
            SystemPrompt = "You are a helpful assistant for searching the web.",
            Model = "gpt-5-search-api",
            Provider = new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            },
            LLMProviderId = 1,
        };

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Agent, Is.InstanceOf<HarnessAgent>());
        }
    }

    [Test, Explicit("Requires valid OpenAI API key and network access to run."), NonParallelizable]
    public async Task From_WithHarnessAgentDefinition_CanRunSimplePromptInHarnessPipeline()
    {
        HarnessAgentDefinition agentDefinition = CreateHarnessAgentDefinition();

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);
        Assert.That(result.IsSuccess, Is.True);

        await using AgentRuntime runtime = result.Value;
        AgentResponse response = await runtime.Agent.RunAsync("Reply with the word harness.");

        Assert.That(response.Messages, Is.Not.Empty);
    }

    [Test, Explicit("Requires valid OpenAI API key, Playwright MCP, and network access to run."), NonParallelizable]
    public async Task From_WithPlaywrightMcpAgentDefinition_CanOpenGoogleVietnamInHeadedChrome()
    {
        AgentDefinition agentDefinition = CreatePlaywrightAgentDefinition();

        Result<AgentRuntime, IApplicationError> result = await AIAgent.From(agentDefinition);
        Assert.That(result.IsSuccess, Is.True);

        await using AgentRuntime runtime = result.Value;
        AgentResponse response = await runtime.Agent.RunAsync("Use the Playwright MCP browser to open https://google.com.vn in headed Chrome. Reply with the page title after the page loads.");

        Assert.That(response.Messages, Is.Not.Empty);
    }

    private static AgentDefinition CreatePlaywrightAgentDefinition()
    {
        string? chromeUserDataDir = GetChromeUserDataDir();
        List<string> arguments = ["@playwright/mcp@latest", "--browser", "chrome"];

        // if (!string.IsNullOrWhiteSpace(chromeUserDataDir))
        // {
        //     arguments.AddRange(["--user-data-dir", chromeUserDataDir]);
        // }

        return new AgentDefinitionBuilder()
            .With(
                name: "Playwright Browser Agent",
                description: "Browses websites with Playwright MCP in headed Chrome mode.",
                systemPrompt: "You are a web browser agent using Playwright MCP. Always use headed mode, connect to real Chrome browser or use existing profile if possible, and navigate to the requested page.",
                model: "gpt-5.4-mini")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = Configs.OpenAIKey,
            })
            .AddSkill(
                skillDirectory: ".agents/skills",
                selectedSkills: ["playwright"])
            .AddMcpStdio(
                name: "playwright",
                description: "Playwright browser automation server.",
                command: "npx",
                arguments: arguments)
            .Build();
    }

    private sealed class AsyncDisposableProbe : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static HarnessAgentDefinition CreateHarnessAgentDefinition()
    {
        return new HarnessAgentDefinition
        {
            Name = "Harness Browser Agent",
            Description = "Runs through the Harness pipeline and responds to a simple prompt.",
            SystemPrompt = "You are a concise assistant. Reply directly and do not use tools unless necessary.",
            Model = "gpt-5.4-mini",
            Provider = new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = Configs.OpenAIKey,
            },
            LLMProviderId = 1,
            DisableTodoProvider = true,
            DisableAgentModeProvider = true,
            DisableFileMemory = true,
            DisableWebSearch = true,
            DisableToolAutoApproval = true,
            DisableAgentSkillsProvider = true,
            DisableOpenTelemetry = true,
        };
    }

    private static string? GetChromeUserDataDir()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string chromeUserDataDir = Path.Combine(localApplicationData, "Google", "Chrome", "User Data");

        return Directory.Exists(chromeUserDataDir) ? chromeUserDataDir : null;
    }
}
