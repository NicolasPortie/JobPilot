using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace JobPilot.Api.Infrastructure.Persistence;

public sealed class EfUnitOfWork(JobPilotDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.BeginTransactionAsync(cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.CommitTransactionAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.RollbackTransactionAsync(cancellationToken);
}
