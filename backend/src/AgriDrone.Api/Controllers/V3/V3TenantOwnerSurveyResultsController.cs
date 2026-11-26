using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/tenant-owner")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class V3TenantOwnerSurveyResultsController(
    IV3OwnerSurveyResults results, IExecutionContext executionContext) : ControllerBase
{
    [HttpGet("survey-orders/{orderId:guid}/published-result")]
    public async Task<IResult> GetResult(Guid orderId, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Forbid();

        var result = await results.GetAsync(tenantId, orderId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    [HttpGet("survey-orders/{orderId:guid}/published-result/images/{mediaId:guid}")]
    public async Task<IResult> GetImage(Guid orderId, Guid mediaId, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Forbid();

        var uri = await results.GetImageAsync(tenantId, orderId, mediaId, cancellationToken);
        return uri is null ? Results.NotFound() : Results.Redirect(uri.ToString());
    }

    [HttpGet("farms/{farmId:guid}/survey-results")]
    public async Task<IResult> GetHistory(Guid farmId, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Forbid();

        var history = await results.GetHistoryAsync(tenantId, farmId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    }

    [HttpGet("farms/{farmId:guid}/survey-results/compare")]
    public async Task<IResult> Compare(
        Guid farmId, [FromQuery] Guid fromOrderId, [FromQuery] Guid toOrderId, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Forbid();

        var comparison = await results.CompareAsync(tenantId, farmId, fromOrderId, toOrderId, cancellationToken);
        return comparison is null ? Results.NotFound() : Results.Ok(comparison);
    }
}
