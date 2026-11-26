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
using Xunit;

namespace AgriDrone.IntegrationTests;

/// <summary>Verifies that an order status and a legacy notice cannot substitute for V3 release proof.</summary>
public sealed class V3QualityRefundAndSettlementIntegrationTests : V303H2IntegrationTestBase
{
    public V3QualityRefundAndSettlementIntegrationTests() : base("v3_qrefund")
    {
    }

    [Fact]
    public async Task CompletedV3OrderWithLegacyNoticeCannotOpenClaimWithoutV3ReleaseProof()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, initialStatus: SurveyOrderStatus.Completed);
        var now = DateTimeOffset.UtcNow;
        await using var provider = BuildWorkflowProvider(
            DatabaseName, seeded.OwnerUserId, seeded.TenantId, now.AddHours(-2), now);
        await using var scope = provider.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IQualityRefundWorkflow>();
        var db = scope.ServiceProvider.GetRequiredService<SurveysDbContext>();

        var window = await workflow.GetOwnerClaimWindowAsync(seeded.OrderId, CancellationToken.None);
        Assert.True(window.IsSuccess);
        Assert.False(window.Value.CanReport);
        Assert.Null(window.Value.V3PublicationId);

        var report = await workflow.ReportAsync(
            seeded.OrderId,
            [new ReportedPoleIssue("POLE-A01", "WRONG_POLE", "Wrong pole mapping")],
            "The result points to a different pole", "evidence-valid",
            "Vietcombank", "0123456789", "OWNER TEST", CancellationToken.None,
            publicationId: Guid.NewGuid());

        Assert.True(report.IsFailure);
        Assert.Equal("QualityRefund.ReleaseUnavailable", report.Error.Code);
        Assert.False(await db.QualityRefundCases.AnyAsync(c => c.SurveyOrderId == seeded.OrderId));
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
