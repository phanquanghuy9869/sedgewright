using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

public sealed class ToolDefinitionRepository(SedgewrightDbContext dbContext) : IToolDefinitionRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    public Task<List<ToolDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.ToolDefinitions
            .AsNoTracking()
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

    public Task<ToolDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.ToolDefinitions
            .Include(definition => definition.SequentialOrchestration)
            .Include(definition => definition.ConcurrentOrchestration)
            .Include(definition => definition.GroupChatOrchestration)
            .Include(definition => definition.MagenticOrchestration)
            .Include(definition => definition.HandoffOrchestration)
            .FirstOrDefaultAsync(definition => definition.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<List<string>> GetAssignedAgentNamesAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.AgentTools
            .AsNoTracking()
            .Where(assignment => assignment.ToolDefinitionId == id)
            .Select(assignment => assignment.AgentDefinition.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        return _dbContext.ToolDefinitions.AnyAsync(
            definition => definition.Name == normalizedName && (!excludeId.HasValue || definition.Id != excludeId.Value),
            cancellationToken);
    }

    public void Create(ToolDefinition definition)
        => _dbContext.ToolDefinitions.Add(definition);

    public void Update(ToolDefinition definition)
        => _dbContext.ToolDefinitions.Update(definition);

    public void Delete(ToolDefinition definition)
        => _dbContext.ToolDefinitions.Remove(definition);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
