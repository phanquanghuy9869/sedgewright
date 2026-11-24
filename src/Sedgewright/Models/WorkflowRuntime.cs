using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Sedgewright.Models;

/// <summary>
/// Owns a workflow and the agent runtimes used to build it.
/// </summary>
public sealed class WorkflowRuntime : IAsyncDisposable
{
    private readonly IReadOnlyList<AgentRuntime> _agentRuntimes;
    private int _disposed;

    /// <summary>
    /// Creates a workflow runtime.
    /// </summary>
    public WorkflowRuntime(
        Workflow workflow,
        IEnumerable<AgentRuntime> agentRuntimes,
        string name,
        string? description,
        OrchestrationKind kind,
        string? toolDescription = null,
        IReadOnlyDictionary<string, string>? executorDisplayNames = null,
        string? managerExecutorId = null)
    {
        Workflow = workflow;
        Name = name;
        Description = description;
        Kind = kind;
        ToolDescription = toolDescription;
        ExecutorDisplayNames = executorDisplayNames is null
            ? null
            : new Dictionary<string, string>(executorDisplayNames, StringComparer.Ordinal);
        ManagerExecutorId = managerExecutorId;
        _agentRuntimes = agentRuntimes.ToArray();
    }

    /// <summary>
    /// Gets the built workflow.
    /// </summary>
    public Workflow Workflow { get; }

    /// <summary>
    /// Gets the orchestration name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the orchestration description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the kind of orchestration represented by the workflow.
    /// </summary>
    public OrchestrationKind Kind { get; }

    /// <summary>
    /// Gets the tool-specific description override.
    /// </summary>
    public string? ToolDescription { get; }

    /// <summary>
    /// Gets the configured display names keyed by workflow executor identifier, when available.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ExecutorDisplayNames { get; }

    /// <summary>
    /// Gets the manager's workflow executor identifier, when the runtime represents a Magentic orchestration.
    /// </summary>
    public string? ManagerExecutorId { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (AgentRuntime agentRuntime in _agentRuntimes.Reverse())
        {
            await agentRuntime.DisposeAsync().ConfigureAwait(false);
        }
    }
}
