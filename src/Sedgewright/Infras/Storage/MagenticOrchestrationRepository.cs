using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists Magentic orchestration rows and their participant graphs.
/// </summary>
public sealed class MagenticOrchestrationRepository(SedgewrightDbContext dbContext)
    : GenericRepository<MagenticOrchestration>(dbContext), IMagenticOrchestrationRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<MagenticOrchestration>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.MagenticOrchestrations
            .AsNoTracking()
            .Include(orchestration => orchestration.Participants)
            .OrderBy(orchestration => orchestration.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public new async Task<MagenticOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        MagenticOrchestration? orchestration = await _dbContext.MagenticOrchestrations
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
                [orchestration.ManagerAgentDefinitionId, .. orchestration.Participants.Select(participant => participant.AgentDefinitionId)],
                cancellationToken)
            .ConfigureAwait(false);

        orchestration.ManagerAgentDefinition = agents[orchestration.ManagerAgentDefinitionId];
        foreach (MagenticOrchestrationParticipant participant in orchestration.Participants)
        {
            participant.AgentDefinition = agents[participant.AgentDefinitionId];
        }

        return orchestration;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();

        return _dbContext.MagenticOrchestrations.AnyAsync(
            orchestration => orchestration.Name == normalizedName && (!excludeId.HasValue || orchestration.Id != excludeId.Value),
            cancellationToken);
    }
}
