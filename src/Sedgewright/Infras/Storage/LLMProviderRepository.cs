using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists LLM provider selections to a local SQLite database.
/// </summary>
public sealed class LLMProviderRepository(SedgewrightDbContext dbContext)
    : GenericRepository<LLMProvider>(dbContext), ILLMProviderRepository
{
}
