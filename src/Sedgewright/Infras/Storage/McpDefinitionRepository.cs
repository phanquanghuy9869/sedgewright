using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists MCP definitions and their agent attachment state.
/// </summary>
public sealed class McpDefinitionRepository(SedgewrightDbContext dbContext) : IMcpDefinitionRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<McpDefinitionSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.MCPDefinitions
            .AsNoTracking()
            .OrderBy(definition => definition.Name)
            .Select(definition => new McpDefinitionSnapshot(
                definition.Id,
                definition.Name,
                definition.Description,
                definition.IsReadOnly,
                definition.IsStdio,
                definition.ConfigJson,
                _dbContext.AgentMCPAttachments.Count(attachment => attachment.McpId == definition.Id)))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<McpDefinition?> GetAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.MCPDefinitions.FirstOrDefaultAsync(definition => definition.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        return _dbContext.MCPDefinitions.AnyAsync(
            definition => definition.Name == normalizedName && (!excludeId.HasValue || definition.Id != excludeId.Value),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasAttachmentsAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.AgentMCPAttachments.AnyAsync(attachment => attachment.McpId == id, cancellationToken);

    /// <inheritdoc />
    public void Create(McpDefinition definition)
        => _dbContext.MCPDefinitions.Add(definition);

    /// <inheritdoc />
    public void Update(McpDefinition definition)
        => _dbContext.MCPDefinitions.Update(definition);

    /// <inheritdoc />
    public void Delete(McpDefinition definition)
        => _dbContext.MCPDefinitions.Remove(definition);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
