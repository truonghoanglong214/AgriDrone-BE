using AgriDrone.Api.Contracts.Surveys.Catalogue;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-combo-discount")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyComboDiscountController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCurrentSurveyComboDiscountPolicyQuery(), cancellationToken);
        return result.ToHttpResult(HttpContext, policy => Results.Ok(Map(policy)));
    }

    [HttpGet("versions")]
    public async Task<IResult> GetVersions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyComboDiscountPoliciesQuery(), cancellationToken);
        return result.ToHttpResult(HttpContext, policies => Results.Ok(
            policies.Select(Map).ToArray()));
    }

    [HttpPost("versions")]
    public async Task<IResult> CreateVersion(
        [FromBody] CreateSurveyComboDiscountPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSurveyComboDiscountPolicyCommand(
                request.DiscountPercentage, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, policy => Results.Json(
            Map(policy), statusCode: StatusCodes.Status201Created));
    }

    private static SurveyComboDiscountPolicyApiResponse Map(
        SurveyComboDiscountPolicyResponse policy) => new(
            policy.PolicyId,
            policy.VersionNumber,
            policy.DiscountPercentage,
            policy.EffectiveFrom);
}

