using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Net.Http;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class ChatClientExtensions
{
    extension(ChatClient chatClient)
    {
        public static ChatClient CreateOpenAI(string apiKey, string model, HttpClient? httpClient, Uri? endpoint = null)
        {
            OpenAIClientOptions options = new();
            if (httpClient is not null)
            {
                options.Transport = new HttpClientPipelineTransport(httpClient);
            }

            if (endpoint is not null)
            {
                options.Endpoint = endpoint;
            }

            return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model);
        }

        /// <summary>
        /// Creates a chat client for a saved provider entry using the OpenAI-compatible client factory.
        /// </summary>
        /// <param name="provider">The saved provider entry to convert into a chat client.</param>
        /// <param name="model">The model or deployment name to bind to the client.</param>
        /// <param name="httpClient">An optional shared HTTP client for transport configuration.</param>
        /// <returns>A chat client bound to the provider's configured endpoint.</returns>
        public static ChatClient FromProvider(LLMProvider provider, string model, HttpClient? httpClient = null)
        {
            ArgumentNullException.ThrowIfNull(provider);

            return provider.ProviderKind switch
            {
                LlmProviderKind.OpenAI => CreateOpenAI(provider.ApiKey, model, httpClient),
                LlmProviderKind.OpenAICompatible
                or LlmProviderKind.OpenRouter
                or LlmProviderKind.Azure
                or LlmProviderKind.Gemini
                or LlmProviderKind.Grok
                or LlmProviderKind.NvidiaNim => CreateOpenAI(
                    provider.ApiKey,
                    model,
                    httpClient,
                    TryCreateRequiredEndpoint(provider.BaseUrl)),
                LlmProviderKind.Copilot => throw new NotSupportedException("GitHub Copilot uses the native Copilot agent runtime, not an OpenAI-compatible chat client."),
                LlmProviderKind.Anthropic => throw new NotSupportedException("Anthropic providers are not supported by the OpenAI chat client factory."),
                _ => throw new NotSupportedException($"Provider kind '{provider.ProviderKind}' is not supported by the OpenAI chat client factory."),
            };
        }

        public static Result<ChatClient, IApplicationError> FromAgentDefinition(AgentDefinition agentDefinition, HttpClient? httpClient = null) =>
            agentDefinition
            .IsValidDefinition()
            .Map(() => FromProvider(agentDefinition.Provider, agentDefinition.Model, httpClient));
    }

    extension(IChatClient chatClient)
    {
        public AIAgent CreateAgent<T>(
            string instruction,
            string? name = null,
            string? description = null,
            IEnumerable<AIContextProvider>? aIContextProviders = null) =>
            chatClient.AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = name,
                    Description = description,
                    ChatOptions = new()
                    {
                        Instructions = instruction,
                        ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(T))),
                    },
                    AIContextProviders = aIContextProviders
                }
            )
            .AsBuilder()
            .Build();
    }

    private static Uri? TryCreateEndpoint(string? baseUrl)
    {
        string normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.Trim();
        if (normalizedBaseUrl.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out Uri? endpoint))
        {
            return endpoint;
        }

        throw new InvalidOperationException($"Invalid provider base URL: {baseUrl}");
    }

    private static Uri TryCreateRequiredEndpoint(string? baseUrl)
        => TryCreateEndpoint(baseUrl)
            ?? throw new InvalidOperationException("A provider base URL is required for this provider kind.");
}
