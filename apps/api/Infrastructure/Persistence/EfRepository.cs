using System.Linq.Expressions;
using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Infrastructure.Persistence;

public class EfRepository<T>(JobPilotDbContext dbContext) : IRepository<T> where T : class
{
    private readonly DbSet<T> _dbSet = dbContext.Set<T>();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbSet.FindAsync([id], cancellationToken).AsTask();

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate is null ? _dbSet.ToListAsync(cancellationToken) : _dbSet.Where(predicate).ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(predicate, cancellationToken);

    public void Add(T entity) => _dbSet.Add(entity);
    public void Update(T entity) => _dbSet.Update(entity);
    public void Remove(T entity) => _dbSet.Remove(entity);
    public IQueryable<T> Query() => _dbSet.AsQueryable();
}
