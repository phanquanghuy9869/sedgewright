using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes agent definitions from the local UI database.
/// </summary>
public interface IAgentDefinitionRepository
{
    /// <summary>
    /// Gets all agent definitions with their usage summaries.
    /// </summary>
    Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one agent definition by identifier with its attached graph.
    /// </summary>
    Task<AgentDefinition?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the agent is referenced by any workflow surfaces that block deletion.
    /// </summary>
    Task<bool> HasReferencesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent definition.
    /// </summary>
    void Create(AgentDefinition definition);

    /// <summary>
    /// Updates an existing agent definition.
    /// </summary>
    void Update(AgentDefinition definition);

    /// <summary>
    /// Removes an agent definition.
    /// </summary>
    void Delete(AgentDefinition definition);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
