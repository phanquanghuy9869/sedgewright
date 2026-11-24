using System.Collections.Generic;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.MAF.Extensions;

[TestFixture]
public sealed class WorkflowExtensionsTests
{
    [Test]
    public void ResolveMagenticExecutorName_UsesConfiguredNameForKnownExecutor()
    {
        Dictionary<string, string> executorDisplayNames = new(StringComparer.Ordinal)
        {
            ["research-executor"] = "Research Agent",
            ["writing-executor"] = "Writing Agent",
        };

        string researchName = WorkflowExtensions.ResolveMagenticExecutorName(executorDisplayNames, "research-executor");
        string writingName = WorkflowExtensions.ResolveMagenticExecutorName(executorDisplayNames, "writing-executor");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(researchName, Is.EqualTo("Research Agent"));
            Assert.That(writingName, Is.EqualTo("Writing Agent"));
            Assert.That(researchName, Is.Not.EqualTo(writingName));
        }
    }

    [Test]
    public void ResolveMagenticExecutorName_UsesExecutorIdWhenNoConfiguredNameExists()
    {
        string resolvedName = WorkflowExtensions.ResolveMagenticExecutorName(
            new Dictionary<string, string>(),
            "unknown-executor");

        Assert.That(resolvedName, Is.EqualTo("unknown-executor"));
    }

    [Test]
    public void ResolveMagenticExecutorName_UsesUnknownWhenNoIdentityIsAvailable()
    {
        string resolvedName = WorkflowExtensions.ResolveMagenticExecutorName(
            null,
            null);

        Assert.That(resolvedName, Is.EqualTo("Unknown"));
    }

    [Test]
    public void ProcessFinalOutput_PreservesMessageAuthorAndUsesFallbackWhenMissing()
    {
        ChatMessage authoredMessage = new(ChatRole.Assistant, "Authored")
        {
            AuthorName = "Explicit Author",
        };
        ChatMessage unlabeledMessage = new(ChatRole.Assistant, "Unlabeled");
        List<ChatMessage> output = [authoredMessage, unlabeledMessage];
        WorkflowOutputEvent outputEvent = new(output, "writer-executor");

        List<MessageModel> messages = outputEvent.ProcessFinalOutput("Resolved Writer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages[0].AuthorLabel, Is.EqualTo("Explicit Author"));
            Assert.That(messages[1].AuthorLabel, Is.EqualTo("Resolved Writer"));
        }
    }
}
