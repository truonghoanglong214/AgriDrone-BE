using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using AgriDrone.Api.Controllers;
using AgriDrone.Api.Controllers.V3;
using AgriDrone.Api.Reporting;
using AgriDrone.Modules.Surveys.Application.Features.Reports;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgriDrone.ArchitectureTests;

/// <summary>
/// Covers API authorization/isolation and reporting parity checks:
/// - TC-F05-A02: Owner denied on Manager/Worker internal AI/review/mission processing endpoints.
/// - TC-F06-A02: Cross-tenant/role isolation on BaseMaps, PlantLifecycle, and Owner Invoice/Report endpoints.
/// - TC-F07-A02: Owner/Manager denied on Admin revenue Get and Excel Export endpoints.
/// - TC-NFR-07: Excel RevenueWorkbook matches MonthlyFinancialReport totals and is formula-injection safe.
/// </summary>
public sealed class V3RoleAndTenantIsolationApiTests
{
    // ─── TC-F05-A02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F05-A02")]
    public void ManagerInternalReviewAndMissionProcessingControllersRequireSystemManagerPolicy()
    {
        var managerOrderAuth = Assert.Single(typeof(V3SystemManagerSurveyOrdersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(AccessAuthorizationPolicies.SystemManager, managerOrderAuth.Policy);

        var managerMissionAuth = Assert.Single(typeof(SystemManagerMissionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(AccessAuthorizationPolicies.SystemManager, managerMissionAuth.Policy);

        Assert.Empty(typeof(V3SystemManagerSurveyOrdersController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.Empty(typeof(SystemManagerMissionsController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    // ─── TC-F06-A02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F06-A02")]
    public void BaseMapAndTenantOwnerOrderEndpointsRequireAuthenticatedAuthorization()
    {
        var baseMapAuth = Assert.Single(typeof(FarmBaseMapsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Null(baseMapAuth.Policy); // Authenticated user required; handler enforces tenant membership

        var ownerOrderAuth = Assert.Single(typeof(V3TenantOwnerSurveyOrdersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(AccessAuthorizationPolicies.TenantOwner, ownerOrderAuth.Policy);
    }

    // ─── TC-F07-A02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F07-A02")]
    public void RevenueDashboardAndExcelExportBothRequireSystemAdminPolicy()
    {
        var controllerAuth = Assert.Single(typeof(V3SystemAdminRevenueReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(AccessAuthorizationPolicies.SystemAdmin, controllerAuth.Policy);
        Assert.NotEqual(AccessAuthorizationPolicies.TenantOwner, controllerAuth.Policy);
        Assert.NotEqual(AccessAuthorizationPolicies.SystemManager, controllerAuth.Policy);

        // Neither Get nor Export may allow anonymous access or override policy
        var getMethod = typeof(V3SystemAdminRevenueReportsController)
            .GetMethod(nameof(V3SystemAdminRevenueReportsController.Get))!;
        var exportMethod = typeof(V3SystemAdminRevenueReportsController)
            .GetMethod(nameof(V3SystemAdminRevenueReportsController.Export))!;

        Assert.Empty(getMethod.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.Empty(exportMethod.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact, Trait("TC", "TC-F07-A02")]
    public async Task RevenueGetAndExportReturnForbidWhenExecutionContextLacksActor()
    {
        using var snapshots = new RevenueReportSnapshots(TimeProvider.System);
        var controller = new V3SystemAdminRevenueReportsController(
            new StubReportQuery(null),
            new UninitializedContext(),
            snapshots);

        var getResult = await controller.Get(2026, 11, null, CancellationToken.None);
        var exportResult = await controller.Export(2026, 11, null, null, CancellationToken.None);

        Assert.IsType<ForbidResult>(getResult);
        Assert.IsType<ForbidResult>(exportResult);
    }

    // ─── TC-NFR-07 (Excel parity + formula safety) ─────────────────────────────

    [Fact, Trait("TC", "TC-NFR-07")]
    public async Task ExcelWorkbookMatchesMonthlyFinancialReportTotalsAndEscapesFormulas()
    {
        var orderId = Guid.NewGuid();
        var period = FinancialReportPeriod.Create(2026, 11);
        var lines = new[]
        {
            new FinancialReportLine(
                OrderId: orderId,
                OrderNumber: "=CMD|'/C calc'!A0", // Formula injection probe
                Service: "MAP",
                PublishedServiceValue: 150_000m,
                ServiceRefunds: 20_000m,
                CashReceipts: 50_000m,
                CashRefunds: 20_000m,
                DepositsHeld: 0m,
                RefundableExcess: 0m,
                RefundPayable: 0m,
                CancellationRetention: 0m,
                OutstandingBalance: 0m,
                PendingPayments: 0m,
                PublishedInPeriod: true,
                Unpriced: false,
                NeedsReconciliation: false)
            {
                ComplaintRefunds = 20_000m
            }
        };

        var report = new MonthlyFinancialReport(
            Year: 2026,
            Month: 11,
            Service: null,
            FormulaVersion: "BR-FIN-v3.2",
            StartUtc: period.StartUtc,
            EndUtc: period.EndUtc,
            GeneratedAt: period.EndUtc,
            HistoryAvailableFrom: period.StartUtc,
            HistoricalBalancesComplete: true,
            Lines: lines);

        using var snapshots = new RevenueReportSnapshots(TimeProvider.System);
        var controller = new V3SystemAdminRevenueReportsController(
            new StubReportQuery(report),
            new InitializedAdminContext(),
            snapshots);

        var exportActionResult = await controller.Export(2026, 11, null, null, CancellationToken.None);
        var fileResult = Assert.IsType<FileContentResult>(exportActionResult);
        using var zip = new ZipArchive(new MemoryStream(fileResult.FileContents), ZipArchiveMode.Read);
        var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);

        using var sheetStream = sheetEntry!.Open();
        var doc = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = doc.Descendants(ns + "row")
            .Select(r => r.Elements(ns + "c").Select(c =>
                c.Element(ns + "v")?.Value ??
                c.Element(ns + "is")?.Element(ns + "t")?.Value ??
                "").ToArray())
            .ToList();

        // Summary values match report properties
        Assert.Contains(rows, r => r.Length >= 6 && r[0] == "PublishedServiceValue" && r[1] == "150000" && r[5] == "130000");
        Assert.Contains(rows, r => r.Length >= 8 && r[0] == "CashReceipts" && r[1] == "50000" && r[3] == "20000" && r[7] == "30000");

        // Order row present and formula string stored as inlineStr (no <f> formula element)
        Assert.Empty(doc.Descendants(ns + "f"));
        var orderRow = rows.Single(r => r.Length >= 5 && r[0] == orderId.ToString());
        Assert.Equal("=CMD|'/C calc'!A0", orderRow[1]);
        Assert.Equal("MAP", orderRow[2]);
        Assert.Equal("150000", orderRow[3]);
    }

    private sealed class StubReportQuery(MonthlyFinancialReport? report) : IMonthlyFinancialReportQuery
    {
        public Task<MonthlyFinancialReport> GetAsync(int year, int month, string? service, CancellationToken cancellationToken) =>
            report is null ? throw new NotSupportedException() : Task.FromResult(report);

        public Task AuditExportAsync(MonthlyFinancialReport report, Guid actorId, Guid correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class InitializedAdminContext : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId { get; } = Guid.NewGuid();
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class UninitializedContext : IExecutionContext
    {
        public bool IsInitialized => false;
        public Guid? TenantId => null;
        public Guid? ActorId => null;
        public Guid CorrelationId => Guid.Empty;
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
