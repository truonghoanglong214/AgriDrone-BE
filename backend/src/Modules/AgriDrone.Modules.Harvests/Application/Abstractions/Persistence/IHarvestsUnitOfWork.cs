using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedInfrastructure.Auditing;

namespace AgriDrone.Modules.Harvests.Application.Abstractions.Persistence
{
    internal interface IHarvestsUnitOfWork : IUnitOfWork, IAuditLogSink
    {
        Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default);
    }
}
