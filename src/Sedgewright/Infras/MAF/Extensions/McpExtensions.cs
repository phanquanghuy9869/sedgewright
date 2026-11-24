using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using ModelContextProtocol.Client;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class MCPExtensions
{
    extension(IMcpClientTransport clientTransport)
    {
        public static Result<IMcpClientTransport, IApplicationError> From(McpDefinition mcpDefinition)
        {
            try
            {
                if (mcpDefinition.IsStdio)
                {
                    StdioConfig? config = JsonSerializer.Deserialize<StdioConfig>(mcpDefinition.ConfigJson);
                    if (config is null)
                        return new GenericError(["Failed to deserialize MCPDefinition config."]);
                    return config;
                }
                else
                {
                    HttpTransportConfig? config = JsonSerializer.Deserialize<HttpTransportConfig>(mcpDefinition.ConfigJson);
                    if (config is null)
                        return new GenericError(["Failed to deserialize MCPDefinition config."]);
                    return config;
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentNullException || ex is NotSupportedException)
            {
                return new ExceptionError(["Failed to deserialize MCPDefinition config."], ex);
            }
        }
    }

    extension(McpClient)
    {
        public static Task<McpClient> FromStdio(StdioConfig stdioConfig) =>
            McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Name = stdioConfig.Name,
                Command = stdioConfig.Command,
                Arguments = stdioConfig.Arguments,
                ShutdownTimeout = TimeSpan.FromMinutes(5)
            }));

        public static Task<McpClient> FromHttp(HttpTransportConfig httpConfig)
        {
            HttpClientTransportOptions transportOption = new()
            {
                Name = httpConfig.Name,
                Endpoint = new Uri(httpConfig.Endpoint)
            };
            if (httpConfig.Headers is not null && httpConfig.Headers.Count > 0)
            {
                transportOption.AdditionalHeaders = httpConfig.Headers;
            }
            HttpClientTransport transport = new(transportOption);
            return McpClient.CreateAsync(transport);
        }

        public static async Task<Result<McpClient, IApplicationError>> FromTransport(IMcpClientTransport transport)
        {
            try
            {
                Task<McpClient> mcpTask = transport switch
                {
                    StdioConfig stdio => FromStdio(stdio),
                    HttpTransportConfig http => FromHttp(http),
                    _ => throw new NotSupportedException($"Unsupported transport type: {transport.GetType().Name}")
                };
                McpClient mcpClient = await mcpTask.ConfigureAwait(false);
                return mcpClient;
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is HttpRequestException)
            {
                return new ExceptionError(["Failed to create McpClient from transport."], ex);
            }
        }

        public static Task<Result<McpClient, IApplicationError>> FromMcpDefinition(McpDefinition mcpDefinition) =>
            IMcpClientTransport.From(mcpDefinition).Bind(FromTransport);

        public static async Task<Result<McpClient, IApplicationError>[]> FromAgentDefinition(AgentDefinition agentDefinition)
        {
            IEnumerable<Task<Result<McpClient, IApplicationError>>> createMcpClientTasks = agentDefinition.McpDefinitions.Select(McpClient.FromMcpDefinition);
            Result<McpClient, IApplicationError>[] mcpClientResults = await Task.WhenAll(createMcpClientTasks).ConfigureAwait(false);
            return mcpClientResults;
        }
    }
}
