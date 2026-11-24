using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentDefinitionsViewModelTests
{
    [Test]
    public void FilterText_MatchesNameDescriptionProviderAndModel()
    {
        FakeAgentDefinitionRepository repository = new();
        repository.Definitions.AddRange([
            CreateAgent("Research agent", "Focused research assistant", "OpenAI", "gpt-5"),
            CreateAgent("Writing agent", "Creates concise release notes", "Anthropic", "claude-sonnet")]);

        AgentDefinitionsViewModel viewModel = CreateViewModel(repository);

        viewModel.FilterText = "research";
        Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));

        viewModel.FilterText = "release";
        Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));

        viewModel.FilterText = "anthropic";
        Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));

        viewModel.FilterText = "claude";
        Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));
    }

    [Test]
    public void FilterText_TrimsWhitespaceAndMatchesCaseInsensitively()
    {
        FakeAgentDefinitionRepository repository = new();
        repository.Definitions.Add(CreateAgent("Research agent", "Focused assistant", "OpenAI", "gpt-5"));
        AgentDefinitionsViewModel viewModel = CreateViewModel(repository);

        viewModel.FilterText = "  RESEARCH  ";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.FilterText, Is.EqualTo("RESEARCH"));
            Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasNoMatches, Is.False);
        }
    }

    [Test]
    public void ClearingFilter_RestoresAllItems()
    {
        FakeAgentDefinitionRepository repository = new();
        repository.Definitions.AddRange([
            CreateAgent("Research agent", "Focused assistant", "OpenAI", "gpt-5"),
            CreateAgent("Writing agent", "Release notes", "Anthropic", "claude-sonnet")]);
        AgentDefinitionsViewModel viewModel = CreateViewModel(repository);

        viewModel.FilterText = "research";
        viewModel.FilterText = string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(2));
            Assert.That(viewModel.HasVisibleItems, Is.True);
            Assert.That(viewModel.HasNoMatches, Is.False);
        }
    }

    [Test]
    public void NonMatchingFilter_UsesNoMatchState()
    {
        FakeAgentDefinitionRepository repository = new();
        repository.Definitions.Add(CreateAgent("Research agent", "Focused assistant", "OpenAI", "gpt-5"));
        AgentDefinitionsViewModel viewModel = CreateViewModel(repository);

        viewModel.FilterText = "missing";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasItems, Is.True);
            Assert.That(viewModel.HasNoItems, Is.False);
            Assert.That(viewModel.HasVisibleItems, Is.False);
            Assert.That(viewModel.HasNoMatches, Is.True);
        }
    }

    [Test]
    public void EmptyRepository_UsesEmptyStateInsteadOfNoMatchState()
    {
        AgentDefinitionsViewModel viewModel = CreateViewModel(new FakeAgentDefinitionRepository());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasItems, Is.False);
            Assert.That(viewModel.HasNoItems, Is.True);
            Assert.That(viewModel.HasNoMatches, Is.False);
        }
    }

    [Test]
    public async Task RefreshAsync_ReappliesCurrentFilter()
    {
        FakeAgentDefinitionRepository repository = new();
        repository.Definitions.Add(CreateAgent("Old agent", "Initial", "OpenAI", "gpt-5"));
        AgentDefinitionsViewModel viewModel = CreateViewModel(repository);
        viewModel.FilterText = "new";

        repository.Definitions.Clear();
        repository.Definitions.AddRange([
            CreateAgent("Other agent", "Does not match", "Anthropic", "claude-sonnet"),
            CreateAgent("New agent", "Matches the active filter", "OpenAI", "gpt-5")]);

        await viewModel.RefreshAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.FilterText, Is.EqualTo("new"));
            Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.VisibleItems[0].Name, Is.EqualTo("New agent"));
        }
    }

    private static AgentDefinitionsViewModel CreateViewModel(IAgentDefinitionRepository repository)
        => new(repository, null!, null!, null!, null!);

    private static AgentDefinition CreateAgent(
        string name,
        string description,
        string providerDisplayName,
        string model)
        => new()
        {
            Id = 1,
            Name = name,
            Description = description,
            Model = model,
            Provider = new LLMProvider { DisplayName = providerDisplayName },
        };

    private sealed class FakeAgentDefinitionRepository : IAgentDefinitionRepository
    {
        public List<AgentDefinition> Definitions { get; } = [];

        public Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Definitions);

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
            => throw new NotSupportedException();

        public void Update(AgentDefinition definition)
            => throw new NotSupportedException();

        public void Delete(AgentDefinition definition)
            => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
