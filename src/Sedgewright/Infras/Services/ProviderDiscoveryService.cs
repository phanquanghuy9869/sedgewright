using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot;

namespace Sedgewright.Infras.Services;

internal sealed class ProviderDiscoveryService
{
    private static readonly HttpClient HttpClient = new();

    private const string AnthropicVersion = "2023-06-01";
    private const string OpenRouterReferer = "https://sentinel.local";
    private const string OpenRouterTitle = "Sedgewright";

    public static bool CanDiscoverModels(string providerKind)
    {
        string normalizedProviderKind = NormalizeProviderKind(providerKind);
        return normalizedProviderKind is nameof(ProviderKinds.OpenAI)
            or nameof(ProviderKinds.Copilot)
            or nameof(ProviderKinds.OpenAICompatible)
            or nameof(ProviderKinds.OpenRouter)
            or nameof(ProviderKinds.Anthropic)
            or nameof(ProviderKinds.Gemini)
            or nameof(ProviderKinds.Grok)
            or nameof(ProviderKinds.NvidiaNim)
            or nameof(ProviderKinds.Azure);
    }

    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(
        string providerKind,
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        string normalizedProviderKind = NormalizeProviderKind(providerKind);
        if (!CanDiscoverModels(normalizedProviderKind))
        {
            return [];
        }

        if (normalizedProviderKind == nameof(ProviderKinds.Copilot))
        {
            return await DiscoverCopilotModelsAsync(cancellationToken);
        }

        string normalizedBaseUrl = NormalizeRequired(baseUrl, "A provider base URL is required before refreshing models.");

        return normalizedProviderKind switch
        {
            nameof(ProviderKinds.Anthropic) => await DiscoverAnthropicModelsAsync(normalizedBaseUrl, apiKey, cancellationToken),
            nameof(ProviderKinds.OpenRouter) => await DiscoverOpenRouterModelsAsync(normalizedBaseUrl, apiKey, cancellationToken),
            _ => await DiscoverOpenAiModelsAsync(normalizedBaseUrl, apiKey, cancellationToken),
        };
    }

    public static string GetDefaultDisplayName(string providerKind)
        => NormalizeProviderKind(providerKind) switch
        {
            nameof(ProviderKinds.Copilot) => "GitHub Copilot",
            nameof(ProviderKinds.Azure) => "Azure OpenAI",
            nameof(ProviderKinds.Anthropic) => "Anthropic",
            nameof(ProviderKinds.OpenAI) => "OpenAI",
            nameof(ProviderKinds.OpenAICompatible) => "OpenAI-compatible",
            nameof(ProviderKinds.OpenRouter) => "OpenRouter",
            nameof(ProviderKinds.Gemini) => "Gemini",
            nameof(ProviderKinds.Grok) => "Grok",
            nameof(ProviderKinds.NvidiaNim) => "NVIDIA NIM",
            _ => "Provider",
        };

    public static string GetDefaultBaseUrl(string providerKind)
        => NormalizeProviderKind(providerKind) switch
        {
            nameof(ProviderKinds.Copilot) => string.Empty,
            nameof(ProviderKinds.OpenAI) => "https://api.openai.com/v1",
            nameof(ProviderKinds.OpenRouter) => "https://openrouter.ai/api/v1",
            nameof(ProviderKinds.Azure) => "https://your-resource.openai.azure.com/openai/v1",
            nameof(ProviderKinds.Anthropic) => "https://api.anthropic.com",
            nameof(ProviderKinds.Gemini) => "https://generativelanguage.googleapis.com/v1beta/openai/",
            nameof(ProviderKinds.Grok) => "https://api.x.ai/v1",
            nameof(ProviderKinds.NvidiaNim) => "https://integrate.api.nvidia.com/v1",
            nameof(ProviderKinds.OpenAICompatible) => "https://your-endpoint.example/v1",
            _ => string.Empty,
        };

    private static async Task<IReadOnlyList<string>> DiscoverOpenAiModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, BuildRequestUri(baseUrl, "models"));
        AddBearerToken(request, apiKey);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return ReadModelIdsFromDataArray(contentStream);
    }

    private static async Task<IReadOnlyList<string>> DiscoverCopilotModelsAsync(CancellationToken cancellationToken)
    {
        await using CopilotClient copilotClient = new(new CopilotClientOptions { UseLoggedInUser = true });
        await copilotClient.StartAsync(cancellationToken);
        IList<ModelInfo> models = await copilotClient.ListModelsAsync(cancellationToken);

        return models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Select(model => model.Id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> DiscoverOpenRouterModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, BuildRequestUri(baseUrl, "models"));
        AddBearerToken(request, apiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", OpenRouterReferer);
        request.Headers.TryAddWithoutValidation("X-Title", OpenRouterTitle);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return ReadModelIdsFromDataArray(contentStream);
    }

    private static async Task<IReadOnlyList<string>> DiscoverAnthropicModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        string normalizedApiKey = NormalizeRequired(apiKey, "An Anthropic API key is required before refreshing models.");

        using HttpRequestMessage request = new(HttpMethod.Get, BuildRequestUri(baseUrl, "v1/models"));
        request.Headers.Add("x-api-key", normalizedApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return ReadModelIdsFromDataArray(contentStream);
    }

    private static Uri BuildRequestUri(string baseUrl, string relativePath)
        => new($"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}", UriKind.Absolute);

    private static IReadOnlyList<string> ReadModelIdsFromDataArray(Stream jsonStream)
    {
        using JsonDocument document = JsonDocument.Parse(jsonStream);
        if (!document.RootElement.TryGetProperty("data", out JsonElement dataElement) || dataElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return dataElement
            .EnumerateArray()
            .Where(item => item.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("id").GetString())
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddBearerToken(HttpRequestMessage request, string apiKey)
    {
        string? normalizedApiKey = NormalizeOptional(apiKey);
        if (normalizedApiKey is null)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalizedApiKey);
    }

    private static string NormalizeRequired(string? value, string errorMessage)
        => NormalizeOptional(value) ?? throw new InvalidOperationException(errorMessage);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeProviderKind(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static class ProviderKinds
    {
        public const string Copilot = nameof(Copilot);
        public const string OpenAI = nameof(OpenAI);
        public const string OpenAICompatible = nameof(OpenAICompatible);
        public const string OpenRouter = nameof(OpenRouter);
        public const string Azure = nameof(Azure);
        public const string Anthropic = nameof(Anthropic);
        public const string Gemini = nameof(Gemini);
        public const string Grok = nameof(Grok);
        public const string NvidiaNim = nameof(NvidiaNim);
    }
}
