using AgriDrone.SharedKernel.Application.Abstractions;

namespace AgriDrone.Database.Mapping;

public interface IMappingPublicationUnitOfWork : IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
