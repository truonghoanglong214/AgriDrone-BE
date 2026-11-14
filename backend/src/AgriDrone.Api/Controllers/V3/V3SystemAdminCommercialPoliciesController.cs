using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/system-admin/commercial-policies")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class V3SystemAdminCommercialPoliciesController(ISender sender) : ControllerBase
{
    /// <summary>Đăng ký dịch vụ khảo sát V3 mới (ví dụ: MAP, COMBO).</summary>
    [HttpPost("services")]
    [ProducesResponseType<CreateSurveyServiceV3Response>(StatusCodes.Status201Created)]
    public async Task<IResult> CreateService(
        [FromBody] CreateSurveyServiceV3ApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSurveyServiceV3Command(
                request.Code,
                request.Name,
                request.Description,
                request.ActivateImmediately),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ban hành revision mới cho dịch vụ khảo sát (MAP, COMBO).</summary>
    [HttpPost("revisions")]
    [ProducesResponseType<PublishSurveyServiceRevisionResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishRevision(
        [FromBody] PublishSurveyServiceRevisionApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishSurveyServiceRevisionCommand(
                request.SurveyServiceId,
                request.Name,
                request.Description,
                request.MapsPoles,
                request.ScreensDisease,
                request.ExpectedLatestVersionNumber,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ban hành biểu giá theo trụ mới cho dịch vụ V3.</summary>
    [HttpPost("prices")]
    [ProducesResponseType<PublishSurveyServicePriceV3Response>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishPrice(
        [FromBody] PublishSurveyServicePriceV3ApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishSurveyServicePriceV3Command(
                request.SurveyServiceId,
                request.PricePerPole,
                request.Currency,
                request.ExpectedLatestPriceVersion,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ban hành chính sách cọc cố định dùng chung mới.</summary>
    [HttpPost("deposit-policies")]
    [ProducesResponseType<PublishSurveyDepositPolicyResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishDepositPolicy(
        [FromBody] PublishSurveyDepositPolicyApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishSurveyDepositPolicyCommand(
                request.Amount,
                request.Currency,
                request.ExpectedLatestVersion,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ban hành chính sách khiếu nại (Claim) mới.</summary>
    [HttpPost("claim-policies")]
    [ProducesResponseType<PublishClaimPolicyResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishClaimPolicy(
        [FromBody] PublishClaimPolicyApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishClaimPolicyCommand(
                request.CalendarWindowDays,
                request.TimeZoneId,
                request.ExcludeNoticeDate,
                request.ExpectedLatestVersion,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ban hành chính sách hủy dịch vụ và hoàn cọc mới.</summary>
    [HttpPost("cancellation-policies")]
    [ProducesResponseType<PublishCancellationPolicyResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishCancellationPolicy(
        [FromBody] PublishCancellationPolicyApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishCancellationPolicyCommand(
                request.RefundableBeforeCapture,
                request.RetainDepositAfterCapture,
                request.AllowedFreeNoShowReschedules,
                request.OperationalPostponementRefundable,
                request.ServiceFailureRefundable,
                request.ExpectedLatestVersion,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Đóng gói và ban hành bundle chính sách thương mại tổng thể (BusinessPolicyVersion).</summary>
    [HttpPost("bundle-versions")]
    [ProducesResponseType<PublishBusinessPolicyVersionResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> PublishBundleVersion(
        [FromBody] PublishBusinessPolicyVersionApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishBusinessPolicyVersionCommand(
                request.StreamKey ?? "DEFAULT_BUNDLE",
                request.DepositPolicyId,
                request.ClaimPolicyId,
                request.CancellationPolicyId,
                request.ServiceRevisionIds,
                request.PricingCurrency ?? "VND",
                request.ExpectedLatestVersion,
                request.ReasonCode,
                request.EvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Kích hoạt phiên bản chính sách thương mại và điều khiển các cờ vận hành.</summary>
    [HttpPost("activations")]
    [ProducesResponseType<TransitionBusinessPolicyActivationResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> TransitionActivation(
        [FromBody] TransitionBusinessPolicyActivationApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionBusinessPolicyActivationCommand(
                request.TargetPolicyVersionId,
                request.PublicIntakeEnabled,
                request.CaptureEnabled,
                request.ReleaseEnabled,
                request.ExpectedLastDecisionId,
                request.DecisionReasonCode,
                request.DecisionEvidenceReference),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }
}
