using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Tests.Apps.UseCases;

[TestFixture]
public sealed class ChatSessionServiceTests
{
    [Test]
    public async Task SaveLastTargetAsync_PersistsWorkflowIdentityAndPreservesOtherSettings()
    {
        ApplicationSettings settings = new()
        {
            DefaultWorkingDirectory = "D:\\work",
            ThemeMode = ThemeMode.Dark,
        };
        FakeSharedState sharedState = new(settings);
        SequentialOrchestration workflow = new()
        {
            Id = 18,
            Name = "Release workflow",
        };
        ChatSessionService service = CreateService(sharedState, [workflow]);

        UnitResult<IApplicationError> result = await service.SaveLastTargetAsync(workflow);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(sharedState.ApplicationSettings!.LastChatTargetType, Is.EqualTo(ChatTargetType.SequentialOrchestration));
            Assert.That(sharedState.ApplicationSettings.LastChatTargetId, Is.EqualTo(workflow.Id));
            Assert.That(sharedState.ApplicationSettings.LastAgentSessionId, Is.Null);
            Assert.That(sharedState.ApplicationSettings.DefaultWorkingDirectory, Is.EqualTo("D:\\work"));
            Assert.That(sharedState.ApplicationSettings.ThemeMode, Is.EqualTo(ThemeMode.Dark));
        }
    }

    [Test]
    public async Task RestoreLastTargetAsync_ReselectsSavedWorkflow()
    {
        SequentialOrchestration workflow = new()
        {
            Id = 6,
            Name = "Saved workflow",
        };
        FakeSharedState sharedState = new(new ApplicationSettings
        {
            LastChatTargetType = ChatTargetType.SequentialOrchestration,
            LastChatTargetId = workflow.Id,
        });
        ChatSessionService service = CreateService(sharedState, [workflow]);

        Result<IChatTarget?, IApplicationError> result = await service.RestoreLastTargetAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.SameAs(workflow));
            Assert.That(sharedState.AgentRuntime.HasValue, Is.False);
        }
    }

    [Test]
    public async Task RestoreLastTargetAsync_ReportsMissingAgentSession()
    {
        AgentDefinition agent = new()
        {
            Id = 4,
            Name = "Saved agent",
            Description = "Agent with no selected session",
            Model = "test-model",
        };
        FakeSharedState sharedState = new(new ApplicationSettings
        {
            LastChatTargetType = ChatTargetType.Agent,
            LastChatTargetId = agent.Id,
        });
        ChatSessionService service = CreateService(sharedState, [agent]);

        Result<IChatTarget?, IApplicationError> result = await service.RestoreLastTargetAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Messages, Does.Contain("The previously selected agent has no saved session to resume."));
            Assert.That(sharedState.AgentRuntime.HasValue, Is.False);
        }
    }

    private static ChatSessionService CreateService(
        FakeSharedState sharedState,
        IReadOnlyList<IChatTarget> targets)
        => new(
            sharedState,
            new FakeChatTargetCatalogService(targets),
            new FakeAgentDefinitionRepository(),
            new FakeAgentSessionRepository());

    private sealed class FakeChatTargetCatalogService(IReadOnlyList<IChatTarget> targets)
        : IChatTargetCatalogService
    {
        public Task<IReadOnlyList<IChatTarget>> GetTargetsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(targets);
    }

    private sealed class FakeAgentDefinitionRepository : IAgentDefinitionRepository
    {
        public Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<AgentDefinition>());

        public Task<AgentDefinition?> GetAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<AgentDefinition?>(null);

        public Task<bool> NameExistsAsync(
            string name,
            int? excludeId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasReferencesAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public void Create(AgentDefinition definition)
        {
        }

        public void Update(AgentDefinition definition)
        {
        }

        public void Delete(AgentDefinition definition)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeAgentSessionRepository : IAgentSessionRepository
    {
        public Task<List<AgentSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<AgentSessionRecord>());

        public Task<AgentSessionRecord?> GetAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<AgentSessionRecord?>(null);

        public void Create(AgentSessionRecord session)
        {
        }

        public void Update(AgentSessionRecord session)
        {
        }

        public void Delete(AgentSessionRecord session)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
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
