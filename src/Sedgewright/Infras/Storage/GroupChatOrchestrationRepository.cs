using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists group chat orchestration rows and their participant graphs.
/// </summary>
public sealed class GroupChatOrchestrationRepository(SedgewrightDbContext dbContext)
    : GenericRepository<GroupChatOrchestration>(dbContext), IGroupChatOrchestrationRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<GroupChatOrchestration>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.GroupChatOrchestrations
            .AsNoTracking()
            .Include(orchestration => orchestration.Participants)
            .OrderBy(orchestration => orchestration.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public new async Task<GroupChatOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        GroupChatOrchestration? orchestration = await _dbContext.GroupChatOrchestrations
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

        foreach (GroupChatOrchestrationParticipant participant in orchestration.Participants)
        {
            participant.AgentDefinition = agents[participant.AgentDefinitionId];
        }

        return orchestration;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();

        return _dbContext.GroupChatOrchestrations.AnyAsync(
            orchestration => orchestration.Name == normalizedName && (!excludeId.HasValue || orchestration.Id != excludeId.Value),
            cancellationToken);
    }
}
