using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Entities;

namespace Sedgewright.Apps.Abstractions;

public interface ILLMProviderRepository
{
    public Task<List<LLMProvider>> GetAll(CancellationToken cancellationToken = default);
}
