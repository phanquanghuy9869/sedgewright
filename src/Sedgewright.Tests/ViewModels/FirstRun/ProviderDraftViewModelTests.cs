using System.Linq;
using Sedgewright.Entities;
using Sedgewright.ViewModels.FirstRun;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.Tests.ViewModels.FirstRun;

[TestFixture]
public sealed class ProviderDraftViewModelTests
{
    [Test]
    public void CopilotProvider_IsReadyWithoutKeyOrModelOnlyAfterOptIn()
    {
        ProviderDraftViewModel provider = new(nameof(LlmProviderKind.Copilot), "GitHub Copilot");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.IsReadyToPersist, Is.False);
            Assert.That(provider.ShowApiKeyInput, Is.False);
            Assert.That(provider.CanDiscoverModels, Is.False);
        }

        provider.IsEnabled = true;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.IsReadyToPersist, Is.True);
            Assert.That(provider.CanDiscoverModels, Is.True);
            Assert.That(provider.EnabledModelIds, Is.Empty);
        }
    }

    [Test]
    public void ProviderCatalog_IncludesCopilot()
    {
        Assert.That(
            ProviderCatalog.Entries.Any(entry => entry.ProviderKind == nameof(LlmProviderKind.Copilot)),
            Is.True);
    }
}
