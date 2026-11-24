using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

public interface IToolDefinitionRepository
{
    Task<List<ToolDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ToolDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of agents assigned to the specified tool definition.
    /// </summary>
    Task<List<string>> GetAssignedAgentNamesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    void Create(ToolDefinition definition);

    void Update(ToolDefinition definition);

    void Delete(ToolDefinition definition);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
