using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;

internal sealed class MarkFarmBoundaryOnOrthophotoCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    ISurveyOrderBoundaryRepository boundaryRepository,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<MarkFarmBoundaryOnOrthophotoCommand,
        Result<MarkFarmBoundaryOnOrthophotoResponse>>
{
    private static readonly GeometryFactory GeometryFactory =
        new(new PrecisionModel(), 4326);

    public async Task<Result<MarkFarmBoundaryOnOrthophotoResponse>> Handle(
        MarkFarmBoundaryOnOrthophotoCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyOrderError.Forbidden());
        }

        if (command.Corners is null || command.Corners.Count != 4)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    "Exactly four corners are required to mark farm boundary on orthophoto."));
        }

        var orderedCorners = command.Corners.OrderBy(c => c.Sequence).ToList();
        for (var i = 0; i < 4; i++)
        {
            var corner = orderedCorners[i];
            if (corner.Longitude is < -180 or > 180 ||
                corner.Latitude is < -90 or > 90)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyBoundaryVerificationError.InvalidGeometry(
                        $"Corner {corner.Sequence} has invalid WGS84 coordinates: ({corner.Longitude}, {corner.Latitude})."));
            }
        }

        var coordinates = orderedCorners
            .Select(c => new Coordinate(c.Longitude, c.Latitude))
            .ToList();

        if (coordinates.DistinctBy(c => (c.X, c.Y)).Count() < 4)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    "All four corners must be distinct points."));
        }

        coordinates.Add(coordinates[0].Copy()); // Close polygon loop

        Polygon polygon;
        try
        {
            var linearRing = GeometryFactory.CreateLinearRing(coordinates.ToArray());
            polygon = GeometryFactory.CreatePolygon(linearRing);

            if (!polygon.IsValid || polygon.IsEmpty || polygon.Area <= 0)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyBoundaryVerificationError.InvalidGeometry(
                        "Four corners do not form a valid simple polygon."));
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    $"Failed to construct valid polygon from corners: {ex.Message}"));
        }

        var markedAt = timeProvider.GetUtcNow();

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => MarkWithinTransactionAsync(
                    command,
                    polygon,
                    actorId,
                    markedAt,
                    token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.UniqueViolation or
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.UniqueViolation) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.SerializationFailure) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.DeadlockDetected))
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
    }

    private async Task<Result<MarkFarmBoundaryOnOrthophotoResponse>>
        MarkWithinTransactionAsync(
            MarkFarmBoundaryOnOrthophotoCommand command,
            Polygon polygon,
            Guid actorId,
            DateTimeOffset markedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await boundaryRepository.GetForVerificationAsync(
            command.SurveyOrderId,
            actorId,
            markedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                stateResult.Error);
        }

        var order = stateResult.Value.Order;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
        }
        catch (SurveyDomainException)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyOrderError.VersionConflict());
        }

        var boundaryResult = await boundaryRepository.StageApprovedBoundaryAsync(
            order,
            polygon,
            expectedCurrentBoundaryVersionNumber: null,
            actorId,
            command.Reason ?? "Marked 4 boundary corners on Orthophoto base map.",
            command.EvidenceReference ?? "ORTHOPHOTO_BASE_MAP",
            markedAt,
            cancellationToken);
        if (boundaryResult.IsFailure)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                boundaryResult.Error);
        }

        var previousBoundaryVersionId = order.FarmBoundaryVersionId;
        order.SetFarmBoundaryFromOrthophoto(
            boundaryResult.Value.FarmBoundaryId,
            actorId,
            markedAt);

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            farmBoundaryVersionId = previousBoundaryVersionId,
            orderStatus = order.Status.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            farmBoundaryVersionId = order.FarmBoundaryVersionId,
            boundaryVersionNumber = boundaryResult.Value.VersionNumber,
            orderStatus = order.Status.ToString(),
            markedAt
        });

        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyOrder),
            order.Id,
            "MARK_FARM_BOUNDARY_ON_ORTHOPHOTO",
            oldData,
            newData,
            markedAt);

        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new MarkFarmBoundaryOnOrthophotoResponse(
            order.Id,
            boundaryResult.Value.FarmBoundaryId,
            boundaryResult.Value.VersionNumber,
            order.Version,
            markedAt));
    }
}
