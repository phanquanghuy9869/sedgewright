using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists agent definitions and their attached graph.
/// </summary>
public sealed class AgentDefinitionRepository(SedgewrightDbContext dbContext) : IAgentDefinitionRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.AgentDefinitions
            .AsNoTracking()
            .Include(definition => definition.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .Include(definition => definition.McpAttachments)
            .Include(definition => definition.Skills)
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<AgentDefinition?> GetAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.AgentDefinitions
            .IncludeRuntimeGraph()
            .AsSplitQuery()
            .FirstOrDefaultAsync(definition => definition.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        return _dbContext.AgentDefinitions.AnyAsync(
            definition => definition.Name == normalizedName && (!excludeId.HasValue || definition.Id != excludeId.Value),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasReferencesAsync(int id, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.ConcurrentOrchestrationParticipants.AnyAsync(participant => participant.AgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.GroupChatOrchestrationParticipants.AnyAsync(participant => participant.AgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.HandoffOrchestrationParticipants.AnyAsync(participant => participant.AgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.HandoffOrchestrations.AnyAsync(orchestration => orchestration.StartAgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.HandoffOrchestrationRoutes.AnyAsync(route => route.SourceAgentDefinitionId == id || route.TargetAgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.MagenticOrchestrationParticipants.AnyAsync(participant => participant.AgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        if (await _dbContext.MagenticOrchestrations.AnyAsync(orchestration => orchestration.ManagerAgentDefinitionId == id, cancellationToken))
        {
            return true;
        }

        return await _dbContext.SequentialOrchestrationAgents.AnyAsync(step => step.AgentDefinitionId == id, cancellationToken);
    }

    /// <inheritdoc />
    public void Create(AgentDefinition definition)
        => _dbContext.AgentDefinitions.Add(definition);

    /// <inheritdoc />
    public void Update(AgentDefinition definition)
        => _dbContext.AgentDefinitions.Update(definition);

    /// <inheritdoc />
    public void Delete(AgentDefinition definition)
        => _dbContext.AgentDefinitions.Remove(definition);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
