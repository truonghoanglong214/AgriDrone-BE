using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyRequestStep2Tests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntakeRejectsHarvestReadinessEvenWhenServiceExists()
    {
        var service = SurveyService.Create(
            "HARVEST_READINESS",
            "Harvest readiness",
            "Compatibility-only service",
            SurveyServiceType.HarvestReadiness,
            Now);

        Assert.Throws<ArgumentException>(() =>
            SurveyRequest.CreateNewCustomer(
                "SR-STEP2-001",
                service,
                RequestIdempotency.Create("public:test", "request-1"),
                "TENANT-STEP2",
                "Applicant",
                "applicant@example.test",
                "+84901234567",
                "Dragon fruit farm",
                "Test address",
                1.5m,
                new Point(106.7, 10.8) { SRID = 4326 },
                100,
                null,
                null,
                null,
                Now));
    }

    [Fact]
    public void EquivalentNormalizedSubmissionsHaveSameFingerprint()
    {
        var serviceId = Guid.NewGuid();
        var first = CreateSnapshot(
            serviceId,
            applicantName: " Applicant ",
            applicantEmail: "OWNER@EXAMPLE.TEST",
            applicantPhone: "+84 901-234-567",
            notes: " Inspect north rows ");
        var second = CreateSnapshot(
            serviceId,
            applicantName: "Applicant",
            applicantEmail: "owner@example.test",
            applicantPhone: "+84901234567",
            notes: "Inspect north rows");

        Assert.Equal(
            SurveyRequestSubmissionFingerprint.Create(first).Value,
            SurveyRequestSubmissionFingerprint.Create(second).Value);
    }

    [Fact]
    public void ReusingKeyWithDifferentBusinessPayloadChangesFingerprint()
    {
        var serviceId = Guid.NewGuid();
        var first = CreateSnapshot(serviceId, estimatedPoleCount: 100);
        var second = CreateSnapshot(serviceId, estimatedPoleCount: 101);

        Assert.NotEqual(
            SurveyRequestSubmissionFingerprint.Create(first).Value,
            SurveyRequestSubmissionFingerprint.Create(second).Value);
    }

    [Fact]
    public void TenantCallerScopeIsServerDerivedAndStable()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var first = SurveyRequestIdempotencyFactory.ForTenantActor(
            tenantId,
            actorId,
            " request-key ");
        var second = SurveyRequestIdempotencyFactory.ForTenantActor(
            tenantId,
            actorId,
            "request-key");

        Assert.Equal(first, second);
        Assert.Equal(
            $"tenant:{tenantId:N}:actor:{actorId:N}",
            first.CallerScope);
        Assert.Equal("request-key", first.Key);
    }

    private static SurveyRequestSubmissionSnapshot CreateSnapshot(
        Guid serviceId,
        string applicantName = "Applicant",
        string applicantEmail = "owner@example.test",
        string applicantPhone = "+84901234567",
        int? estimatedPoleCount = 100,
        string? notes = null) =>
        SurveyRequestSubmissionSnapshot.Create(
            SurveyRequestKind.NewCustomer,
            tenantId: null,
            farmId: null,
            requestedByUserId: null,
            serviceId,
            "TENANT-STEP2",
            applicantName,
            applicantEmail,
            applicantPhone,
            "Dragon fruit farm",
            "Test address",
            1.5m,
            106.7,
            10.8,
            4326,
            estimatedPoleCount,
            null,
            null,
            notes);
}
