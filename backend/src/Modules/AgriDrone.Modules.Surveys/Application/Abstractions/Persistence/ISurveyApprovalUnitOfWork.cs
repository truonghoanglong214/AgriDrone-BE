using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedInfrastructure.Auditing;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

public interface ISurveyApprovalUnitOfWork : IUnitOfWork, IAuditLogSink
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
