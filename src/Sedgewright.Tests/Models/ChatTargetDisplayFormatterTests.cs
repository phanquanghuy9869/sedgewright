using Sedgewright.Entities;
using Sedgewright.ViewModels.Chat;

namespace Sedgewright.Tests.Models;

[TestFixture]
public sealed class ChatTargetDisplayInfoExtensionsTests
{
    [Test]
    public void From_ForAgent_UsesAgentNameAndDetails()
    {
        LLMProvider provider = new()
        {
            DisplayName = "OpenAI",
            BaseUrl = "https://example.invalid",
            ApiKey = "test-key",
        };

        AgentDefinition agent = new()
        {
            Id = 11,
            Name = "Planner",
            Description = "Creates a plan before execution.",
            Model = "gpt-5",
            Provider = provider,
        };

        ChatTargetDisplayInfo displayInfo = ChatTargetDisplayInfo.From(agent);

        Assert.That(displayInfo.Label, Is.EqualTo("Planner"));
        Assert.That(displayInfo.Subtitle, Does.Contain("gpt-5"));
        Assert.That(displayInfo.Subtitle, Does.Contain("OpenAI"));
    }

    [Test]
    public void From_ForOrchestration_UsesKindLabelNameAndDescription()
    {
        SequentialOrchestration orchestration = new()
        {
            Id = 7,
            Name = "Draft",
            Description = "Writes a first draft.",
        };

        ChatTargetDisplayInfo displayInfo = ChatTargetDisplayInfo.From(orchestration);

        Assert.That(displayInfo.Label, Is.EqualTo("Sequential · Draft"));
        Assert.That(displayInfo.Subtitle, Is.EqualTo("Writes a first draft."));
    }
}
