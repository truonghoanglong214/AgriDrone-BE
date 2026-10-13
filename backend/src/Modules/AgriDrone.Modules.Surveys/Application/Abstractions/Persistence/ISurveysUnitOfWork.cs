using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

internal interface ISurveysUnitOfWork : IUnitOfWork, IAuditLogSink
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
