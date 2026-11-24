using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sedgewright.Models;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentDefinitionSkillsViewModelTests
{
    [Test]
    public void AddSkillCommand_OpensUnifiedPickerWithWorkingAndCommonSources()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "sentinel-working-skills");
        string commonDirectory = Path.Combine(Path.GetTempPath(), ".claude", "skills");
        AgentDefinitionSkillsViewModel viewModel = CreateViewModel(
            workingDirectory,
            [new AgentSkillSource(".claude", commonDirectory)]);
        AgentSkillSelectionRequestedEventArgs? selectionRequest = null;
        bool directorySelectionRequested = false;
        viewModel.SkillSelectionRequested += (_, args) => selectionRequest = args;
        viewModel.SkillDirectorySelectionRequested += (_, _) => directorySelectionRequested = true;

        viewModel.AddSkillCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selectionRequest?.SkillSources.Select(source => source.Name),
                Is.EqualTo(["Working directory", ".claude"]));
            Assert.That(selectionRequest?.SkillSources.Select(source => source.Directory),
                Is.EqualTo([workingDirectory, commonDirectory]));
            Assert.That(directorySelectionRequested, Is.False);
        }
    }

    [Test]
    public void AddSkillCommand_RequestsFolderBeforeOpeningUnifiedPickerWhenWorkingDirectoryIsBlank()
    {
        string commonDirectory = Path.Combine(Path.GetTempPath(), ".copilot", "skills");
        AgentDefinitionSkillsViewModel viewModel = CreateViewModel(
            null,
            [new AgentSkillSource(".copilot", commonDirectory)]);
        bool directorySelectionRequested = false;
        AgentSkillSelectionRequestedEventArgs? selectionRequest = null;
        viewModel.SkillDirectorySelectionRequested += (_, _) => directorySelectionRequested = true;
        viewModel.SkillSelectionRequested += (_, args) => selectionRequest = args;

        viewModel.AddSkillCommand.Execute(null);
        string selectedDirectory = Path.Combine(Path.GetTempPath(), "selected-skills");
        viewModel.RequestSkillSelection(selectedDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(directorySelectionRequested, Is.True);
            Assert.That(selectionRequest?.SkillSources.Select(source => source.Name),
                Is.EqualTo(["Selected folder", ".copilot"]));
            Assert.That(selectionRequest?.SkillSources.Select(source => source.Directory),
                Is.EqualTo([selectedDirectory, commonDirectory]));
        }
    }

    [Test]
    public void ApplySkillSelection_UpdatesSelectedRootsAndPreservesUnrepresentedAttachments()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "sentinel-working-skills");
        string commonDirectory = Path.Combine(Path.GetTempPath(), ".agents", "skills");
        string unrelatedDirectory = Path.Combine(Path.GetTempPath(), "unrelated-skills");
        int draftChangedCount = 0;
        AgentDefinitionSkillsViewModel viewModel = CreateViewModel(
            workingDirectory,
            [new AgentSkillSource(".agents", commonDirectory)],
            [
                new AgentSkillAttachmentDraft(workingDirectory, ["old-working-skill"]),
                new AgentSkillAttachmentDraft(commonDirectory, ["old-common-skill"]),
                new AgentSkillAttachmentDraft(unrelatedDirectory, ["preserve-me"]),
            ],
            () => draftChangedCount++);

        viewModel.ApplySkillSelection(
        [
            new AgentSkillAttachmentDraft(workingDirectory, ["new-working-skill"]),
            new AgentSkillAttachmentDraft(commonDirectory, []),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SkillItems, Has.Count.EqualTo(2));
            Assert.That(viewModel.SkillItems.Single(item => item.SkillDirectory == workingDirectory).SelectedSkills,
                Is.EqualTo(["new-working-skill"]));
            Assert.That(viewModel.SkillItems.Single(item => item.SkillDirectory == unrelatedDirectory).SelectedSkills,
                Is.EqualTo(["preserve-me"]));
            Assert.That(viewModel.SkillItems.Any(item => item.SkillDirectory == commonDirectory), Is.False);
            Assert.That(draftChangedCount, Is.EqualTo(1));
        }
    }

    private static AgentDefinitionSkillsViewModel CreateViewModel(
        string? defaultWorkingDirectory,
        IReadOnlyList<AgentSkillSource>? commonSkillSources = null,
        IReadOnlyList<AgentSkillAttachmentDraft>? skillAttachments = null,
        Action? draftChanged = null)
        => new(
            skillAttachments ?? Array.Empty<AgentSkillAttachmentDraft>(),
            commonSkillSources ?? Array.Empty<AgentSkillSource>(),
            defaultWorkingDirectory,
            draftChanged ?? (() => { }),
            _ => { });
}
