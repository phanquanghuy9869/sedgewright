using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists agent session records and their serialized runtime payloads.
/// </summary>
public sealed class AgentSessionRepository(SedgewrightDbContext dbContext) : IAgentSessionRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<AgentSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.AgentSessions
            .AsNoTracking()
            .Include(session => session.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .OrderByDescending(session => session.LastUsedAtUtc)
            .ThenByDescending(session => session.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<AgentSessionRecord?> GetAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.AgentSessions
            .Include(session => session.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public void Create(AgentSessionRecord session)
        => _dbContext.AgentSessions.Add(session);

    /// <inheritdoc />
    public void Update(AgentSessionRecord session)
        => _dbContext.AgentSessions.Update(session);

    /// <inheritdoc />
    public void Delete(AgentSessionRecord session)
        => _dbContext.AgentSessions.Remove(session);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
