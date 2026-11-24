using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists handoff orchestration rows and their participant and route graphs.
/// </summary>
public sealed class HandoffOrchestrationRepository(SedgewrightDbContext dbContext)
    : GenericRepository<HandoffOrchestration>(dbContext), IHandoffOrchestrationRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<HandoffOrchestration>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.HandoffOrchestrations
            .AsNoTracking()
            .Include(orchestration => orchestration.Participants)
            .Include(orchestration => orchestration.Routes)
            .OrderBy(orchestration => orchestration.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public new async Task<HandoffOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        HandoffOrchestration? orchestration = await _dbContext.HandoffOrchestrations
            .Include(orchestration => orchestration.Participants)
            .Include(orchestration => orchestration.Routes)
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
                [
                    orchestration.StartAgentDefinitionId,
                    .. orchestration.Participants.Select(participant => participant.AgentDefinitionId),
                    .. orchestration.Routes.SelectMany(route => new[]
                    {
                        route.SourceAgentDefinitionId,
                        route.TargetAgentDefinitionId,
                    }),
                ],
                cancellationToken)
            .ConfigureAwait(false);

        orchestration.StartAgentDefinition = agents[orchestration.StartAgentDefinitionId];
        foreach (HandoffOrchestrationParticipant participant in orchestration.Participants)
        {
            participant.AgentDefinition = agents[participant.AgentDefinitionId];
        }

        foreach (HandoffOrchestrationRoute route in orchestration.Routes)
        {
            route.SourceAgentDefinition = agents[route.SourceAgentDefinitionId];
            route.TargetAgentDefinition = agents[route.TargetAgentDefinitionId];
        }

        return orchestration;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();

        return _dbContext.HandoffOrchestrations.AnyAsync(
            orchestration => orchestration.Name == normalizedName && (!excludeId.HasValue || orchestration.Id != excludeId.Value),
            cancellationToken);
    }
}
