using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sedgewright.Infras.Storage;

public interface IRepository<T> where T : class
{
    void Create(T item);
    Task<T?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAll(CancellationToken cancellationToken = default);
    void Update(T item);
    void Delete(T item);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
