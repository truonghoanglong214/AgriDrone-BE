using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Missions.Application.Features.PlantHealth;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

/// <summary>
/// Plant Health Review APIs for SystemManager.
/// Route base: api/system-manager/farms/{farmId}
///
/// UC-28: Review suspected disease observation (Confirm / Reject)
/// UC-29: Resolve flagged outlier plants (SNAP / CREATE_NEW_PLANT / DISMISS)
/// UC-30: Disease zone adjustment (Merge / Reshape / Update memberships)
/// </summary>
[ApiController]
[Route("api/system-manager/farms/{farmId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class PlantHealthReviewController(
    ISender sender) : ControllerBase
{
    // =========================================================
    // UC-28: Review suspected disease observation
    // PUT api/system-manager/farms/{farmId}/plant-health/observations/{observationId}/review
    // =========================================================

    /// <summary>
    /// Manager xem anh crop cua AI, bam Confirm hoac Reject tung vet benh.
    /// - Confirmed: cap nhat plant health = MODERATE, pin doi mau tren map.
    /// - Rejected:  xoa co benh, plant tro ve HEALTHY neu khong con vet Confirmed khac.
    /// </summary>
    [HttpPut("plant-health/observations/{observationId:guid}/review")]
    public async Task<IResult> ReviewObservation(
        [FromRoute] Guid farmId,
        [FromRoute] Guid observationId,
        [FromBody] ReviewObservationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReviewPlantObservationCommand(
            farmId,
            observationId,
            request.Decision,
            request.Reason ?? string.Empty,
            request.CorrectedConditionCode);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ReviewObservationResponse(
                value.ObservationId,
                value.NewStatus,
                value.ResolvedPlantId,
                request.Reason ?? string.Empty)));
    }

    // =========================================================
    // UC-29: Get flagged (Pending) observations for a farm
    // GET api/system-manager/farms/{farmId}/plant-health/flagged-observations
    // =========================================================

    /// <summary>
    /// Lay danh sach anh chup bi co Pending (lech toa do > 2.5m, chua khop cay nao).
    /// Frontend hien thi danh sach outlier + candidate target poles de manager xu ly.
    /// </summary>
    [HttpGet("plant-health/flagged-observations")]
    public async Task<IResult> GetFlaggedObservations(
        [FromRoute] Guid farmId,
        CancellationToken cancellationToken)
    {
        var query = new GetFlaggedObservationsQuery(farmId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new
            {
                farmId,
                count = value.Items.Count,
                items = value.Items
            }));
    }

    // =========================================================
    // UC-29: Resolve flagged observation
    // POST api/system-manager/farms/{farmId}/plant-health/observations/{observationId}/resolve-flag
    // =========================================================

    /// <summary>
    /// Xu ly anh chup bi co Pending:
    /// - SNAP: hat vao cay co san (can TargetPlantId).
    /// - CREATE_NEW_PLANT: tao cay moi tai vi tri phat hien (kiem tra ST_Covers voi ranh gioi vuon).
    /// - DISMISS: bac bo (False Positive / Noise).
    /// </summary>
    [HttpPost("plant-health/observations/{observationId:guid}/resolve-flag")]
    public async Task<IResult> ResolveFlaggedObservation(
        [FromRoute] Guid farmId,
        [FromRoute] Guid observationId,
        [FromBody] ResolveFlaggedObservationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolveFlaggedObservationCommand(
            farmId,
            Guid.Empty,
            observationId,
            request.Action,
            request.TargetPlantId,
            request.NewPlantCode,
            request.ZoneId,
            request.Reason ?? "Resolved by manager");

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => value.Action == "CREATE_NEW_PLANT"
                ? Results.Created(
                    $"/api/system-manager/farms/{farmId}/plants/{value.NewPlantId}",
                    new ResolveFlaggedObservationResponse(
                        value.ObservationId,
                        value.Action,
                        value.NewStatus,
                        value.ResolvedPlantId,
                        value.NewPlantId,
                        value.NewPlantCode))
                : Results.Ok(new ResolveFlaggedObservationResponse(
                    value.ObservationId,
                    value.Action,
                    value.NewStatus,
                    value.ResolvedPlantId,
                    value.NewPlantId,
                    value.NewPlantCode)));
    }

    // =========================================================
    // UC-30a: Merge disease zones
    // POST api/system-manager/farms/{farmId}/plant-health/disease-zones/merge
    // =========================================================

    /// <summary>
    /// Gop 2+ vung benh PROPOSED thanh 1 vung moi.
    /// Client truyen danh sach ZoneIds va polygon moi bao phu toan bo cay.
    /// </summary>
    [HttpPost("plant-health/disease-zones/merge")]
    public async Task<IResult> MergeDiseaseZones(
        [FromRoute] Guid farmId,
        [FromBody] MergeDiseaseZonesRequest request,
        CancellationToken cancellationToken)
    {
        var mergedGeometry = GeoJsonGeometryMapper.ToPolygon(request.MergedGeometry);
        if (mergedGeometry is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["mergedGeometry"] = ["Invalid GeoJSON polygon."]
                });
        }

        var command = new MergeDiseaseZonesCommand(
            farmId,
            request.ZoneIds,
            mergedGeometry);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new MergeDiseaseZonesResponse(
                value.MergedZoneId,
                value.TotalTrees,
                value.MergedCount)));
    }

    // =========================================================
    // UC-30b: Reshape zone boundary
    // PUT api/system-manager/farms/{farmId}/plant-health/disease-zones/{zoneId}/boundary
    // =========================================================

    /// <summary>
    /// Cap nhat duong bien polygon vung benh sau khi manager keo chinh tren Leaflet Draw.
    /// Tu dong tinh lai danh sach cay nam trong polygon moi (ST_Contains).
    /// </summary>
    [HttpPut("plant-health/disease-zones/{zoneId:guid}/boundary")]
    public async Task<IResult> UpdateZoneBoundary(
        [FromRoute] Guid farmId,
        [FromRoute] Guid zoneId,
        [FromBody] UpdateZoneBoundaryRequest request,
        CancellationToken cancellationToken)
    {
        var newGeometry = GeoJsonGeometryMapper.ToPolygon(request.NewGeometry);
        if (newGeometry is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["newGeometry"] = ["Invalid GeoJSON polygon."]
                });
        }

        var command = new UpdateZoneBoundaryCommand(farmId, zoneId, newGeometry);
        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new UpdateZoneBoundaryResponse(
                value.ZoneId,
                value.GeometryGeoJson,
                value.TotalEnclosedTrees)));
    }

    // =========================================================
    // UC-30c: Update zone memberships (add/remove plants manually)
    // PATCH api/system-manager/farms/{farmId}/plant-health/disease-zones/{zoneId}/memberships
    // =========================================================

    /// <summary>
    /// Them/xoa cay thanh vien khoi vung benh theo danh sach tay
    /// (manager click tung cay tren map roi gan/bo vao zone).
    /// </summary>
    [HttpPatch("plant-health/disease-zones/{zoneId:guid}/memberships")]
    public async Task<IResult> UpdateZoneMemberships(
        [FromRoute] Guid farmId,
        [FromRoute] Guid zoneId,
        [FromBody] UpdateZoneMembershipsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateZoneMembershipsCommand(
            farmId,
            zoneId,
            request.AddPlantIds ?? [],
            request.RemovePlantIds ?? []);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new UpdateZoneMembershipsResponse(
                value.ZoneId,
                value.TotalEnclosedTrees,
                value.AddedCount,
                value.RemovedCount)));
    }
}
