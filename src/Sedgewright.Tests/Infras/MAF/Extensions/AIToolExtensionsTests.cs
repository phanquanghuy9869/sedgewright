using System;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Sedgewright.Tests.Configurations;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.MAF.Extensions;

[TestFixture]
public sealed class AIToolExtensionsTests
{
    [Test]
    public async Task AIFunctionFromToolDefinition_UsesWorkflowMetadata()
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", "workflow-description");
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<AIFunction, IApplicationError> result = await AIFunction.FromToolDefinition(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value.Name, Is.EqualTo(orchestration.Name));
            Assert.That(result.Value.Description, Is.EqualTo(orchestration.Description));
        }
    }

    [Test]
    public async Task AIFunctionFromToolDefinition_UsesToolDescriptionWhenProvided()
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", "workflow-description");
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            ToolDescription = "tool-description",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<AIFunction, IApplicationError> result = await AIFunction.FromToolDefinition(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Description, Is.EqualTo(toolDefinition.ToolDescription));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task AIFunctionFromToolDefinition_FallsBackForBlankToolDescription(string toolDescription)
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", "workflow-description");
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            ToolDescription = toolDescription,
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<AIFunction, IApplicationError> result = await AIFunction.FromToolDefinition(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Description, Is.EqualTo(orchestration.Description));
    }

    [Test]
    public void AIFunctionFactoryCreateWithSnakeCase_UsesSnakeCaseJsonSchema()
    {
        AIFunction function = AIFunctionFactory.CreateWithSnakeCase(Echo, "echo", "Echoes a search query.");
        JsonElement properties = function.JsonSchema
            .GetProperty("properties")
            .GetProperty("arguments")
            .GetProperty("properties");
        bool hasSnakeCaseProperty = properties.TryGetProperty("search_query", out _);
        bool hasCamelCaseProperty = properties.TryGetProperty("searchQuery", out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hasSnakeCaseProperty, Is.True);
            Assert.That(hasCamelCaseProperty, Is.False);
        }
    }

    [Test]
    public async Task WorkflowFromToolDefinition_PopulatesRuntimeMetadataFromOrchestration()
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", "workflow-description");
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<WorkflowRuntime, IApplicationError> result = await Workflow.From(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        await using WorkflowRuntime runtime = result.Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(runtime.Name, Is.EqualTo(orchestration.Name));
            Assert.That(runtime.Description, Is.EqualTo(orchestration.Description));
            Assert.That(runtime.Kind, Is.EqualTo(OrchestrationKind.Sequential));
            Assert.That(runtime.ToolDescription, Is.Null);
        }
    }

    [Test]
    public async Task WorkflowFromToolDefinition_PopulatesToolDescription()
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", "workflow-description");
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            ToolDescription = "tool-description",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<WorkflowRuntime, IApplicationError> result = await Workflow.From(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        await using WorkflowRuntime runtime = result.Value;
        Assert.That(runtime.ToolDescription, Is.EqualTo(toolDefinition.ToolDescription));
    }

    [Test]
    public async Task WorkflowFromToolDefinition_PreservesNullOrchestrationDescription()
    {
        SequentialOrchestration orchestration = CreateSequentialOrchestration("workflow-name", null);
        ToolDefinition toolDefinition = new()
        {
            Name = "workflow-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestration = orchestration,
        };

        Result<WorkflowRuntime, IApplicationError> result = await Workflow.From(toolDefinition);

        Assert.That(result.IsSuccess, Is.True);
        await using WorkflowRuntime runtime = result.Value;
        Assert.That(runtime.Description, Is.Null);
    }

    [Test]
    public async Task FromAgentDefinition_WithMultipleWorkflowTools_ReturnsAllTools()
    {
        SequentialOrchestration firstWorkflow = new() { Name = "first-workflow" };
        SequentialOrchestration secondWorkflow = new() { Name = "second-workflow" };
        ToolDefinition firstTool = new()
        {
            Id = Guid.NewGuid(),
            Name = "first-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestrationId = firstWorkflow.Id,
            SequentialOrchestration = firstWorkflow,
        };
        ToolDefinition secondTool = new()
        {
            Id = Guid.NewGuid(),
            Name = "second-tool",
            Type = ToolDefinitionType.Workflow,
            SequentialOrchestrationId = secondWorkflow.Id,
            SequentialOrchestration = secondWorkflow,
        };
        AgentDefinition agentDefinition = new AgentDefinitionBuilder()
            .With(
                name: "multi-tool-agent",
                description: "Uses multiple workflow tools.",
                systemPrompt: "You use the available tools.",
                model: "gpt-5")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            })
            .Build();
        agentDefinition.Tools =
        [
            new AgentTool
            {
                ToolDefinitionId = firstTool.Id,
                ToolDefinition = firstTool,
                AgentDefinition = agentDefinition,
            },
            new AgentTool
            {
                ToolDefinitionId = secondTool.Id,
                ToolDefinition = secondTool,
                AgentDefinition = agentDefinition,
            },
        ];

        Result<IEnumerable<AITool>, IApplicationError> result = await AITool.FromAgentDefinition(agentDefinition);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task FromAgentDefinition_WithNativeTool_ReturnsFailure()
    {
        ToolDefinition nativeTool = new()
        {
            Id = Guid.NewGuid(),
            Name = "native-tool",
            Type = ToolDefinitionType.Native,
        };
        AgentDefinition agentDefinition = new AgentDefinitionBuilder()
            .With(
                name: "native-tool-agent",
                description: "Uses a native tool.",
                systemPrompt: "You use the available tools.",
                model: "gpt-5")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            })
            .Build();
        agentDefinition.Tools =
        [new AgentTool
        {
            ToolDefinitionId = nativeTool.Id,
            ToolDefinition = nativeTool,
            AgentDefinition = agentDefinition,
        }];

        Result<IEnumerable<AITool>, IApplicationError> result = await AITool.FromAgentDefinition(agentDefinition);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Messages.Single(), Does.Contain("unsupported type 'Native'"));
    }

    private static SequentialOrchestration CreateSequentialOrchestration(string name, string? description)
    {
        AgentDefinition agentDefinition = new AgentDefinitionBuilder()
            .With(
                name: "workflow-agent",
                description: "Workflow agent description.",
                systemPrompt: "You are a workflow agent.",
                model: "gpt-5")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = "test-key",
            })
            .Build();

        return new SequentialOrchestration
        {
            Name = name,
            Description = description,
            Steps =
            [
                new SequentialOrchestrationAgent
                {
                    StepOrder = 1,
                    AgentDefinition = agentDefinition,
                },
            ],
        };
    }

    private static string Echo(EchoArguments arguments) => arguments.SearchQuery;

    private sealed class EchoArguments
    {
        public string SearchQuery { get; set; } = string.Empty;
    }
}
