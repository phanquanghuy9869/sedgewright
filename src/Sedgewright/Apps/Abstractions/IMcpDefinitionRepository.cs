using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Reads and writes MCP definitions from the local UI database.
/// </summary>
public interface IMcpDefinitionRepository
{
    /// <summary>
    /// Gets all MCP definitions with their attachment counts.
    /// </summary>
    Task<List<McpDefinitionSnapshot>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one MCP definition by identifier.
    /// </summary>
    Task<McpDefinition?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a name already exists, optionally excluding one identifier.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the MCP is attached to any agents.
    /// </summary>
    Task<bool> HasAttachmentsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new MCP definition.
    /// </summary>
    void Create(McpDefinition definition);

    /// <summary>
    /// Updates an existing MCP definition.
    /// </summary>
    void Update(McpDefinition definition);

    /// <summary>
    /// Removes an MCP definition.
    /// </summary>
    void Delete(McpDefinition definition);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
