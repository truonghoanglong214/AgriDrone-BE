using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateZone;

internal sealed class UpdateZoneHandler(
    IFarmZoneRepository farmZoneRepository,
    IFarmRepository farmRepository,
    IFarmUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateZoneCommand, Result<UpdateZoneResponse>>
{
    public async Task<Result<UpdateZoneResponse>> Handle(
        UpdateZoneCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<UpdateZoneResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<UpdateZoneResponse>(
                AuthenticationError.CurrentTenantRequired());
        }

        var accessDecision = await effectiveAccessService.CheckZoneAsync(
            actorId,
            tenantId,
            request.FarmId,
            request.ZoneId,
            FarmAccessLevel.Manager,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.AccessDenied());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmError.NotFound());
        }

        var zone = await farmZoneRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            request.ZoneId,
            cancellationToken);

        if (zone is null)
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.NotFound());
        }

        if (zone.Version != request.ExpectedVersion)
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.ConcurrentUpdate());
        }

        var name = request.Name.Trim();

        if (request.Boundary is not null &&
            farm.Boundary is not null &&
            !farm.Boundary.Covers(request.Boundary))
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.BoundaryOutsideFarm());
        }

        if (request.Boundary is not null &&
            await farmZoneRepository.ActiveBoundaryOverlapsAsync(
                tenantId,
                request.FarmId,
                request.Boundary,
                request.ZoneId,
                cancellationToken))
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.BoundaryOverlaps());
        }

        if (HasSameDetails(
                zone,
                name,
                request.Boundary,
                request.AreaHectares))
        {
            return Result.Success(ToResponse(zone));
        }

        using var oldData = CreateAuditData(zone);
    
        farmZoneRepository.Update(zone);

        var now = timeProvider.GetUtcNow();
        zone.UpdateDetails(
            name,
            request.Boundary,
            request.AreaHectares,
            now);

        using var newData = CreateAuditData(zone);

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: request.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(FarmZone),
            entityId: zone.Id,
            action: "UPDATE_DETAILS",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<UpdateZoneResponse>(
                FarmZoneError.ConcurrentUpdate());
        }

        return Result.Success(ToResponse(zone));
    }

    private static bool HasSameDetails(
        FarmZone zone,
        string name,
        Polygon? boundary,
        decimal? areaHectares) =>
        zone.Name == name &&
        GeometryEquals(zone.Boundary, boundary) &&
        zone.AreaHectares == areaHectares;

    private static bool GeometryEquals(Geometry? left, Geometry? right) =>
        ReferenceEquals(left, right) ||
        left is not null &&
        right is not null &&
        left.SRID == right.SRID &&
        left.EqualsExact(right);

    private static JsonDocument CreateAuditData(FarmZone zone) =>
        JsonSerializer.SerializeToDocument(new
        {
            zone.Code,
            zone.Name,
            BoundaryWkt = zone.Boundary?.AsText(),
            BoundarySrid = zone.Boundary?.SRID,
            zone.AreaHectares,
            zone.UpdatedAt,
            zone.Version
        });

    private static UpdateZoneResponse ToResponse(FarmZone zone) =>
        new(
            zone.Id,
            zone.FarmId,
            zone.Code,
            zone.Name,
            zone.Boundary,
            zone.AreaHectares,
            zone.Status,
            zone.Version,
            zone.UpdatedAt);
}
