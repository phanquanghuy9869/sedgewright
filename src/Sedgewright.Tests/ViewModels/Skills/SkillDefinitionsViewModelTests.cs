using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using NUnit.Framework;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;
using Sedgewright.ViewModels.Skills;

namespace Sedgewright.Tests.ViewModels.Skills;

[TestFixture]
public sealed class SkillDefinitionsViewModelTests
{
    [Test]
    public async Task RefreshAsync_GroupsWorkingAndCommonSkillsAndFiltersByName()
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

            FakeSharedState sharedState = new(new ApplicationSettings
            {
                DefaultWorkingDirectory = workingDirectory,
            });
            AgentSkillDiscoveryService discoveryService = new();
            SkillDefinitionsViewModel viewModel = new(
                new FixedSourcesSkillDiscoveryService(
                    [new AgentSkillSource(".agents", commonDirectory)],
                    discoveryService),
                sharedState);

            await viewModel.RefreshAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasSkills, Is.True);
                Assert.That(viewModel.HasNoSkills, Is.False);
                Assert.That(viewModel.SkillSources, Has.Count.EqualTo(2));
                Assert.That(viewModel.SkillCount, Is.EqualTo(3));
                Assert.That(viewModel.SourceCount, Is.EqualTo(2));
            }

            viewModel.FilterText = "  BeTa  ";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.FilterText, Is.EqualTo("BeTa"));
                Assert.That(viewModel.VisibleSkillSources, Has.Count.EqualTo(1));
                Assert.That(viewModel.VisibleSkillSources[0].Name, Is.EqualTo("Working directory"));
                Assert.That(viewModel.VisibleSkillSources[0].VisibleSkills.Select(skill => skill.Name),
                    Is.EqualTo(["Beta"]));
            }

            viewModel.FilterText = "missing";
            Assert.That(viewModel.HasNoMatches, Is.True);

            viewModel.FilterText = string.Empty;
            Assert.That(viewModel.VisibleSkillSources, Has.Count.EqualTo(2));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
            Directory.Delete(commonDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RefreshAsync_ExposesEmptyStateWhenNoSkillDirectoryContainsSkills()
    {
        string emptyDirectory = CreateTemporaryDirectory();
        try
        {
            FakeSharedState sharedState = new(new ApplicationSettings
            {
                DefaultWorkingDirectory = emptyDirectory,
            });
            SkillDefinitionsViewModel viewModel = new(
                new FixedSourcesSkillDiscoveryService([], new AgentSkillDiscoveryService()),
                sharedState);

            await viewModel.RefreshAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasSkills, Is.False);
                Assert.That(viewModel.HasNoSkills, Is.True);
                Assert.That(viewModel.HasNoMatches, Is.False);
                Assert.That(viewModel.SkillCount, Is.EqualTo(0));
            }
        }
        finally
        {
            Directory.Delete(emptyDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RefreshAsync_ReportsFailedWorkingDirectoryWithoutDiscardingCommonSkills()
    {
        string commonDirectory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(commonDirectory, "Available"));
            File.WriteAllText(Path.Combine(commonDirectory, "Available", "SKILL.md"), "# Available");
            string missingDirectory = Path.Combine(Path.GetTempPath(), $"missing-skills-{Guid.NewGuid():N}");

            FakeSharedState sharedState = new(new ApplicationSettings
            {
                DefaultWorkingDirectory = missingDirectory,
            });
            SkillDefinitionsViewModel viewModel = new(
                new FixedSourcesSkillDiscoveryService(
                    [new AgentSkillSource(".agents", commonDirectory)],
                    new AgentSkillDiscoveryService()),
                sharedState);

            await viewModel.RefreshAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.HasSkills, Is.True);
                Assert.That(viewModel.SkillSources, Has.Count.EqualTo(1));
                Assert.That(viewModel.HasStatusText, Is.True);
                Assert.That(viewModel.StatusText, Does.Contain("Working directory"));
            }
        }
        finally
        {
            Directory.Delete(commonDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sedgewright-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FixedSourcesSkillDiscoveryService(
        IReadOnlyList<AgentSkillSource> sources,
        AgentSkillDiscoveryService discoveryService) : IAgentSkillDiscoveryService
    {
        public IReadOnlyList<AgentSkillSource> GetCommonSkillSources() => sources;

        public Result<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError> Discover(string sourceDirectory)
            => discoveryService.Discover(sourceDirectory);
    }

    private sealed class FakeSharedState(ApplicationSettings settings) : ISharedState
    {
        private ApplicationSettings _applicationSettings = settings;

        public ApplicationSettings? ApplicationSettings => _applicationSettings;

        public event EventHandler? ApplicationSettingsChanged
        {
            add { }
            remove { }
        }

        public ObservableCollection<AgentDefinitionSnapshot> Agents { get; } = [];

        public Maybe<AgentRuntime> AgentRuntime => Maybe<AgentRuntime>.None;

        public ObservableCollection<AgentSessionSnapshot> AgentSessions { get; } = [];

        public ObservableCollection<OrchestrationListItemSnapshot> Orchestrations { get; } = [];

        public ObservableCollection<LLMProvider> Providers { get; } = [];

        public Task RefreshAgentsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void SetAgentSessions(IEnumerable<AgentSessionSnapshot> agentSessions)
        {
        }

        public Task SetAgentRuntimeAsync(AgentRuntime agentRuntime)
            => Task.CompletedTask;

        public Task RefreshOrchestrationsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshProvidersAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshApplicationSettingsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveApplicationSettingsAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default)
        {
            _applicationSettings = settings;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
