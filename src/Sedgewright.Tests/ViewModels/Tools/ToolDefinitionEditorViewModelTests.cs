using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Tools;

namespace Sedgewright.Tests.ViewModels.Tools;

[TestFixture]
public sealed class ToolDefinitionEditorViewModelTests
{
    [Test]
    public async Task NewEditor_UsesWorkflowTypeAndNoSelection()
    {
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(null, []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Type, Is.EqualTo(ToolDefinitionType.Workflow));
            Assert.That(viewModel.SelectedWorkflow, Is.Null);
            Assert.That(viewModel.SaveCommand.CanExecute(null), Is.False);
        }
    }

    [Test]
    public async Task Save_WithoutWorkflowSelection_IsDisabled()
    {
        FakeToolDefinitionRepository repository = new();
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(
            null,
            [CreateWorkflow(OrchestrationKind.Sequential, 1)],
            repository);
        viewModel.Name = "workflow-tool";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SaveCommand.CanExecute(null), Is.False);
            Assert.That(repository.SavedDefinition, Is.Null);
        }
    }

    [Test]
    public async Task Save_DuplicateName_DoesNotPersist()
    {
        FakeToolDefinitionRepository repository = new() { NameExists = true };
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(
            null,
            [CreateWorkflow(OrchestrationKind.Sequential, 1)],
            repository);
        viewModel.Name = "duplicate-tool";
        viewModel.SelectedWorkflow = viewModel.WorkflowOptions[0];

        viewModel.SaveCommand.Execute(null);
        await Task.Delay(50);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.ValidationText, Is.EqualTo("A tool named 'duplicate-tool' already exists."));
            Assert.That(repository.SavedDefinition, Is.Null);
        }
    }

    [Test]
    public async Task Edit_ResolvesSelectionByKindAndId()
    {
        Guid definitionId = Guid.NewGuid();
        ToolDefinition definition = new()
        {
            Id = definitionId,
            Name = "workflow-tool",
            Type = ToolDefinitionType.Workflow,
            ConcurrentOrchestrationId = 23,
        };
        OrchestrationListItemSnapshot sameIdDifferentKind = CreateWorkflow(OrchestrationKind.Sequential, 23);
        OrchestrationListItemSnapshot matchingWorkflow = CreateWorkflow(OrchestrationKind.Concurrent, 23);
        FakeToolDefinitionRepository repository = new() { DefinitionToLoad = definition };

        ToolDefinitionEditorViewModel viewModel = await CreateEditor(
            definitionId,
            [sameIdDifferentKind, matchingWorkflow],
            repository);

        Assert.That(viewModel.SelectedWorkflow, Is.SameAs(matchingWorkflow));
    }

    [Test]
    public async Task Edit_KeepingWorkflow_PreservesWorkflowNavigation()
    {
        Guid definitionId = Guid.NewGuid();
        MagenticOrchestration orchestration = new()
        {
            Id = 1,
            Name = "existing-workflow",
        };
        ToolDefinition definition = new()
        {
            Id = definitionId,
            Name = "workflow-tool",
            Type = ToolDefinitionType.Workflow,
            MagenticOrchestrationId = orchestration.Id,
            MagenticOrchestration = orchestration,
        };
        FakeToolDefinitionRepository repository = new() { DefinitionToLoad = definition };
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(
            definitionId,
            [CreateWorkflow(OrchestrationKind.Magentic, orchestration.Id)],
            repository);
        viewModel.Name = "renamed-tool";

        Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
        viewModel.SaveCommand.Execute(null);
        UnitResult<IApplicationError> result = await closeTask;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(repository.SavedDefinition!.MagenticOrchestrationId, Is.EqualTo(orchestration.Id));
            Assert.That(repository.SavedDefinition.MagenticOrchestration, Is.SameAs(orchestration));
        }
    }

    [Test]
    public async Task AddEditor_DoesNotLoadDefinition()
    {
        FakeToolDefinitionRepository repository = new();

        _ = await CreateEditor(null, [], repository);

        Assert.That(repository.GetCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task EditEditor_LoadsDefinitionById()
    {
        Guid definitionId = Guid.NewGuid();
        FakeToolDefinitionRepository repository = new()
        {
            DefinitionToLoad = new ToolDefinition
            {
                Id = definitionId,
                Name = "loaded-tool",
                ToolDescription = "loaded description",
                Type = ToolDefinitionType.Workflow,
            },
        };

        ToolDefinitionEditorViewModel? viewModel = await ToolDefinitionEditorViewModel.CreateAsync(
            definitionId,
            [],
            repository,
            (name, excludeId) => repository.NameExistsAsync(name, excludeId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel, Is.Not.Null);
            Assert.That(repository.GetCallCount, Is.EqualTo(1));
            Assert.That(repository.LastRequestedId, Is.EqualTo(definitionId));
            Assert.That(viewModel!.Name, Is.EqualTo("loaded-tool"));
            Assert.That(viewModel.ToolDescription, Is.EqualTo("loaded description"));
        }
    }

    [Test]
    public async Task Save_ToolDescription_TrimsAndPersistsValue()
    {
        FakeToolDefinitionRepository repository = new();
        OrchestrationListItemSnapshot workflow = CreateWorkflow(OrchestrationKind.Sequential, 11);
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(null, [workflow], repository);
        viewModel.Name = "workflow-tool";
        viewModel.ToolDescription = "  tool description  ";
        viewModel.SelectedWorkflow = workflow;

        Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
        viewModel.SaveCommand.Execute(null);
        UnitResult<IApplicationError> result = await closeTask;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(repository.SavedDefinition!.ToolDescription, Is.EqualTo("tool description"));
        }
    }

    [Test]
    public async Task EditEditor_WithMissingDefinition_DoesNotCreateEditor()
    {
        Guid definitionId = Guid.NewGuid();
        FakeToolDefinitionRepository repository = new();

        ToolDefinitionEditorViewModel? viewModel = await ToolDefinitionEditorViewModel.CreateAsync(
            definitionId,
            [],
            repository,
            (name, excludeId) => repository.NameExistsAsync(name, excludeId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel, Is.Null);
            Assert.That(repository.GetCallCount, Is.EqualTo(1));
            Assert.That(repository.LastRequestedId, Is.EqualTo(definitionId));
        }
    }

    [TestCase(OrchestrationKind.Sequential, 11)]
    [TestCase(OrchestrationKind.Concurrent, 12)]
    [TestCase(OrchestrationKind.GroupChat, 13)]
    [TestCase(OrchestrationKind.Magentic, 14)]
    [TestCase(OrchestrationKind.Handoff, 15)]
    public async Task Save_WorkflowSelection_SetsOnlyMatchingForeignKey(OrchestrationKind kind, int id)
    {
        FakeToolDefinitionRepository repository = new();
        OrchestrationListItemSnapshot workflow = CreateWorkflow(kind, id);
        ToolDefinitionEditorViewModel viewModel = await CreateEditor(null, [workflow], repository);
        viewModel.Name = "workflow-tool";
        viewModel.SelectedWorkflow = workflow;

        Task<UnitResult<IApplicationError>> closeTask = WaitForCloseAsync(viewModel);
        viewModel.SaveCommand.Execute(null);
        UnitResult<IApplicationError> result = await closeTask;
        ToolDefinition savedDefinition = repository.SavedDefinition!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(savedDefinition.Type, Is.EqualTo(ToolDefinitionType.Workflow));
            Assert.That(savedDefinition.Name, Is.EqualTo("workflow-tool"));
            Assert.That(CountWorkflowReferences(savedDefinition), Is.EqualTo(1));
            Assert.That(GetWorkflowReference(savedDefinition, kind), Is.EqualTo(id));
        }
    }

    private static async Task<ToolDefinitionEditorViewModel> CreateEditor(
        Guid? definitionId,
        IReadOnlyList<OrchestrationListItemSnapshot> workflows,
        FakeToolDefinitionRepository? repository = null)
    {
        FakeToolDefinitionRepository selectedRepository = repository ?? new();
        ToolDefinitionEditorViewModel? editor = await ToolDefinitionEditorViewModel.CreateAsync(
            definitionId,
            workflows,
            selectedRepository,
            (name, excludeId) => selectedRepository.NameExistsAsync(name, excludeId));

        return editor ?? throw new AssertionException("The test definition could not be loaded.");
    }

    private static OrchestrationListItemSnapshot CreateWorkflow(OrchestrationKind kind, int id)
        => new(id, kind, $"{kind}-{id}", string.Empty, "workflow");

    private static async Task<UnitResult<IApplicationError>> WaitForCloseAsync(ToolDefinitionEditorViewModel viewModel)
    {
        TaskCompletionSource<UnitResult<IApplicationError>> completion = new();
        viewModel.CloseRequested += result => completion.TrySetResult(result);
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static int CountWorkflowReferences(ToolDefinition definition)
        => new int?[]
        {
            definition.SequentialOrchestrationId,
            definition.ConcurrentOrchestrationId,
            definition.GroupChatOrchestrationId,
            definition.MagenticOrchestrationId,
            definition.HandoffOrchestrationId,
        }.Count(reference => reference.HasValue);

    private static int? GetWorkflowReference(ToolDefinition definition, OrchestrationKind kind)
        => kind switch
        {
            OrchestrationKind.Sequential => definition.SequentialOrchestrationId,
            OrchestrationKind.Concurrent => definition.ConcurrentOrchestrationId,
            OrchestrationKind.GroupChat => definition.GroupChatOrchestrationId,
            OrchestrationKind.Magentic => definition.MagenticOrchestrationId,
            OrchestrationKind.Handoff => definition.HandoffOrchestrationId,
            _ => null,
        };

    private sealed class FakeToolDefinitionRepository : IToolDefinitionRepository
    {
        public bool NameExists { get; init; }

        public ToolDefinition? DefinitionToLoad { get; init; }

        public int GetCallCount { get; private set; }

        public Guid? LastRequestedId { get; private set; }

        public ToolDefinition? SavedDefinition { get; private set; }

        public Task<List<ToolDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ToolDefinition>());

        public Task<ToolDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            LastRequestedId = id;
            return Task.FromResult(DefinitionToLoad?.Id == id ? DefinitionToLoad : null);
        }

        public Task<List<string>> GetAssignedAgentNamesAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(NameExists);

        public void Create(ToolDefinition definition)
            => SavedDefinition = definition;

        public void Update(ToolDefinition definition)
            => SavedDefinition = definition;

        public void Delete(ToolDefinition definition)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }
}
