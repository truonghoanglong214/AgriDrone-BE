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

    /// <summary>
    /// Phân loại điểm kiểm tra so với ranh giới Farm (WithinBoundary, NearBoundary, OutOfBoundary) và ghi nhận ngoại lệ.
    /// </summary>
    [HttpPost("{farmId:guid}/boundary-exceptions/classify")]
    [ProducesResponseType<ClassifyPointApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> ClassifyPoint(
        [FromRoute] Guid farmId,
        [FromBody] ClassifyPointApiRequest request,
        CancellationToken cancellationToken)
    {
        var point = GeoJsonGeometryMapper.ToPoint(request.Point);
        if (point is null)
        {
            return Results.BadRequest("Invalid point coordinates.");
        }

        var result = await sender.Send(
            new AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint.ClassifyAndRecordPointRequest(
                new AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint.ClassifyAndRecordPointCommand(
                    farmId,
                    point,
                    request.Source,
                    request.SourceReferenceId,
                    request.SurveyOrderId,
                    request.MissionId,
                    request.NearBoundaryThresholdMeters)),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(new ClassifyPointApiResponse(
                response.Classification.ToString(),
                response.DistanceMeters,
                response.BoundaryExceptionId,
                response.State)));
    }

    /// <summary>
    /// Lấy danh sách các Boundary Exceptions của Farm theo trạng thái.
    /// </summary>
    [HttpGet("{farmId:guid}/boundary-exceptions")]
    [ProducesResponseType<IReadOnlyList<BoundaryExceptionApiResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBoundaryExceptions(
        [FromRoute] Guid farmId,
        [FromQuery] AgriDrone.Modules.Farms.Domain.Boundaries.BoundaryExceptionState? state,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AgriDrone.Modules.Farms.Application.Features.GetBoundaryExceptions.GetBoundaryExceptionsQuery(
                farmId,
                state),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response.Select(e => new BoundaryExceptionApiResponse(
                e.Id,
                e.FarmId,
                e.FarmBoundaryVersionId,
                e.Source,
                e.SourceReferenceId,
                e.SurveyOrderId,
                e.MissionId,
                GeoJsonGeometryMapper.FromPoint(e.OriginalPosition)!,
                e.State,
                e.MeasuredDistanceMeters,
                e.ThresholdMeters,
                e.Decision,
                e.CorrectedPosition is null ? null : GeoJsonGeometryMapper.FromPoint(e.CorrectedPosition),
                e.ReviewedBy,
                e.ReviewedAt,
                e.ReviewReason,
                e.Version,
                e.CreatedAt)).ToArray()));
    }

    /// <summary>
    /// SystemManager xét duyệt quyết định cho Boundary Exception.
    /// </summary>
    [HttpPost("{farmId:guid}/boundary-exceptions/{exceptionId:guid}/review")]
    [ProducesResponseType<ReviewBoundaryExceptionApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ReviewBoundaryException(
        [FromRoute] Guid farmId,
        [FromRoute] Guid exceptionId,
        [FromBody] ReviewBoundaryExceptionApiRequest request,
        CancellationToken cancellationToken)
    {
        NetTopologySuite.Geometries.Point? correctedPoint = null;
        if (request.CorrectedPosition is not null)
        {
            correctedPoint = GeoJsonGeometryMapper.ToPoint(request.CorrectedPosition);
            if (correctedPoint is null)
            {
                return Results.BadRequest("Invalid corrected position point coordinates.");
            }
        }

        using var defaultEvidence = System.Text.Json.JsonDocument.Parse("{}");
        var evidence = request.Evidence ?? defaultEvidence;

        var result = await sender.Send(
            new AgriDrone.Modules.Farms.Application.Features.ReviewBoundaryException.ReviewBoundaryExceptionRequest(
                new AgriDrone.Modules.Farms.Application.Features.ReviewBoundaryException.ReviewBoundaryExceptionCommand(
                    farmId,
                    exceptionId,
                    request.Decision,
                    correctedPoint,
                    request.Reason,
                    evidence,
                    request.ExpectedVersion!.Value)),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(new ReviewBoundaryExceptionApiResponse(
                response.ExceptionId,
                response.State,
                response.Decision,
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
