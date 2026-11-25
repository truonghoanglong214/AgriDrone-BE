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
        var activityPeriod = period with { EndUtc = cutoff };
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH legacy_orders AS (
                    SELECT id, tenant_id, farm_id, original_final_price, status,
                        booked_mapping_price_per_pole IS NOT NULL AND booked_plant_health_price_per_pole IS NOT NULL AS is_combo
                    FROM survey.survey_orders
                    WHERE scope = 'LEGACY' AND post_flight_settlement AND booked_service_type = 'PLANT_HEALTH'
                )
                SELECT h.sequence, h.source, h.source_id, h.order_id, h.recorded_at,
                    (CASE WHEN legacy.is_combo THEN h.data || '{"booked_service_code":"COMBO"}'::jsonb ELSE h.data END)::text,
                    h.is_deleted, c.started_at
                FROM survey.financial_report_coverage c
                LEFT JOIN survey.financial_report_history h ON h.recorded_at < @cutoff OR (
                    h.source IN ('original_service_publication','survey_funding_receipts','survey_payments',
                        'survey_refund_obligations','quality_refund_cases','survey_funding_reversals','cancellation_retentions')
                    AND COALESCE(h.data->>'published_at', h.data->>'received_at', h.data->>'confirmed_at',
                        h.data->>'completed_at', h.data->>'transferred_at', h.data->>'occurred_at',
                        h.data->>'recorded_at')::timestamptz < @cutoff)
                LEFT JOIN legacy_orders legacy ON h.source = 'survey_orders' AND legacy.id = h.order_id
                WHERE c.id = 1

                UNION ALL

                -- Legacy Plant Health can publish before final pricing. Use its immutable publication and original invoice.
                SELECT 0::bigint, 'legacy_service_publication', p.id, legacy.id, p.published_at,
                    jsonb_build_object('published_at', p.published_at, 'amount', legacy.original_final_price)::text,
                    FALSE, c.started_at
                FROM survey.financial_report_coverage c
                CROSS JOIN legacy_orders legacy
                JOIN survey.survey_results r ON r.survey_order_id = legacy.id
                    AND r.tenant_id = legacy.tenant_id AND r.farm_id = legacy.farm_id
                JOIN survey.plant_health_publications p ON p.survey_result_id = r.id
                    AND p.tenant_id = legacy.tenant_id AND p.farm_id = legacy.farm_id
                WHERE c.id = 1 AND legacy.status = 'COMPLETED' AND legacy.original_final_price > 0
                    AND p.published_at < @cutoff
                ORDER BY 1
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
                .Select(group => FinancialReportCalculator.Calculate(group.ToArray(), activityPeriod, available))
                .Where(x => x is not null).Select(x => x!)
                .Where(x => service is null || x.Service == service)
                .OrderBy(x => x.OrderNumber, StringComparer.Ordinal).ThenBy(x => x.OrderId).ToArray();
            return new(year, month, service, "BR-FIN-v3.2", period.StartUtc, period.EndUtc, now,
                available, period.StartUtc >= available && lines.All(x => !x.NeedsReconciliation), lines);
        }
        finally { if (close) await connection.CloseAsync(); }
    }

    public async Task AuditExportAsync(MonthlyFinancialReport report, Guid actorId, Guid correlationId, CancellationToken cancellationToken)
    {
        using var data = JsonSerializer.SerializeToDocument(new { report.Year, report.Month, report.Service, report.SnapshotId,
            report.FormulaVersion, report.ReportFingerprint, report.AsOfUtc, report.GeneratedAt,
            report.HistoricalBalancesComplete, report.PublishedServiceValue, report.NetRevenue,
            report.NetServiceRevenue, report.NetCash });
        audit.AddSystemAdminAction(db, actorId, correlationId, "MonthlyFinancialReport", Guid.NewGuid(),
            "Export", null, data, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
}
