using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalUnitOfWork(
    SurveyApprovalDbContext context)
    : ISurveyApprovalUnitOfWork
{
    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        context.ExecuteInTransactionAsync(operation, cancellationToken);

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Survey approval can only persist through ExecuteInTransactionAsync; " +
            "application code must not create independent save boundaries.");

    public void AddAuditLog(AuditLog auditLog) =>
        context.AddAuditLog(auditLog);
}
