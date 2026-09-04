using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;

internal sealed class UpdateFarmDetailHandler(
    IFarmRepository farmRepository,
    IFarmUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateFarmDetailCommand, Result<UpdateFarmDetailResponse>>
{
    public async Task<Result<UpdateFarmDetailResponse>> Handle(
        UpdateFarmDetailCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<UpdateFarmDetailResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<UpdateFarmDetailResponse>(
                AuthenticationError.CurrentTenantRequired());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            request.farmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure<UpdateFarmDetailResponse>(FarmError.NotFound());
        }

        var accessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure<UpdateFarmDetailResponse>(FarmError.AccessDenied());
        }

        if (farm.Version != request.expectedVersion)
        {
            return Result.Failure<UpdateFarmDetailResponse>(
                FarmError.ConcurrentUpdate());
        }

        var name = request.name.Trim();
        var address = string.IsNullOrWhiteSpace(request.address)
            ? null
            : request.address.Trim();

        if (HasSameDetails(farm, name, address, request))
        {
            return Result.Success(ToResponse(farm));
        }

        using var oldData = CreateAuditData(farm);

        farmRepository.Update(farm);

        var now = timeProvider.GetUtcNow();
        farm.UpdateDetails(
            name,
            address,
            request.boundary,
            request.centerPoint,
            request.areaHectares,
            now);

        using var newData = CreateAuditData(farm);

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: farm.Id,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: "Farm",
            entityId: farm.Id,
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
            return Result.Failure<UpdateFarmDetailResponse>(
                FarmError.ConcurrentUpdate());
        }

        return Result.Success(ToResponse(farm));
    }

    private static bool HasSameDetails(
        Farm farm,
        string name,
        string? address,
        UpdateFarmDetailCommand request) =>
        farm.Name == name &&
        farm.Address == address &&
        GeometryEquals(farm.Boundary, request.boundary) &&
        GeometryEquals(farm.CenterPoint, request.centerPoint) &&
        farm.AreaHectares == request.areaHectares;

    private static bool GeometryEquals(Geometry? left, Geometry? right) =>
        ReferenceEquals(left, right) ||
        left is not null &&
        right is not null &&
        left.SRID == right.SRID &&
        left.EqualsExact(right);

    private static JsonDocument CreateAuditData(Farm farm) =>
        JsonSerializer.SerializeToDocument(new
        {
            farm.Name,
            farm.Address,
            BoundaryWkt = farm.Boundary?.AsText(),
            BoundarySrid = farm.Boundary?.SRID,
            CenterPointWkt = farm.CenterPoint?.AsText(),
            CenterPointSrid = farm.CenterPoint?.SRID,
            farm.AreaHectares,
            farm.UpdatedAt,
            farm.Version
        });

    private static UpdateFarmDetailResponse ToResponse(Farm farm) =>
        new(
            farm.Id,
            farm.TenantId,
            farm.Code,
            farm.Name,
            farm.Address,
            farm.Boundary,
            farm.CenterPoint,
            farm.AreaHectares,
            farm.Status,
            farm.UpdatedAt,
            farm.Version);
}
