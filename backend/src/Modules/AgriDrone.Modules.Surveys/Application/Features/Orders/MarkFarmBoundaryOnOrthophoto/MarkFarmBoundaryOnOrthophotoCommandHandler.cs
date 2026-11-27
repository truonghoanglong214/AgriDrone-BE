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

        var isCapture = command.CaptureAttemptId.HasValue;
        if (command.Corners is not { Count: >= 3 } corners ||
            (!isCapture && corners.Count != 4))
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    isCapture
                        ? "At least three vertices are required to mark a capture boundary."
                        : "Exactly four corners are required to mark farm boundary on orthophoto."));
        }

        if (corners.Any(corner => corner is null))
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    "Boundary vertices must not contain null entries."));
        }

        var orderedCorners = corners.OrderBy(c => c.Sequence).ToArray();
        if (!orderedCorners.Select(c => c.Sequence)
                .SequenceEqual(Enumerable.Range(1, orderedCorners.Length)))
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    isCapture
                        ? "Boundary vertex sequences must be consecutive and start at 1."
                        : "Corners must have sequences exactly 1, 2, 3, and 4."));
        }

        foreach (var corner in orderedCorners)
        {
            if (!double.IsFinite(corner.Longitude) || !double.IsFinite(corner.Latitude) ||
                corner.Longitude is < -180 or > 180 ||
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

        if (coordinates.DistinctBy(c => (c.X, c.Y)).Count() != coordinates.Count)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    "All boundary vertices must be distinct points."));
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
                        "Boundary vertices do not form a valid simple polygon."));
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    $"Failed to construct valid polygon from corners: {ex.Message}"));
        }

        if (!isCapture)
        {
            var areaHectares = CalculateMetricAreaHectares(polygon);
            if (areaHectares < 0.01)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyBoundaryVerificationError.InvalidGeometry(
                        $"Marked boundary area ({areaHectares:F4} ha) is below the minimum allowed threshold of 0.01 hectares (100 m²)."));
            }

            if (areaHectares > 5000.0)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyBoundaryVerificationError.InvalidGeometry(
                        $"Marked boundary area ({areaHectares:F2} ha) exceeds the maximum allowed threshold of 5000 hectares."));
            }
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

        var evidenceReference = command.EvidenceReference ?? "ORTHOPHOTO_BASE_MAP";
        if (command.CaptureAttemptId is { } captureAttemptId)
        {
            if (order.Scope != ContractScope.V3 || !order.RequiresBaselineMapping ||
                order.Status != SurveyOrderStatus.Processing || command.ExpectedMissionVersion is not { } version)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyOrderError.InvalidTransition("A V3 First order in Processing and an expected Mission version are required."));
            }

            var evidence = await boundaryRepository.FindCaptureOrthophotoAsync(
                order, captureAttemptId, version, cancellationToken);
            if (evidence is not { } mediaId)
            {
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    AppError.Conflict("CaptureBoundary.OrthophotoUnavailable",
                        "The active Capture Mission or its orthophoto has changed. Reload and verify the evidence."));
            }
            evidenceReference = $"mission-media:{mediaId:D}";
        }
        else if (order.Scope == ContractScope.V3 ||
                 order.Status is not (SurveyOrderStatus.AwaitingBaselineReview or SurveyOrderStatus.PendingBoundaryVerification))
        {
            return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                SurveyOrderError.InvalidTransition(
                    $"Order in state '{order.Status}' cannot set farm boundary through this route."));
        }

        var boundaryResult = await boundaryRepository.StageApprovedBoundaryAsync(
            order,
            polygon,
            command.ExpectedBoundaryVersionNumber,
            actorId,
            command.Reason ?? $"Marked {polygon.NumPoints - 1} boundary corners on Orthophoto base map.",
            evidenceReference,
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
            captureAttemptId = command.CaptureAttemptId,
            evidenceReference,
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

        uint? missionVersion = null;
        if (command.CaptureAttemptId is { } attemptId && command.ExpectedMissionVersion is { } expectedMissionVersion)
        {
            missionVersion = await boundaryRepository.BindCaptureBoundaryAsync(
                order, attemptId, expectedMissionVersion, boundaryResult.Value.FarmBoundaryId, markedAt, cancellationToken);
            if (missionVersion is null)
                return Result.Failure<MarkFarmBoundaryOnOrthophotoResponse>(
                    SurveyBoundaryVerificationError.PersistenceConflict());
        }

        return Result.Success(new MarkFarmBoundaryOnOrthophotoResponse(
            order.Id,
            boundaryResult.Value.FarmBoundaryId,
            boundaryResult.Value.VersionNumber,
            order.Version,
            markedAt,
            missionVersion));
    }

    private static double CalculateMetricAreaHectares(Polygon polygon)
    {
        var coords = polygon.ExteriorRing.Coordinates;
        if (coords.Length < 4)
        {
            return 0.0;
        }

        var refLat = 0.0;
        for (var i = 0; i < coords.Length - 1; i++)
        {
            refLat += coords[i].Y;
        }
        refLat /= (coords.Length - 1);

        var latRad = refLat * Math.PI / 180.0;
        const double metersPerDegreeLat = 111_320.0;
        var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(latRad);

        var areaSquareMeters = 0.0;
        for (var i = 0; i < coords.Length - 1; i++)
        {
            var x1 = coords[i].X * metersPerDegreeLon;
            var y1 = coords[i].Y * metersPerDegreeLat;
            var x2 = coords[i + 1].X * metersPerDegreeLon;
            var y2 = coords[i + 1].Y * metersPerDegreeLat;
            areaSquareMeters += (x1 * y2) - (x2 * y1);
        }

        areaSquareMeters = Math.Abs(areaSquareMeters) / 2.0;
        return areaSquareMeters / 10_000.0;
    }
}
