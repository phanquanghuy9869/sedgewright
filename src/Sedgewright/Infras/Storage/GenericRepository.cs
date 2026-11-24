using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Sedgewright.Infras.Storage;

public class GenericRepository<T>(SedgewrightDbContext dbContext) : IRepository<T> where T : class
{
    private readonly SedgewrightDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public void Create(T item)
    {
        _dbContext.Set<T>().Add(item);
    }

    public Task<T?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        _dbContext.Set<T>().FindAsync([id], cancellationToken).AsTask();

    public Task<List<T>> GetAll(CancellationToken cancellationToken = default) =>
        _dbContext.Set<T>().ToListAsync(cancellationToken);

    public void Update(T item)
    {
        _dbContext.Set<T>().Update(item);
    }

    public void Delete(T item)
    {
        _dbContext.Set<T>().Remove(item);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
