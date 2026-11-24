using CSharpFunctionalExtensions;
using Microsoft.Extensions.AI;
using Sedgewright.Tests.Configurations;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Services;

[TestFixture]
public sealed class GroupChatOrchestrationServiceTests
{
    private static GroupChatOrchestration CreateGroupChatOrchestration()
    {
        AgentDefinition firstAgentDefinition = CreateAgentDefinition(
            name: "Group Chat Test Agent One",
            description: "A minimal agent used to verify the group chat orchestration execution flow.",
            systemPrompt: "You are agent one. Contribute a short, thoughtful first response and mention agent one.");
        firstAgentDefinition.Id = 1;

        AgentDefinition secondAgentDefinition = CreateAgentDefinition(
            name: "Group Chat Test Agent Two",
            description: "A second minimal agent used to verify the group chat orchestration execution flow.",
            systemPrompt: "You are agent two. Contribute a short follow-up response and mention agent two.");
        secondAgentDefinition.Id = 2;

        GroupChatOrchestration orchestration = new()
        {
            Name = "Group Chat Orchestration Test",
            Description = "Manual integration test for the group chat orchestration execution pipeline.",
            MaximumIterationCount = 4,
            Participants =
            [
                new GroupChatOrchestrationParticipant
                {
                    AgentDefinitionId = firstAgentDefinition.Id,
                    StepOrder = 1,
                    AgentDefinition = firstAgentDefinition,
                },
                new GroupChatOrchestrationParticipant
                {
                    AgentDefinitionId = secondAgentDefinition.Id,
                    StepOrder = 2,
                    AgentDefinition = secondAgentDefinition,
                },
            ],
        };

        return orchestration;
    }

    private static AgentDefinition CreateAgentDefinition(string name, string description, string systemPrompt)
    {
        return new AgentDefinitionBuilder()
            .With(
                name: name,
                description: description,
                systemPrompt: systemPrompt,
                model: "gpt-5.4-mini")
            .WithProvider(new LLMProvider
            {
                Id = 1,
                ProviderKind = LlmProviderKind.OpenAI,
                DisplayName = "OpenAI",
                ApiKey = Configs.OpenAIKey,
            })
            .Build();
    }
}
