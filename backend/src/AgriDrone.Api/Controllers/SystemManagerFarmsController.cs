using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerFarmsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lấy ngữ cảnh vận hành của Farm được phân công cho SystemManager (Boundary, Zones, Published Base Map).
    /// </summary>
    [HttpGet("{farmId:guid}/operational-context")]
    [ProducesResponseType<FarmOperationalContextApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetOperationalContext(
        [FromRoute] Guid farmId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAssignedFarmOperationalContextQuery(farmId),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(ToApiResponse(response)));
    }

    /// <summary>
    /// Xác minh hoặc cập nhật phiên bản ranh giới Farm (Mapped/Correction Path).
    /// </summary>
    [HttpPost("{farmId:guid}/boundary/verify")]
    [ProducesResponseType<VerifyFarmBoundaryApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> VerifyBoundary(
        [FromRoute] Guid farmId,
        [FromBody] VerifyFarmBoundaryApiRequest request,
        CancellationToken cancellationToken)
    {
        var polygon = GeoJsonGeometryMapper.ToPolygon(request.Boundary);
        if (polygon is null)
        {
            return Results.BadRequest("Invalid polygon geometry.");
        }

        var result = await sender.Send(
            new AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary.VerifyFarmBoundaryCommand(
                farmId,
                polygon,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedFarmVersion!.Value,
                request.ExpectedCurrentBoundaryVersionNumber),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(new VerifyFarmBoundaryApiResponse(
                response.BoundaryId,
                response.FarmId,
                response.VersionNumber,
                GeoJsonGeometryMapper.FromPolygon(response.Geometry),
                response.Status,
                response.Source,
                response.EvidenceReference,
                response.ReviewedBy,
                response.ReviewedAt,
                response.ReviewReason,
                response.Version)));
    }

    private static FarmOperationalContextApiResponse ToApiResponse(
        FarmOperationalContextResponse response) =>
        new(
            response.FarmId,
            response.TenantId,
            response.Code,
            response.Name,
            response.Address,
            response.Status,
            response.AreaHectares,
            response.Version,
            response.ApprovedBoundary is null
                ? null
                : new ApprovedBoundaryApiResponse(
                    response.ApprovedBoundary.Id,
                    response.ApprovedBoundary.VersionNumber,
                    GeoJsonGeometryMapper.FromPolygon(response.ApprovedBoundary.Geometry),
                    response.ApprovedBoundary.Source,
                    response.ApprovedBoundary.EvidenceReference,
                    response.ApprovedBoundary.ReviewedBy,
                    response.ApprovedBoundary.ReviewedAt,
                    response.ApprovedBoundary.ReviewReason,
                    response.ApprovedBoundary.Version),
            response.Zones.Select(z => new OperationalZoneApiResponse(
                z.Id,
                z.Code,
                z.Name,
                z.Status,
                z.AreaHectares,
                GeoJsonGeometryMapper.FromPolygon(z.Boundary),
                z.Version)).ToArray(),
            response.PublishedBaseMap is null
                ? null
                : new PublishedBaseMapApiResponse(
                    response.PublishedBaseMap.Id,
                    response.PublishedBaseMap.VersionNumber,
                    response.PublishedBaseMap.SourceSurveyOrderId,
                    response.PublishedBaseMap.PublishedAt,
                    response.PublishedBaseMap.PublishedBy,
                    response.PublishedBaseMap.Version));
}
