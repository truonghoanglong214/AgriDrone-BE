namespace AgriDrone.Modules.Surveys.Application.Features.Reports;

public interface IMonthlyFinancialReportQuery
{
    Task<MonthlyFinancialReport> GetAsync(int year, int month, string? service, CancellationToken cancellationToken);
    Task AuditExportAsync(MonthlyFinancialReport report, Guid actorId, Guid correlationId, CancellationToken cancellationToken);
}
