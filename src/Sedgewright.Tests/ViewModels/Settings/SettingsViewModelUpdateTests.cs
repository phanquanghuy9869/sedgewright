using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Tests.ViewModels.Settings;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Infras.Storage;
using Sedgewright.Models;
using Sedgewright.Models.Updates;
using Sedgewright.ViewModels.Settings;

namespace Sedgewright.Tests.ViewModels.Settings;

[TestFixture]
public sealed class SettingsViewModelUpdateTests
{
    [Test]
    public void SelectSettingsSectionCommand_SwitchesVisibleSection()
    {
        FakeUpdateService updateService = new(new UpdateCheckResult(false, "1.0.0", null));
        using SedgewrightDbContext context = CreateContext();
        SettingsViewModel viewModel = CreateViewModel(context, updateService);

        viewModel.SelectSettingsSectionCommand.Execute("Appearance");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SelectedSettingsSection, Is.EqualTo("Appearance"));
            Assert.That(viewModel.IsAppearanceSelected, Is.True);
            Assert.That(viewModel.IsProvidersSelected, Is.False);
        }
    }

    [Test]
    public async Task UpdateService_CanBeConstructedOutsideVelopackInstallation()
    {
        UpdateService updateService = new();

        UpdateCheckResult result = await updateService.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsUpdateAvailable, Is.False);
            Assert.That(result.ErrorMessage, Is.Null);
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenUpdateIsAvailable_ExposesAvailableVersion()
    {
        UpdateCheckResult result = new(true, "1.0.0", "1.1.0");
        FakeUpdateService updateService = new(result);
        await using SedgewrightDbContext context = CreateContext();
        SettingsViewModel viewModel = CreateViewModel(context, updateService);

        await viewModel.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.CurrentVersion, Is.EqualTo("1.0.0"));
            Assert.That(viewModel.AvailableVersion, Is.EqualTo("1.1.0"));
            Assert.That(viewModel.HasAvailableUpdate, Is.True);
            Assert.That(viewModel.CanUpdateNow, Is.True);
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenNoUpdateIsAvailable_ShowsCurrentStatus()
    {
        FakeUpdateService updateService = new(new UpdateCheckResult(false, "1.0.0", null));
        await using SedgewrightDbContext context = CreateContext();
        SettingsViewModel viewModel = CreateViewModel(context, updateService);

        await viewModel.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasAvailableUpdate, Is.False);
            Assert.That(viewModel.UpdateStatusText, Is.EqualTo("You're up to date."));
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenServiceFails_ShowsFriendlyStatus()
    {
        FakeUpdateService updateService = new(new UpdateCheckResult(
            IsUpdateAvailable: false,
            CurrentVersion: "1.0.0",
            AvailableVersion: null,
            ErrorMessage: "GitHub is unavailable."));
        await using SedgewrightDbContext context = CreateContext();
        SettingsViewModel viewModel = CreateViewModel(context, updateService);

        await viewModel.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasAvailableUpdate, Is.False);
            Assert.That(viewModel.UpdateStatusText, Is.EqualTo("Unable to check for updates right now."));
            Assert.That(viewModel.IsUpdateBusy, Is.False);
        }
    }

    [Test]
    public async Task UpdateNowAsync_WhenUpdateIsAvailable_InvokesServiceAndRestoresState()
    {
        FakeUpdateService updateService = new(new UpdateCheckResult(true, "1.0.0", "1.1.0"));
        await using SedgewrightDbContext context = CreateContext();
        SettingsViewModel viewModel = CreateViewModel(context, updateService);

        await viewModel.CheckForUpdatesAsync();
        await viewModel.UpdateNowAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updateService.ApplyUpdateCallCount, Is.EqualTo(1));
            Assert.That(viewModel.IsUpdateBusy, Is.False);
            Assert.That(viewModel.UpdateStatusText, Is.EqualTo("Update downloaded. Restart Sedgewright to finish installing it."));
        }
    }

    private static SettingsViewModel CreateViewModel(
        SedgewrightDbContext context,
        IUpdateService updateService)
        => new(
            new LLMProviderRepository(context),
            new EmptySharedState(),
            updateService);

    private static SedgewrightDbContext CreateContext()
    {
        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new SedgewrightDbContext(options);
    }

    private sealed class FakeUpdateService(UpdateCheckResult result) : IUpdateService
    {
        public int ApplyUpdateCallCount { get; private set; }

        public string CurrentVersion => result.CurrentVersion;

        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task ApplyUpdateAsync(CancellationToken cancellationToken = default)
        {
            ApplyUpdateCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptySharedState : ISharedState
    {
        public ApplicationSettings? ApplicationSettings => null;

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
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
