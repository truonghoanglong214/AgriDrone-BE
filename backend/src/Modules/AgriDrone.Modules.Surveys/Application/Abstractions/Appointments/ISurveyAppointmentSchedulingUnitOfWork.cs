using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;

public interface ISurveyAppointmentSchedulingUnitOfWork
    : IUnitOfWork, IAuditLogSink
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
