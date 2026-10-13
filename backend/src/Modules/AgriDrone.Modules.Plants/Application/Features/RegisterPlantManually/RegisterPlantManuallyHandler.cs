using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;

internal sealed class RegisterPlantManuallyHandler(
    IPlantRepository plantRepository,
    IPlantsUnitOfWork unitOfWork,
    IMissionPlanningReferenceQuery farmReferenceQuery,
    IHealthLevelReferenceQuery healthLevelReferenceQuery,
    IEffectiveAccessService effectiveAccessService,
    IAuditWriter auditWriter,
    IPlantReferenceCache plantReferenceCache,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        RegisterPlantManuallyCommand,
        Result<RegisterPlantManuallyResponse>>
{
    private const string PlantCodeConstraint =
        "uq_plants_farm_code";
    private const string ActiveGridPositionConstraint =
        "ux_plants_active_zone_grid_position";

    public async Task<Result<RegisterPlantManuallyResponse>> Handle(
        RegisterPlantManuallyCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.CurrentTenantRequired());
        }

        var zoneExists = await farmReferenceQuery.IsActiveZoneAsync(
            tenantId,
            request.FarmId,
            request.ZoneId,
            cancellationToken);
        if (!zoneExists)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.ZoneNotFound());
        }

        var access = await effectiveAccessService.CheckZoneAsync(
            actorId,
            tenantId,
            request.FarmId,
            request.ZoneId,
            FarmAccessLevel.Manager,
            cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.AccessDenied());
        }

        if (request.MapVersionId is Guid mapVersionId)
        {
            var mapExists =
                await farmReferenceQuery.IsConfirmedMapVersionAsync(
                    tenantId,
                    request.FarmId,
                    request.ZoneId,
                    mapVersionId,
                    cancellationToken);
            if (!mapExists)
            {
                return Result.Failure<RegisterPlantManuallyResponse>(
                    PlantRegistrationError.MapVersionNotFound());
            }
        }

        var normalizedCode = request.PlantCode.Trim().ToUpperInvariant();
        var codeExists = await plantRepository.CodeExistsAsync(
            request.FarmId,
            normalizedCode,
            cancellationToken);
        if (codeExists)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.CodeAlreadyExists(normalizedCode));
        }

        if (request.RowIndex is int rowIndex &&
            request.ColumnIndex is int columnIndex)
        {
            var gridPositionExists =
                await plantRepository.GridPositionExistsAsync(
                    request.ZoneId,
                    rowIndex,
                    columnIndex,
                    cancellationToken);
            if (gridPositionExists)
            {
                return Result.Failure<RegisterPlantManuallyResponse>(
                    PlantRegistrationError.GridPositionOccupied(
                        rowIndex,
                        columnIndex));
            }
        }

        var unknownHealthLevelId =
            await healthLevelReferenceQuery.GetActiveUnknownIdAsync(
                cancellationToken);
        if (!unknownHealthLevelId.HasValue)
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.UnknownHealthLevelMissing());
        }

        var now = timeProvider.GetUtcNow();
        var plant = Plant.RegisterManually(
            Guid.NewGuid(),
            request.FarmId,
            request.ZoneId,
            normalizedCode,
            request.Location,
            request.MapVersionId,
            request.RowIndex,
            request.ColumnIndex,
            request.LocationAccuracyM,
            unknownHealthLevelId.Value,
            now);
        var changeEvent = PlantChangeEvent.ManualRegistered(
            request.FarmId,
            plant.Id,
            request.Location,
            request.RowIndex,
            request.ColumnIndex,
            actorId,
            request.Reason,
            now);

        plantRepository.Add(plant);
        plantRepository.AddChangeEvent(changeEvent);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            plant.ZoneId,
            plant.PlantCode,
            Latitude = plant.Location!.Y,
            Longitude = plant.Location.X,
            MapVersionId = plant.CurrentMapVersionId,
            plant.RowIndex,
            plant.ColumnIndex,
            plant.LocationAccuracyM,
            PositionSource = plant.PositionSource!.Value.ToString(),
            LifecycleStatus = plant.LifecycleStatus.ToString(),
            plant.CurrentHealthLevelId,
            Reason = changeEvent.Notes
        });
        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: plant.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(Plant),
            entityId: plant.Id,
            action: "REGISTER_MANUALLY",
            oldData: null,
            newData: auditData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                PlantCodeConstraint))
        {
            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.CodeAlreadyExists(normalizedCode));
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                ActiveGridPositionConstraint))
        {
            if (request.RowIndex is not int conflictedRowIndex ||
                request.ColumnIndex is not int conflictedColumnIndex)
            {
                throw;
            }

            return Result.Failure<RegisterPlantManuallyResponse>(
                PlantRegistrationError.GridPositionOccupied(
                    conflictedRowIndex,
                    conflictedColumnIndex));
        }

        if (plant.CurrentMapVersionId.HasValue)
        {
            await plantReferenceCache.InvalidateZoneAsync(
                tenantId,
                plant.FarmId,
                plant.ZoneId!.Value,
                cancellationToken);
        }

        return Result.Success(RegisterPlantManuallyResponse.From(plant));
    }
}
