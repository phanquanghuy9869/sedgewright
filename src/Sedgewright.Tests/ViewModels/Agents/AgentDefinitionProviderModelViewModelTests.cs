using Sedgewright.Entities;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentDefinitionProviderModelViewModelTests
{
    [Test]
    public void ChooseModelCommand_AllowsCopilotWithoutBaseUrl()
    {
        LLMProvider copilotProvider = new()
        {
            Id = 1,
            ProviderKind = LlmProviderKind.Copilot,
            DisplayName = "GitHub Copilot",
            BaseUrl = string.Empty,
            ApiKey = string.Empty,
            EnabledModelsJson = "[]",
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null) with
        {
            SelectedProvider = copilotProvider,
        };
        AgentDefinitionProviderModelViewModel viewModel = new(draft, [copilotProvider], () => { });

        Assert.That(viewModel.ChooseModelCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void ChooseModelCommand_RequiresBaseUrlForOpenAiCompatibleProviders()
    {
        LLMProvider provider = new()
        {
            Id = 1,
            ProviderKind = LlmProviderKind.OpenAICompatible,
            DisplayName = "OpenAI-compatible",
            BaseUrl = string.Empty,
            ApiKey = "test-key",
            EnabledModelsJson = "[]",
        };
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null) with
        {
            SelectedProvider = provider,
        };
        AgentDefinitionProviderModelViewModel viewModel = new(draft, [provider], () => { });

        Assert.That(viewModel.ChooseModelCommand.CanExecute(null), Is.False);
    }
}
