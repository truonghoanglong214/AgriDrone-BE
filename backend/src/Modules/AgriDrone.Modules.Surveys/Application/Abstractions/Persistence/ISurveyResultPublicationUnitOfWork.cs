using AgriDrone.SharedKernel.Application.Abstractions;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

public interface ISurveyResultPublicationUnitOfWork : IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
