using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;
using Sedgewright.ViewModels.Tools;

namespace Sedgewright.Tests.ViewModels.Tools;

[TestFixture]
public sealed class ToolDefinitionsViewModelTests
{
    [Test]
    public void DeleteAssignedToolDefinition_ShowsAgentNamesAndDoesNotDelete()
    {
        ToolDefinition definition = new()
        {
            Name = "assigned-workflow-tool",
            Type = ToolDefinitionType.Workflow,
        };
        FakeToolDefinitionRepository repository = new(definition, ["Alpha agent", "Zulu agent"]);
        ToolDefinitionsViewModel viewModel = new(
            repository,
            new FakeOrchestrationQueryRepository(),
            new FakeToolDefinitionDialogService());

        viewModel.Items.Single().DeleteCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.StatusText, Is.EqualTo(
                "Cannot delete \"assigned-workflow-tool\" because it is assigned to these agents: "
                + "Alpha agent, Zulu agent. Remove it from those agents first."));
            Assert.That(repository.DeleteCallCount, Is.Zero);
            Assert.That(repository.SaveChangesCallCount, Is.Zero);
        }
    }

    private sealed class FakeToolDefinitionRepository(
        ToolDefinition definition,
        List<string> assignedAgentNames) : IToolDefinitionRepository
    {
        private readonly List<ToolDefinition> _definitions = [definition];

        public int DeleteCallCount { get; private set; }

        public int SaveChangesCallCount { get; private set; }

        public Task<List<ToolDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_definitions.ToList());

        public Task<ToolDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_definitions.FirstOrDefault(item => item.Id == id));

        public Task<List<string>> GetAssignedAgentNamesAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(assignedAgentNames);

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public void Create(ToolDefinition definition)
            => _definitions.Add(definition);

        public void Update(ToolDefinition definition)
        {
        }

        public void Delete(ToolDefinition definition)
        {
            DeleteCallCount++;
            _definitions.Remove(definition);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeOrchestrationQueryRepository : IOrchestrationQueryRepository
    {
        public Task<List<OrchestrationListItemSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<OrchestrationListItemSnapshot>());
    }

    private sealed class FakeToolDefinitionDialogService : IToolDefinitionDialogService
    {
        public Task<UnitResult<IApplicationError>> ShowAsync(ToolDefinitionEditorViewModel viewModel)
            => Task.FromResult(UnitResult.Success<IApplicationError>());
    }
}
