using AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class VerifySurveyCaptureAuthorizationCommandValidatorTests
{
    private readonly VerifySurveyCaptureAuthorizationCommandValidator _validator = new();

    [Fact]
    public void ValidStorageCommandPassesValidation()
    {
        var command = new VerifySurveyCaptureAuthorizationCommand(
            SurveyOrderId: Guid.NewGuid(),
            Type: SurveyCaptureAuthorizationType.WrittenDataUseConsent,
            EvidenceReference: "minio://agridrone-media/evidences/orders/123/consent.jpg",
            IssuedAt: DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30),
            ExpectedVersion: 0);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidMockCommandPassesValidation()
    {
        var command = new VerifySurveyCaptureAuthorizationCommand(
            SurveyOrderId: Guid.NewGuid(),
            Type: SurveyCaptureAuthorizationType.FlightPermission,
            EvidenceReference: "mock:student-drone-lab-2026",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            ExpectedVersion: 1);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyEvidenceReferenceFailsValidation(string evidence)
    {
        var command = new VerifySurveyCaptureAuthorizationCommand(
            SurveyOrderId: Guid.NewGuid(),
            Type: SurveyCaptureAuthorizationType.WrittenDataUseConsent,
            EvidenceReference: evidence,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            ExpectedVersion: 0);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifySurveyCaptureAuthorizationCommand.EvidenceReference));
    }

    [Fact]
    public void ExpiryBeforeIssuedFailsValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var command = new VerifySurveyCaptureAuthorizationCommand(
            SurveyOrderId: Guid.NewGuid(),
            Type: SurveyCaptureAuthorizationType.FlightPermission,
            EvidenceReference: "mock:permit",
            IssuedAt: now,
            ExpiresAt: now.AddDays(-1),
            ExpectedVersion: 0);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifySurveyCaptureAuthorizationCommand.ExpiresAt));
    }
}
