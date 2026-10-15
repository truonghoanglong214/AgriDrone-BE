using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.RevokeSurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-orders/{orderId:guid}/capture-authorizations")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyOrderAuthorizationsController(ISender sender)
    : ControllerBase
{
    [HttpPut("{type}/verify")]
    public async Task<IResult> Verify(
        Guid orderId,
        string type,
        VerifySurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        var result = await sender.Send(new VerifySurveyCaptureAuthorizationCommand(
            orderId, authorizationType, request.EvidenceReference, request.IssuedAt,
            request.ExpiresAt, request.ExpectedVersion), cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    [HttpPut("{type}/revoke")]
    public async Task<IResult> Revoke(
        Guid orderId,
        string type,
        RevokeSurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        var result = await sender.Send(new RevokeSurveyCaptureAuthorizationCommand(
            orderId, authorizationType, request.Reason, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    private static bool TryParseType(
        string value,
        out SurveyCaptureAuthorizationType type)
    {
        type = value.ToUpperInvariant() switch
        {
            "WRITTEN_DATA_USE_CONSENT" => SurveyCaptureAuthorizationType.WrittenDataUseConsent,
            "FLIGHT_PERMISSION" => SurveyCaptureAuthorizationType.FlightPermission,
            _ => (SurveyCaptureAuthorizationType)(-1)
        };
        return Enum.IsDefined(type);
    }

    private static SurveyCaptureAuthorizationApiResponse Map(
        SurveyCaptureAuthorizationResponse item) => new(
            item.Id, item.SurveyOrderId,
            item.Type == SurveyCaptureAuthorizationType.WrittenDataUseConsent
                ? "WRITTEN_DATA_USE_CONSENT" : "FLIGHT_PERMISSION",
            item.Status.ToString().ToUpperInvariant(), item.VerifiedAt,
            item.ExpiresAt, item.RevokedAt, item.Version);
}
