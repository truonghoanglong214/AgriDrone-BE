using AgriDrone.Modules.Surveys.Application.Features.Reports;
using AgriDrone.Api.Reporting;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/system-admin/reports/revenue")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class V3SystemAdminRevenueReportsController(
    IMonthlyFinancialReportQuery reports,
    IExecutionContext executionContext,
    RevenueReportSnapshots snapshots) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int year, [FromQuery] int month, [FromQuery] string? service, CancellationToken cancellationToken)
    {
        if (!Valid(year, month, service)) return BadRequest("Invalid year/month/service filter.");
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actor || actor == Guid.Empty) return Forbid();
        var report = await reports.GetAsync(year, month, Normalize(service), cancellationToken);
        return Ok(snapshots.Save(actor, report));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int year, [FromQuery] int month, [FromQuery] string? service,
        [FromQuery] Guid? snapshotId, CancellationToken cancellationToken)
    {
        if (!Valid(year, month, service)) return BadRequest("Invalid year/month/service filter.");
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actor || actor == Guid.Empty || executionContext.CorrelationId == Guid.Empty) return Forbid();
        var normalizedService = Normalize(service);
        var report = snapshotId is Guid id
            ? snapshots.Find(actor, id)
            : await reports.GetAsync(year, month, normalizedService, cancellationToken);
        if (report is null || report.Year != year || report.Month != month || report.Service != normalizedService)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict,
                detail: "Bản báo cáo đang xem đã hết hiệu lực. Vui lòng tải lại báo cáo trước khi xuất Excel.");
        }
        var content = RevenueWorkbook.Create(report);
        await reports.AuditExportAsync(report, actor, executionContext.CorrelationId, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"revenue-{year:D4}-{month:D2}.xlsx");
    }
    private static string? Normalize(string? service) => string.IsNullOrWhiteSpace(service) ? null : service.Trim().ToUpperInvariant();
    private static bool Valid(int year, int month, string? service) => year is >= 2000 and <= 9998 && month is >= 1 and <= 12 && Normalize(service) is null or "MAP" or "COMBO";
}
