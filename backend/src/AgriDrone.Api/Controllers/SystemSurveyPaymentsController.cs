using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-payments")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyPaymentsController(ISender sender)
    : ControllerBase
{
    /// <summary>
    /// Reconcile payment bằng provider reference và evidence của SystemAdmin.
    /// </summary>
    [HttpPost("{paymentId:guid}/reconcile")]
    [ProducesResponseType<ReconcileSurveyPaymentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Reconcile(
        [FromRoute] Guid paymentId,
        [FromBody] ReconcileSurveyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReconcileSurveyPaymentCommand(
                paymentId,
                request.ProviderReference,
                request.SettledAmount!.Value,
                request.Currency,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedPaymentVersion!.Value,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
    }
}
