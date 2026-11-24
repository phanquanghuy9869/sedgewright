using System.Collections.Generic;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;

namespace Sedgewright.ViewModels.Shared;

internal sealed record ProviderCatalogEntry(string ProviderKind, string DisplayName, string DefaultBaseUrl);

internal static class ProviderCatalog
{
    private static readonly ProviderCatalogEntry[] ProviderEntries =
    [
        new(nameof(LlmProviderKind.Copilot), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.Copilot)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.Copilot))),
        new(nameof(LlmProviderKind.OpenRouter), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.OpenRouter)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.OpenRouter))),
        new(nameof(LlmProviderKind.NvidiaNim), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.NvidiaNim)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.NvidiaNim))),
        new(nameof(LlmProviderKind.OpenAI), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.OpenAI)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.OpenAI))),
        new(nameof(LlmProviderKind.Anthropic), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.Anthropic)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.Anthropic))),
        new(nameof(LlmProviderKind.Gemini), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.Gemini)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.Gemini))),
        new(nameof(LlmProviderKind.Grok), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.Grok)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.Grok))),
        new(nameof(LlmProviderKind.Azure), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.Azure)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.Azure))),
        new(nameof(LlmProviderKind.OpenAICompatible), ProviderDiscoveryService.GetDefaultDisplayName(nameof(LlmProviderKind.OpenAICompatible)), ProviderDiscoveryService.GetDefaultBaseUrl(nameof(LlmProviderKind.OpenAICompatible))),
    ];

    public static IReadOnlyList<ProviderCatalogEntry> Entries => ProviderEntries;
}
