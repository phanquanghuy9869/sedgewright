using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists sequential orchestration rows and their step graphs.
/// </summary>
public sealed class SequentialOrchestrationRepository(SedgewrightDbContext dbContext)
    : GenericRepository<SequentialOrchestration>(dbContext), ISequentialOrchestrationRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<List<SequentialOrchestration>> GetAllAsync(CancellationToken cancellationToken = default)
        => _dbContext.SequentialOrchestrations
            .AsNoTracking()
            .Include(orchestration => orchestration.Steps)
            .OrderBy(orchestration => orchestration.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public new async Task<SequentialOrchestration?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        SequentialOrchestration? orchestration = await _dbContext.SequentialOrchestrations
            .Include(orchestration => orchestration.Steps)
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
                orchestration.Steps.Select(step => step.AgentDefinitionId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (SequentialOrchestrationAgent step in orchestration.Steps)
        {
            step.AgentDefinition = agents[step.AgentDefinitionId];
        }

        return orchestration;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        return _dbContext.SequentialOrchestrations.AnyAsync(
            orchestration => orchestration.Name == normalizedName && (!excludeId.HasValue || orchestration.Id != excludeId.Value),
            cancellationToken);
    }
}
