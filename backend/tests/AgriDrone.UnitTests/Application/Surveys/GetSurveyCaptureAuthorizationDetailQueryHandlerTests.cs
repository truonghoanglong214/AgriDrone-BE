using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizationDetail;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizations;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class GetSurveyCaptureAuthorizationDetailQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsNotFoundWhenAuthorizationDoesNotExist()
    {
        var repo = new FakeSurveyCaptureAuthorizationRepository([]);
        var handler = new GetSurveyCaptureAuthorizationDetailQueryHandler(repo);

        var result = await handler.Handle(
            new GetSurveyCaptureAuthorizationDetailQuery(
                Guid.NewGuid(), SurveyCaptureAuthorizationType.WrittenDataUseConsent),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SurveyCaptureAuthorization.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsAuthorizationDetailWhenFound()
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var auth = SurveyCaptureAuthorization.CreatePending(
            orderId, SurveyCaptureAuthorizationType.FlightPermission, now.AddDays(-2));

        auth.Verify(
            "minio://agridrone-media/evidences/orders/123/flight_permission.pdf",
            now.AddDays(-1),
            now.AddDays(30),
            Guid.NewGuid(),
            now,
            0);

        var repo = new FakeSurveyCaptureAuthorizationRepository([auth]);
        var handler = new GetSurveyCaptureAuthorizationDetailQueryHandler(repo);

        var result = await handler.Handle(
            new GetSurveyCaptureAuthorizationDetailQuery(
                orderId, SurveyCaptureAuthorizationType.FlightPermission),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value.SurveyOrderId);
        Assert.Equal(SurveyCaptureAuthorizationType.FlightPermission, result.Value.Type);
        Assert.Equal(SurveyCaptureAuthorizationStatus.Verified, result.Value.Status);
        Assert.Equal("minio://agridrone-media/evidences/orders/123/flight_permission.pdf", result.Value.EvidenceReference);
        Assert.Equal(1u, result.Value.Version);
    }

    [Fact]
    public async Task GetAllAuthorizationsReturnsAllForOrder()
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var consent = SurveyCaptureAuthorization.CreatePending(
            orderId, SurveyCaptureAuthorizationType.WrittenDataUseConsent, now);
        var permit = SurveyCaptureAuthorization.CreatePending(
            orderId, SurveyCaptureAuthorizationType.FlightPermission, now);

        var repo = new FakeSurveyCaptureAuthorizationRepository([consent, permit]);
        var handler = new GetSurveyCaptureAuthorizationsQueryHandler(repo);

        var result = await handler.Handle(
            new GetSurveyCaptureAuthorizationsQuery(orderId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, a => a.Type == SurveyCaptureAuthorizationType.WrittenDataUseConsent);
        Assert.Contains(result.Value, a => a.Type == SurveyCaptureAuthorizationType.FlightPermission);
    }

    private sealed class FakeSurveyCaptureAuthorizationRepository(
        IReadOnlyList<SurveyCaptureAuthorization> authorizations)
        : ISurveyCaptureAuthorizationRepository
    {
        public Task<SurveyCaptureAuthorization?> GetAsync(
            Guid surveyOrderId,
            SurveyCaptureAuthorizationType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(authorizations.FirstOrDefault(
                a => a.SurveyOrderId == surveyOrderId && a.Type == type));

        public Task<IReadOnlyList<SurveyCaptureAuthorization>> ListByOrderIdAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SurveyCaptureAuthorization>>(
                authorizations.Where(a => a.SurveyOrderId == surveyOrderId).ToList());
    }
}
