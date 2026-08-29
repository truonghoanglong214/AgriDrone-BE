using AgriDrone.SharedKernel.Application.Abstractions;
namespace AgriDrone.Modules.Identity.Application.Abstractions.Persistence
{
    internal interface IIdentityUnitOfWork : IUnitOfWork
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    }
}
