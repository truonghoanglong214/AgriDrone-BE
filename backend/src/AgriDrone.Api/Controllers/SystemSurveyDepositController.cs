using AgriDrone.Api.Contracts.Surveys.Catalogue;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-deposit")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyDepositController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCurrentSurveyDepositPolicyQuery(), cancellationToken);
        return result.ToHttpResult(HttpContext, policy => Results.Ok(Map(policy)));
    }

    [HttpGet("versions")]
    public async Task<IResult> GetVersions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyDepositPoliciesQuery(), cancellationToken);
        return result.ToHttpResult(HttpContext, policies => Results.Ok(
            policies.Select(Map).ToArray()));
    }

    [HttpPost("versions")]
    public async Task<IResult> CreateVersion(
        [FromBody] CreateSurveyDepositPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSurveyDepositPolicyCommand(
                request.AmountVnd, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, policy => Results.Json(
            Map(policy), statusCode: StatusCodes.Status201Created));
    }

    private static SurveyDepositPolicyApiResponse Map(
        SurveyDepositPolicyResponse policy) => new(
            policy.PolicyId,
            policy.VersionNumber,
            policy.AmountVnd,
            policy.EffectiveFrom);
}
