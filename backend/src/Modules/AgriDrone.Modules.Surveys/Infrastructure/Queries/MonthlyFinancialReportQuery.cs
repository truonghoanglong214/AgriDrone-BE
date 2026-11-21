using System.Data;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Features.Reports;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class MonthlyFinancialReportQuery(SurveysDbContext db, IAuditWriter audit) : IMonthlyFinancialReportQuery
{
    public async Task<MonthlyFinancialReport> GetAsync(int year, int month, string? service, CancellationToken cancellationToken)
    {
        var period = FinancialReportPeriod.Create(year, month);
        var now = DateTimeOffset.UtcNow;
        var cutoff = period.EndUtc < now ? period.EndUtc : now;
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT h.sequence, h.source, h.source_id, h.order_id, h.recorded_at, h.data::text, h.is_deleted,
                    c.started_at
                FROM survey.financial_report_coverage c
                LEFT JOIN survey.financial_report_history h ON h.recorded_at < @cutoff OR (
                    h.source IN ('original_service_publication','survey_funding_receipts','survey_payments',
                        'survey_refund_obligations','quality_refund_cases','survey_funding_reversals','cancellation_retentions')
                    AND COALESCE(h.data->>'published_at', h.data->>'received_at', h.data->>'confirmed_at',
                        h.data->>'completed_at', h.data->>'transferred_at', h.data->>'occurred_at',
                        h.data->>'recorded_at')::timestamptz < @cutoff)
                WHERE c.id = 1 ORDER BY h.sequence
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "cutoff"; parameter.Value = cutoff.UtcDateTime;
            command.Parameters.Add(parameter);
            var rows = new List<FinancialHistoryRow>();
            DateTimeOffset? coverage = null;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    coverage = reader.GetFieldValue<DateTimeOffset>(7);
                    if (reader.IsDBNull(0)) continue;
                    using var document = JsonDocument.Parse(reader.GetString(5));
                    rows.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3),
                        reader.GetFieldValue<DateTimeOffset>(4), document.RootElement.Clone(), reader.GetBoolean(6)));
                }
            }
            var available = coverage ?? throw new InvalidOperationException("Financial report coverage is not initialized.");
            var lines = rows.GroupBy(x => x.OrderId)
                .Select(group => FinancialReportCalculator.Calculate(group.ToArray(), period, available))
                .Where(x => x is not null).Select(x => x!)
                .Where(x => service is null || x.Service == service)
                .OrderBy(x => x.OrderNumber, StringComparer.Ordinal).ToArray();
            return new(year, month, service, "BR-FIN-v3.1", period.StartUtc, period.EndUtc, now,
                available, period.StartUtc >= available && period.EndUtc <= now && lines.All(x => !x.NeedsReconciliation), lines);
        }
        finally { if (close) await connection.CloseAsync(); }
    }

    public async Task AuditExportAsync(MonthlyFinancialReport report, Guid actorId, Guid correlationId, CancellationToken cancellationToken)
    {
        using var data = JsonSerializer.SerializeToDocument(new { report.Year, report.Month, report.Service,
            report.FormulaVersion, report.ReportFingerprint, report.GeneratedAt, report.HistoricalBalancesComplete,
            report.PublishedServiceValue, report.NetServiceRevenue, report.NetCash });
        audit.AddSystemAdminAction(db, actorId, correlationId, "MonthlyFinancialReport", Guid.NewGuid(),
            "Export", null, data, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
}
