using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sedgewright.Infras.Services;
using Sedgewright.Models;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentSkillSelectionViewModelTests
{
    [Test]
    public void Constructor_GroupsSkillsBySourceAndPreservesExistingSelections()
    {
        string workingDirectory = CreateTemporaryDirectory();
        string commonDirectory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(workingDirectory, "beta"));
            File.WriteAllText(Path.Combine(workingDirectory, "beta", "SKILL.md"), "# Beta");
            Directory.CreateDirectory(Path.Combine(workingDirectory, "nested", "Alpha"));
            File.WriteAllText(Path.Combine(workingDirectory, "nested", "Alpha", "SKILL.md"), "# Alpha");
            Directory.CreateDirectory(Path.Combine(commonDirectory, "Gamma"));
            File.WriteAllText(Path.Combine(commonDirectory, "Gamma", "SKILL.md"), "# Gamma");

            AgentSkillSelectionViewModel viewModel = new(
                [
                    new AgentSkillSource("Working directory", workingDirectory),
                    new AgentSkillSource(".agents", commonDirectory),
                ],
                [
                    new AgentSkillAttachmentDraft(workingDirectory, ["BETA"]),
                    new AgentSkillAttachmentDraft(commonDirectory, []),
                ],
                new AgentSkillDiscoveryService());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.SkillGroups, Has.Count.EqualTo(2));
                Assert.That(viewModel.SkillGroups[0].Name, Is.EqualTo("Working directory"));
                Assert.That(viewModel.SkillGroups[0].SkillItems.Select(item => item.Name),
                    Is.EqualTo(["Alpha", "beta"]));
                Assert.That(viewModel.SkillGroups[0].SkillItems[0].DisplayName,
                    Is.EqualTo($"Alpha ({Path.Combine("nested", "Alpha")})"));
                Assert.That(viewModel.SkillGroups[0].SkillItems[0].IsSelected, Is.False);
                Assert.That(viewModel.SkillGroups[0].SkillItems[1].IsSelected, Is.True);
                Assert.That(viewModel.SkillGroups[1].Name, Is.EqualTo(".agents"));
                Assert.That(viewModel.SkillGroups[1].SkillItems.Single().IsSelected, Is.True);
                Assert.That(viewModel.SelectedSkillAttachments[0].SelectedSkills, Is.EqualTo(["beta"]));
                Assert.That(viewModel.SelectedSkillAttachments[1].SelectedSkills, Is.EqualTo(["Gamma"]));
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
            Directory.Delete(commonDirectory, recursive: true);
        }
    }

    [Test]
    public void Constructor_ExposesEmptyStateWhenNoSourceContainsSkills()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            AgentSkillSelectionViewModel viewModel = new(
                [new AgentSkillSource("Working directory", root)],
                [],
                new AgentSkillDiscoveryService());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasSkills, Is.False);
                Assert.That(viewModel.HasNoSkills, Is.True);
                Assert.That(viewModel.HasStatusText, Is.False);
                Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.False);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FilterText_MatchesNamesAndClearingRestoresSkillsWithoutChangingSelections()
    {
        string workingDirectory = CreateTemporaryDirectory();
        string commonDirectory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(workingDirectory, "Alpha"));
            File.WriteAllText(Path.Combine(workingDirectory, "Alpha", "SKILL.md"), "# Alpha");
            Directory.CreateDirectory(Path.Combine(workingDirectory, "nested", "Beta"));
            File.WriteAllText(Path.Combine(workingDirectory, "nested", "Beta", "SKILL.md"), "# Beta");
            Directory.CreateDirectory(Path.Combine(commonDirectory, "Gamma"));
            File.WriteAllText(Path.Combine(commonDirectory, "Gamma", "SKILL.md"), "# Gamma");

            AgentSkillSelectionViewModel viewModel = new(
                [
                    new AgentSkillSource("Working directory", workingDirectory),
                    new AgentSkillSource(".agents", commonDirectory),
                ],
                [new AgentSkillAttachmentDraft(workingDirectory, ["Beta"])],
                new AgentSkillDiscoveryService());

            viewModel.FilterText = "  bEtA  ";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.FilterText, Is.EqualTo("bEtA"));
                Assert.That(viewModel.HasNoMatches, Is.False);
                Assert.That(viewModel.VisibleSkillGroups, Has.Count.EqualTo(1));
                Assert.That(viewModel.VisibleSkillGroups[0].Name, Is.EqualTo("Working directory"));
                Assert.That(viewModel.VisibleSkillGroups[0].VisibleSkillItems.Select(item => item.Name),
                    Is.EqualTo(["Beta"]));
                Assert.That(viewModel.SelectedSkillAttachments[0].SelectedSkills, Is.EqualTo(["Beta"]));
            }

            viewModel.FilterText = "missing";
            Assert.That(viewModel.HasNoMatches, Is.True);
            Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.True);

            viewModel.FilterText = string.Empty;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasNoMatches, Is.False);
                Assert.That(viewModel.VisibleSkillGroups, Has.Count.EqualTo(2));
                Assert.That(viewModel.VisibleSkillGroups[0].VisibleSkillItems, Has.Count.EqualTo(2));
                Assert.That(viewModel.VisibleSkillGroups[1].VisibleSkillItems, Has.Count.EqualTo(1));
                Assert.That(viewModel.SelectedSkillAttachments[0].SelectedSkills, Is.EqualTo(["Beta"]));
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
            Directory.Delete(commonDirectory, recursive: true);
        }
    }

    [Test]
    public void Constructor_ReportsFailedSourcesWithoutDiscardingSuccessfulGroups()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "available"));
            File.WriteAllText(Path.Combine(root, "available", "SKILL.md"), "# Available");
            string missingDirectory = Path.Combine(Path.GetTempPath(), $"sentinel-skills-{Guid.NewGuid():N}");

            AgentSkillSelectionViewModel viewModel = new(
                [
                    new AgentSkillSource("Working directory", missingDirectory),
                    new AgentSkillSource(".copilot", root),
                ],
                [],
                new AgentSkillDiscoveryService());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasSkills, Is.True);
                Assert.That(viewModel.HasStatusText, Is.True);
                Assert.That(viewModel.StatusText, Does.Contain("Working directory"));
                Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.True);
                Assert.That(viewModel.SelectedSkillAttachments, Has.Count.EqualTo(1));
                Assert.That(viewModel.SelectedSkillAttachments[0].SkillDirectory, Is.EqualTo(root));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sentinel-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
