using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists concurrent orchestration rows and their participant graphs.
/// </summary>
public sealed class ConcurrentOrchestrationRepository(SedgewrightDbContext dbContext)
    : GenericRepository<ConcurrentOrchestration>(dbContext), IConcurrentOrchestrationRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<ConcurrentOrchestration>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.ConcurrentOrchestrations
            .AsNoTracking()
            .Include(orchestration => orchestration.Participants)
            .OrderBy(orchestration => orchestration.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public new async Task<ConcurrentOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        ConcurrentOrchestration? orchestration = await _dbContext.ConcurrentOrchestrations
            .Include(orchestration => orchestration.Participants)
            .AsSplitQuery()
            .FirstOrDefaultAsync(orchestration => orchestration.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (orchestration is null)
        {
            return null;
        }

        Dictionary<int, AgentDefinition> agents = await AgentDefinitionQueryExtensions
            .LoadRuntimeGraphAsync(
                _dbContext,
                orchestration.Participants.Select(participant => participant.AgentDefinitionId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (ConcurrentOrchestrationParticipant participant in orchestration.Participants)
        {
            participant.AgentDefinition = agents[participant.AgentDefinitionId];
        }

        return orchestration;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();

        return _dbContext.ConcurrentOrchestrations.AnyAsync(
            orchestration => orchestration.Name == normalizedName && (!excludeId.HasValue || orchestration.Id != excludeId.Value),
            cancellationToken);
    }
}
