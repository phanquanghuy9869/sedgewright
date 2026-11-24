using Sedgewright.Entities;
using Sedgewright.ViewModels.Settings;

namespace Sedgewright.Tests.ViewModels.Settings;

[TestFixture]
public sealed class ProviderSettingsItemViewModelTests
{
    [Test]
    public void CopilotProvider_CanBeEnabledWithoutApiKey()
    {
        LLMProvider entity = new()
        {
            ProviderKind = LlmProviderKind.Copilot,
            DisplayName = "GitHub Copilot",
            BaseUrl = string.Empty,
            ApiKey = string.Empty,
            EnabledModelsJson = "[]",
        };
        ProviderSettingsItemViewModel provider = new(entity);

        Assert.That(provider.ShowApiKeyInput, Is.False);
        Assert.That(provider.CanDiscoverModels, Is.False);

        provider.EnableCopilotCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.IsCopilotEnabled, Is.True);
            Assert.That(provider.CanDiscoverModels, Is.True);
            Assert.That(provider.IsDirty, Is.True);
        }
    }
}
