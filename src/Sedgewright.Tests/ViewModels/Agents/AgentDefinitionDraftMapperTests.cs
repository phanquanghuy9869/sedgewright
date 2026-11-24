using System;
using NUnit.Framework;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentDefinitionDraftMapperTests
{
    [Test]
    public void CreateDraft_NewDefinition_UsesConfiguredWorkingDirectoryForBothCapabilities()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(
            null,
            "d:\\workspace\\sentinel");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.ShellWorkingDirectory, Is.EqualTo("d:\\workspace\\sentinel"));
            Assert.That(draft.IsShellActive, Is.True);
            Assert.That(draft.EnableFileAccess, Is.True);
        }
    }

    [Test]
    public void CreateDraft_NewDefinition_BlankWorkingDirectoryFallsBackToCurrentDirectory()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null, " ");

        Assert.That(draft.ShellWorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));
    }

    [Test]
    public void CreateDraft_ExistingHarness_PreservesShellAndFileAccessState()
    {
        HarnessAgentDefinition definition = new()
        {
            Name = "Harness",
            Description = "Harness agent",
            Model = "model",
            ShellConfiguration = new(
                ShellExecutorType.Docker,
                "d:\\workspace\\shell",
                IsActive: false),
            FileAccessWorkingDirectory = "d:\\workspace\\file-access",
        };

        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(definition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.ShellWorkingDirectory, Is.EqualTo("d:\\workspace\\shell"));
            Assert.That(draft.IsShellActive, Is.False);
            Assert.That(draft.EnableFileAccess, Is.True);
        }
    }

    [Test]
    public void CreateCandidateDefinition_HarnessWithFileAccess_UsesSharedWorkingDirectory()
    {
        LLMProvider provider = new()
        {
            Id = 1,
            DisplayName = "Provider",
            ApiKey = "key",
        };
        AgentDefinition sourceDefinition = new()
        {
            Name = "Source",
            Description = "Source agent",
            Model = "model",
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper
            .CreateDraft(null, "d:\\workspace\\shared") with
        {
            Name = "Harness",
            Description = "Harness agent",
            Model = "model",
            SelectedProvider = provider,
            IsHarnessAgent = true,
            IsShellActive = false,
            EnableFileAccess = true,
        };

        HarnessAgentDefinition candidate = (HarnessAgentDefinition)new AgentDefinitionDraftMapper()
            .CreateCandidateDefinition(draft, sourceDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidate.ShellConfiguration.WorkingDirectory, Is.EqualTo("d:\\workspace\\shared"));
            Assert.That(candidate.ShellConfiguration.IsActive, Is.False);
            Assert.That(candidate.FileAccessWorkingDirectory, Is.EqualTo("d:\\workspace\\shared"));
        }
    }

    [Test]
    public void CreateCandidateDefinition_HarnessWithoutFileAccess_ClearsFileAccessDirectory()
    {
        LLMProvider provider = new()
        {
            Id = 1,
            DisplayName = "Provider",
            ApiKey = "key",
        };
        AgentDefinition sourceDefinition = new()
        {
            Name = "Source",
            Description = "Source agent",
            Model = "model",
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper
            .CreateDraft(null, "d:\\workspace\\shared") with
        {
            Name = "Harness",
            Description = "Harness agent",
            Model = "model",
            SelectedProvider = provider,
            IsHarnessAgent = true,
            EnableFileAccess = false,
        };

        HarnessAgentDefinition candidate = (HarnessAgentDefinition)new AgentDefinitionDraftMapper()
            .CreateCandidateDefinition(draft, sourceDefinition);

        Assert.That(candidate.FileAccessWorkingDirectory, Is.Null);
    }

    [Test]
    public void CreateCandidateDefinition_PreservesMultipleToolDefinitions()
    {
        LLMProvider provider = new()
        {
            Id = 1,
            DisplayName = "Provider",
            ApiKey = "key",
        };
        ToolDefinition firstTool = new()
        {
            Id = Guid.NewGuid(),
            Name = "first-tool",
            Type = ToolDefinitionType.Native,
        };
        ToolDefinition secondTool = new()
        {
            Id = Guid.NewGuid(),
            Name = "second-tool",
            Type = ToolDefinitionType.Native,
        };
        AgentDefinition sourceDefinition = new()
        {
            Id = 12,
            Name = "Source",
            Description = "Source agent",
            Model = "model",
            Tools =
            [
                new AgentTool
                {
                    Id = 1,
                    AgentId = 12,
                    ToolDefinitionId = firstTool.Id,
                    ToolDefinition = firstTool,
                },
                new AgentTool
                {
                    Id = 2,
                    AgentId = 12,
                    ToolDefinitionId = secondTool.Id,
                    ToolDefinition = secondTool,
                },
            ],
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null) with
        {
            Name = "Candidate",
            Description = "Candidate agent",
            Model = "model",
            SelectedProvider = provider,
            SelectedToolDefinitionIds = [firstTool.Id, secondTool.Id],
        };

        AgentDefinition candidate = new AgentDefinitionDraftMapper()
            .CreateCandidateDefinition(draft, sourceDefinition);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidate.Tools, Has.Count.EqualTo(2));
            Assert.That(candidate.Tools.Select(tool => tool.ToolDefinitionId), Is.EquivalentTo([firstTool.Id, secondTool.Id]));
            Assert.That(candidate.Tools, Has.All.Property(nameof(AgentTool.AgentDefinition)).SameAs(candidate));
        }
    }

    [Test]
    public void CreateDraft_ExistingDefinition_LoadsDistinctToolDefinitionIds()
    {
        Guid toolId = Guid.NewGuid();
        AgentDefinition definition = new()
        {
            Name = "Agent",
            Description = "Agent description",
            Model = "model",
            Tools =
            [
                new AgentTool
                {
                    ToolDefinitionId = toolId,
                    ToolDefinition = new ToolDefinition
                    {
                        Id = toolId,
                        Name = "tool",
                        Type = ToolDefinitionType.Native,
                    },
                },
                new AgentTool
                {
                    ToolDefinitionId = toolId,
                    ToolDefinition = new ToolDefinition
                    {
                        Id = toolId,
                        Name = "tool duplicate",
                        Type = ToolDefinitionType.Native,
                    },
                },
            ],
        };

        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(definition);

        Assert.That(draft.SelectedToolDefinitionIds, Is.EqualTo([toolId]));
    }

    [Test]
    public void ApplyDraft_SynchronizesToolAttachments()
    {
        LLMProvider provider = new()
        {
            Id = 1,
            DisplayName = "Provider",
            ApiKey = "key",
        };
        Guid retainedToolId = Guid.NewGuid();
        Guid addedToolId = Guid.NewGuid();
        AgentTool retainedTool = new()
        {
            Id = 4,
            AgentId = 12,
            ToolDefinitionId = retainedToolId,
            ToolDefinition = new ToolDefinition
            {
                Id = retainedToolId,
                Name = "retained",
                Type = ToolDefinitionType.Native,
            },
        };
        AgentDefinition definition = new()
        {
            Id = 12,
            Name = "Agent",
            Description = "Agent description",
            Model = "model",
            Provider = provider,
            Tools = [retainedTool],
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(definition) with
        {
            SelectedToolDefinitionIds = [retainedToolId, addedToolId],
        };

        new AgentDefinitionDraftMapper().ApplyDraft(definition, draft);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition.Tools, Has.Count.EqualTo(2));
            Assert.That(definition.Tools.Select(tool => tool.ToolDefinitionId),
                Is.EquivalentTo([retainedToolId, addedToolId]));
            Assert.That(definition.Tools.Single(tool => tool.ToolDefinitionId == retainedToolId),
                Is.SameAs(retainedTool));
            Assert.That(definition.Tools.Single(tool => tool.ToolDefinitionId == addedToolId).AgentDefinition,
                Is.SameAs(definition));
        }

        AgentDefinitionEditorDraft removalDraft = draft with
        {
            SelectedToolDefinitionIds = [addedToolId],
        };

        new AgentDefinitionDraftMapper().ApplyDraft(definition, removalDraft);

        Assert.That(definition.Tools.Select(tool => tool.ToolDefinitionId), Is.EqualTo([addedToolId]));
    }
}
