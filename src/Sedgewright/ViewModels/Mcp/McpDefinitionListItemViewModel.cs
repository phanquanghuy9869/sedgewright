using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Mcp;

/// <summary>
/// Represents one MCP row in the list.
/// </summary>
public sealed class McpDefinitionListItemViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates a row view-model.
    /// </summary>
    public McpDefinitionListItemViewModel(
        McpDefinitionSnapshot snapshot,
        Func<Task> editAsync,
        Func<Task> deleteAsync)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        Description = snapshot.Description;
        IsReadOnly = snapshot.IsReadOnly;
        IsStdio = snapshot.IsStdio;
        TransportLabel = snapshot.IsStdio ? "stdio" : "HTTP/SSE";
        AttachmentCount = snapshot.AttachmentCount;
        AttachmentSummary = BuildAttachmentSummary(snapshot.AttachmentCount);
        ConfigSummary = BuildConfigSummary(snapshot);
        CanDelete = snapshot.AttachmentCount == 0;

        EditCommand = new AsyncRelayCommand(editAsync);
        DeleteCommand = new AsyncRelayCommand(deleteAsync, () => CanDelete);
    }

    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    public bool IsReadOnly { get; }

    public bool IsStdio { get; }

    public string TransportLabel { get; }

    public int AttachmentCount { get; }

    public string AttachmentSummary { get; }

    public string ConfigSummary { get; }

    public bool CanDelete { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    private static string BuildAttachmentSummary(int attachmentCount)
        => attachmentCount == 0
            ? "Not attached to any agents"
            : attachmentCount == 1
                ? "Attached to 1 agent"
                : $"Attached to {attachmentCount} agents";

    private static string BuildConfigSummary(McpDefinitionSnapshot snapshot)
    {
        if (snapshot.IsStdio)
        {
            if (!TryParseJsonConfig<StdioConfig>(snapshot.ConfigJson, out StdioConfig? config) || config is null)
            {
                return "Invalid stdio config";
            }

            if (string.IsNullOrWhiteSpace(config.Command))
            {
                return "stdio command not set";
            }

            if (config.Arguments.Length == 0)
            {
                return config.Command;
            }

            string arguments = string.Join(" ", config.Arguments.Take(3));
            if (config.Arguments.Length > 3)
            {
                arguments += " ...";
            }

            return $"{config.Command} {arguments}";
        }

        if (!TryParseJsonConfig<HttpTransportConfig>(snapshot.ConfigJson, out HttpTransportConfig? httpConfig) || httpConfig is null)
        {
            return "Invalid HTTP/SSE config";
        }

        if (httpConfig.Headers.Count == 0)
        {
            return httpConfig.Endpoint;
        }

        return $"{httpConfig.Endpoint} ({httpConfig.Headers.Count} headers)";
    }

    private static bool TryParseJsonConfig<T>(string json, out T? config)
    {
        try
        {
            config = JsonSerializer.Deserialize<T>(json, ReadOptions);
            return config is not null;
        }
        catch (JsonException)
        {
            config = default;
            return false;
        }
    }
}
