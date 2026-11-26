using System.Text.Json;
using AgriDrone.Modules.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

/// <summary>
/// PostgreSQL integration tests for F-07 quality refund pole scoping and state transitions:
/// - TC-F07-A01: Quality report on Order A with pole code only released in Order B (same Farm) is rejected;
///   control sample with pole code released in Order A succeeds.
/// - TC-ST-NEG-05: Recording bank transfer on a Submitted quality refund case is rejected.
/// </summary>
public sealed class V3QualityRefundAndSettlementIntegrationTests : V303H2IntegrationTestBase
{
    public V3QualityRefundAndSettlementIntegrationTests() : base("v3_qrefund")
    {
    }

    [Fact, Trait("TC", "TC-F07-A01"), Trait("TC", "TC-ST-NEG-05")]
    public async Task QualityReportRejectsPoleFromSiblingOrderOnSameFarmAndAcceptsOrderReleasedPole()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, initialStatus: SurveyOrderStatus.Completed);
        var orderAId = seeded.OrderId;
        var orderBId = Guid.NewGuid();
        var requestBId = Guid.NewGuid();
        var resultAId = Guid.NewGuid();
        var resultBId = Guid.NewGuid();
        var plantAId = Guid.NewGuid();
        var plantBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $$"""
                SELECT survey_service_id, business_policy_version_id
                FROM survey.survey_orders WHERE id = '{{orderAId}}';
                """;
            Guid serviceId;
            Guid policyVersionId;
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                serviceId = reader.GetGuid(0);
                policyVersionId = reader.GetGuid(1);
            }

            await ExecuteAsync(conn, $$"""
                -- Order B on the same Tenant & Farm
                INSERT INTO survey.survey_requests
                  (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                   survey_service_id, caller_scope, idempotency_key, applicant_name,
                   applicant_email, applicant_phone, farm_name, farm_address,
                   approximate_area_ha, map_location, status)
                VALUES
                  ('{{requestBId}}', 'REQ-B-{{requestBId:N}}'[1:15], 'EXISTING_FARM_SURVEY', '{{seeded.TenantId}}', '{{seeded.FarmId}}', '{{seeded.OwnerUserId}}',
                   '{{serviceId}}', 'tenant:{{seeded.TenantId}}', 'IDEM-B-{{requestBId:N}}'[1:15], 'Owner',
                   'owner@test.local', '0900000000', 'Test Farm', 'Address',
                   1.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'APPROVED');

                INSERT INTO survey.survey_orders
                  (id, order_number, tenant_id, farm_id, survey_request_id,
                   survey_service_id, scope, requires_baseline_mapping, status,
                   deposit_amount, deposit_payment_status, business_policy_version_id, created_at, updated_at)
                VALUES
                  ('{{orderBId}}', 'ORD-B-{{orderBId:N}}'[1:15], '{{seeded.TenantId}}', '{{seeded.FarmId}}', '{{requestBId}}',
                   '{{serviceId}}', 'V3'::system.contract_scope, FALSE, 'COMPLETED'::system.survey_order_status,
                   100000, 'PAID'::system.survey_payment_status, '{{policyVersionId}}', NOW() - INTERVAL '2 days', NOW());

                -- Captured payment on Order A (150,000 VND)
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, amount, currency, provider, provider_reference, status, purpose, confirmed_at, created_at, updated_at)
                VALUES
                  ('{{Guid.NewGuid()}}', '{{orderAId}}', 150000, 'VND', 'SEPAY', 'SP-QR-A1', 'PAID'::system.survey_payment_status, 'FINAL_BALANCE', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NOW());

                -- Plants on same Farm: POLE-A01 (released in A) and POLE-B01 (released in B)
                INSERT INTO plant.plants (id, farm_id, plant_code, location, position_source, lifecycle_status, created_at, updated_at)
                VALUES
                  ('{{plantAId}}', '{{seeded.FarmId}}', 'POLE-A01', ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'MAPPING_AI', 'ACTIVE', NOW() - INTERVAL '2 days', NOW()),
                  ('{{plantBId}}', '{{seeded.FarmId}}', 'POLE-B01', ST_SetSRID(ST_MakePoint(106.501, 10.501), 4326), 'MAPPING_AI', 'ACTIVE', NOW() - INTERVAL '2 days', NOW());

                -- Published Plant Health results: Order A references plantAId, Order B references plantBId
                INSERT INTO survey.survey_results
                  (id, survey_order_id, tenant_id, farm_id, service_type, status, provenance, payload, reviewed_by, reviewed_at, published_by, published_at)
                VALUES
                  ('{{resultAId}}', '{{orderAId}}', '{{seeded.TenantId}}', '{{seeded.FarmId}}', 'PLANT_HEALTH'::system.survey_service_type, 'PUBLISHED'::system.survey_result_status, '{}'::jsonb,
                   '{"Findings":[{"PlantId":"{{plantAId}}"}]}'::jsonb, '{{seeded.ManagerUserId}}', NOW() - INTERVAL '1 day', '{{seeded.ManagerUserId}}', NOW() - INTERVAL '1 day'),
                  ('{{resultBId}}', '{{orderBId}}', '{{seeded.TenantId}}', '{{seeded.FarmId}}', 'PLANT_HEALTH'::system.survey_service_type, 'PUBLISHED'::system.survey_result_status, '{}'::jsonb,
                   '{"Findings":[{"PlantId":"{{plantBId}}"}]}'::jsonb, '{{seeded.ManagerUserId}}', NOW() - INTERVAL '1 day', '{{seeded.ManagerUserId}}', NOW() - INTERVAL '1 day');
                """);
        }

        await using var provider = BuildWorkflowProvider(
            DatabaseName,
            seeded.OwnerUserId,
            seeded.TenantId,
            noticeCreatedAt: now.AddHours(-2),
            now: now);

        await using var scope = provider.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IQualityRefundWorkflow>();
        var db = scope.ServiceProvider.GetRequiredService<SurveysDbContext>();

        // 1. Pole from Order B reported against Order A -> rejected with QualityRefund.PoleOutsideReleasedResult
        var crossOrderReport = await workflow.ReportAsync(
            orderAId,
            [new ReportedPoleIssue("POLE-B01", "FALSE_POSITIVE", "Pole belongs to Order B only")],
            "Reporting wrong pole from another order on the same farm",
            "evidence-cross",
            "Vietcombank",
            "0123456789",
            "OWNER TEST",
            CancellationToken.None);

        Assert.True(crossOrderReport.IsFailure);
        Assert.Equal("QualityRefund.PoleOutsideReleasedResult", crossOrderReport.Error.Code);
        Assert.Equal(0, await db.QualityRefundCases.CountAsync(c => c.SurveyOrderId == orderAId));

        // 2. Control sample: Pole released in Order A -> accepted and creates Submitted case
        var validReport = await workflow.ReportAsync(
            orderAId,
            [new ReportedPoleIssue("POLE-A01", "FALSE_POSITIVE", "Pole released in Order A")],
            "Valid quality report for Order A pole",
            "evidence-valid",
            "Vietcombank",
            "0123456789",
            "OWNER TEST",
            CancellationToken.None);

        Assert.True(validReport.IsSuccess, validReport.Error?.Description);
        Assert.Equal(QualityRefundStatus.Submitted, validReport.Value.Status);

        // 3. TC-ST-NEG-05: Recording transfer on a Submitted case is rejected
        var prematureTransfer = await workflow.RecordTransferAsync(
            orderAId,
            validReport.Value.Id,
            "FT-PREMATURE-01",
            "receipt-ref",
            validReport.Value.Version,
            CancellationToken.None);

        Assert.True(prematureTransfer.IsFailure);
        Assert.Equal("QualityRefund.TransferConflict", prematureTransfer.Error.Code);
    }

    private static ServiceProvider BuildWorkflowProvider(
        string databaseName,
        Guid actorId,
        Guid tenantId,
        DateTimeOffset noticeCreatedAt,
        DateTimeOffset now)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] = BuildConnectionString(databaseName),
                ["SurveyPayments:SePay:BaseUrl"] = "https://sandbox.sepay.local",
                ["SurveyPayments:SePay:MerchantId"] = "test-merchant",
                ["SurveyPayments:SePay:SecretKey"] = "test-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
        services.AddScoped<IExecutionContext>(_ => new TestExecutionContext(actorId, tenantId));
        services.AddScoped<ISystemManagerAccessService, StubManagerAccess>();
        services.AddScoped<IAuditWriter, NoopAuditWriter>();
        services.AddScoped<IQualityRefundNoticeQuery>(_ => new FixedNoticeQuery(noticeCreatedAt));
        services.AddScoped<IRefundEvidenceStorage, AlwaysExistsEvidenceStorage>();
        services.AddSurveysModule(config);
        return services.BuildServiceProvider();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedNoticeQuery(DateTimeOffset createdAt) : IQualityRefundNoticeQuery
    {
        public Task<DateTimeOffset?> GetCreatedAtAsync(Guid tenantId, Guid orderId, Guid resultId, CancellationToken cancellationToken) =>
            Task.FromResult<DateTimeOffset?>(createdAt);
    }

    private sealed class StubManagerAccess : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Denied("Not manager"));
    }

    private sealed class AlwaysExistsEvidenceStorage : IRefundEvidenceStorage
    {
        public Task<string> UploadAsync(Guid tenantId, Guid caseId, Stream content, CancellationToken cancellationToken) =>
            Task.FromResult("ref");
        public Task<bool> ExistsAsync(Guid tenantId, Guid caseId, string reference, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task<Uri> GetDownloadUriAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri("https://example.invalid/evidence"));
    }

    private sealed class NoopAuditWriter : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) { }

        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId, Guid correlationId,
            string entityType, Guid entityId, string action, JsonDocument? oldData,
            JsonDocument? newData, DateTimeOffset createdAt) { }
    }
}
