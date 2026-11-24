using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Exposes shared application state that can be reused across unrelated pages and features.
/// </summary>
public interface ISharedState : IAsyncDisposable
{
    /// <summary>
    /// Gets the shared application settings loaded from persistent storage.
    /// </summary>
    ApplicationSettings? ApplicationSettings { get; }

    /// <summary>
    /// Raised on the UI thread after the shared application settings are replaced.
    /// </summary>
    event EventHandler? ApplicationSettingsChanged;

    /// <summary>
    /// Gets the shared agent collection currently used by the chat picker.
    /// </summary>
    ObservableCollection<AgentDefinitionSnapshot> Agents { get; }
    Maybe<AgentRuntime> AgentRuntime { get; }

    /// <summary>
    /// Gets the shared agent session collection currently used by the session chooser.
    /// </summary>
    ObservableCollection<AgentSessionSnapshot> AgentSessions { get; }

    /// <summary>
    /// Reloads the shared agent collection from persistent storage.
    /// </summary>
    Task RefreshAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the shared agent session collection from caller-provided snapshots.
    /// </summary>
    void SetAgentSessions(IEnumerable<AgentSessionSnapshot> agentSessions);

    /// <summary>
    /// Stores a new current agent runtime and disposes the previous runtime.
    /// </summary>
    Task SetAgentRuntimeAsync(AgentRuntime agentRuntime);

    /// <summary>
    /// Gets the shared orchestration collection currently used by the chat picker.
    /// </summary>
    ObservableCollection<OrchestrationListItemSnapshot> Orchestrations { get; }

    /// <summary>
    /// Reloads the shared orchestration collection from persistent storage.
    /// </summary>
    Task RefreshOrchestrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the shared provider collection currently used by settings and editors.
    /// </summary>
    ObservableCollection<LLMProvider> Providers { get; }

    /// <summary>
    /// Reloads the shared provider collection from persistent storage.
    /// </summary>
    Task RefreshProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the shared application settings from persistent storage.
    /// </summary>
    Task RefreshApplicationSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves and publishes the shared application settings.
    /// </summary>
    Task SaveApplicationSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
