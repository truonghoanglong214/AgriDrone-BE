using System.Text.Json;
using AgriDrone.IntegrationContracts.Contracts;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Mapping;

internal sealed class MappingCandidatesApprovedHandler(
    MappingPublicationDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator,
    IEffectiveAccessService accessService,
    OutboxMessageFactory outboxMessageFactory,
    IPlantReferenceCache plantReferenceCache,
    IAuditWriter auditWriter,
    TimeProvider timeProvider)
    : IIntegrationMessageHandler<MappingCandidatesApprovedV1>
{
    private const string UnknownHealthCode = "UNKNOWN";

    public async Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<MappingCandidatesApprovedV1> envelope,
        CancellationToken cancellationToken)
    {
        var result = await inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be1MappingCandidatesApprovedV1,
            envelope,
            (context, token) => PublishMapAsync(
                context,
                envelope,
                token),
            cancellationToken);

        if (result.Disposition ==
            IntegrationMessageDisposition.Acknowledge)
        {
            await plantReferenceCache.InvalidateZoneAsync(
                envelope.TenantId,
                envelope.Payload.FarmId,
                envelope.Payload.ZoneId,
                cancellationToken);
        }

        return result;
    }

    private async Task<InboxHandlerResult> PublishMapAsync(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<MappingCandidatesApprovedV1> envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.ActorId is not Guid actorId)
        {
            return Permanent(
                MappingPublicationErrorCodes.ActorRequired,
                "Mapping publication requires the approving actor.");
        }

        var payload = envelope.Payload;
        var duplicateApproval = await context.ZoneMapVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                mapVersion =>
                    mapVersion.SourceApprovalId == payload.ApprovalId,
                cancellationToken);
        if (duplicateApproval is not null)
        {
            if (duplicateApproval.SourceMissionId != payload.MissionId ||
                duplicateApproval.FarmId != payload.FarmId ||
                duplicateApproval.ZoneId != payload.ZoneId)
            {
                return Permanent(
                    MappingPublicationErrorCodes.ApprovalAlreadyUsed,
                    "ApprovalId is already associated with another mapping publication.");
            }

            return InboxHandlerResult.Completed(
                SerializeResult(
                    duplicateApproval.Id,
                    duplicateApproval.VersionNumber));
        }

        var sourceValidation = await ValidateSourceAsync(
            context,
            envelope,
            actorId,
            cancellationToken);
        if (sourceValidation is not null)
        {
            return sourceValidation;
        }

        if (!TryConvertMeasurements(payload, out var measurements))
        {
            return Permanent(
                MappingPublicationErrorCodes.MeasurementOutOfRange,
                "Mapping measurements exceed the supported database precision.");
        }

        var currentMap = await context.ZoneMapVersions
            .SingleOrDefaultAsync(
                mapVersion =>
                    mapVersion.ZoneId == payload.ZoneId &&
                    mapVersion.Status == MapVersionStatus.Confirmed,
                cancellationToken);

        if (currentMap?.Id != payload.ExpectedCurrentMapVersionId)
        {
            return Permanent(
                MappingPublicationErrorCodes.SnapshotStale,
                "ExpectedCurrentMapVersionId no longer matches the current published map.");
        }

        var nextVersion = (await context.ZoneMapVersions
            .Where(mapVersion => mapVersion.ZoneId == payload.ZoneId)
            .MaxAsync(
                mapVersion => (int?)mapVersion.VersionNumber,
                cancellationToken) ?? 0) + 1;

        var actionableCandidates = payload.Candidates
            .Where(candidate => candidate.Decision is
                MappingCandidateDecisions.Matched or
                MappingCandidateDecisions.CreateNew)
            .ToArray();
        var matchedIds = actionableCandidates
            .Where(candidate =>
                candidate.Decision == MappingCandidateDecisions.Matched)
            .Select(candidate => candidate.ResolvedPlantId!.Value)
            .ToArray();
        var matchedPlants = await context.Plants
            .Where(plant => matchedIds.Contains(plant.Id))
            .ToDictionaryAsync(plant => plant.Id, cancellationToken);

        var plantValidation = ValidateMatchedPlants(
            payload,
            matchedIds,
            matchedPlants);
        if (plantValidation is not null)
        {
            return plantValidation;
        }

        var unknownHealthId = await context.HealthLevels
            .Where(level =>
                level.Code == UnknownHealthCode && level.IsActive)
            .Select(level => (Guid?)level.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!unknownHealthId.HasValue)
        {
            return Permanent(
                MappingPublicationErrorCodes.UnknownHealthMissing,
                "The active UNKNOWN health level is not configured.");
        }

        var now = timeProvider.GetUtcNow();
        var mapVersionId = Guid.NewGuid();
        using var parameters = JsonSerializer.SerializeToDocument(
            payload.Parameters);
        var newMap = ZoneMapVersion.CreateDraft(
            mapVersionId,
            payload.FarmId,
            payload.ZoneId,
            payload.MissionId,
            payload.ApprovalId,
            nextVersion,
            measurements.GridBearingDeg,
            measurements.RowSpacingM,
            measurements.PlantSpacingM,
            payload.AlgorithmVersion,
            parameters,
            now);

        context.ZoneMapVersions.Add(newMap);
        await context.SaveChangesAsync(cancellationToken);

        var previousPositions = matchedPlants.Values.ToDictionary(
            plant => plant.Id,
            plant => PlantPositionSnapshot.From(plant));
        foreach (var plant in matchedPlants.Values)
        {
            plant.ClearGridPositionForRemap(now);
        }

        if (matchedPlants.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        var plantMappings = new List<PlantMappingV1>(
            actionableCandidates.Length);
        foreach (var candidate in actionableCandidates)
        {
            var location = CreatePoint(
                candidate.Longitude,
                candidate.Latitude);
            decimal? accuracy = candidate.LocationAccuracyM.HasValue
                ? decimal.Round(
                    (decimal)candidate.LocationAccuracyM.Value,
                    3)
                : null;
            var confidence = decimal.Round(
                (decimal)candidate.PositionConfidence,
                4);

            if (candidate.Decision == MappingCandidateDecisions.Matched)
            {
                var plant = matchedPlants[candidate.ResolvedPlantId!.Value];
                var previous = previousPositions[plant.Id];
                plant.ApplyPublishedMapPosition(
                    mapVersionId,
                    location,
                    candidate.RowIndex,
                    candidate.ColumnIndex,
                    accuracy,
                    confidence,
                    now);

                if (previous.IsDifferentFrom(
                    location,
                    candidate.RowIndex,
                    candidate.ColumnIndex))
                {
                    context.PlantChangeEvents.Add(
                        PlantChangeEvent.MappingPositionChanged(
                            payload.FarmId,
                            payload.MissionId,
                            plant.Id,
                            previous.Location,
                            location,
                            previous.RowIndex,
                            candidate.RowIndex,
                            previous.ColumnIndex,
                            candidate.ColumnIndex,
                            plant.LifecycleStatus,
                            actorId,
                            now));
                }

                plantMappings.Add(
                    new PlantMappingV1(
                        candidate.ObservationId,
                        plant.Id,
                        WasCreated: false));
                continue;
            }

            var plantId = Guid.NewGuid();
            var plantCode = $"MAP-{candidate.ObservationId:N}";
            var newPlant = Plant.CreateFromMapping(
                plantId,
                payload.FarmId,
                payload.ZoneId,
                plantCode,
                location,
                mapVersionId,
                candidate.RowIndex,
                candidate.ColumnIndex,
                accuracy,
                confidence,
                unknownHealthId.Value,
                payload.MissionId,
                now);
            context.Plants.Add(newPlant);
            context.PlantChangeEvents.Add(
                PlantChangeEvent.MappingCreated(
                    payload.FarmId,
                    payload.MissionId,
                    plantId,
                    location,
                    candidate.RowIndex,
                    candidate.ColumnIndex,
                    actorId,
                    now));
            plantMappings.Add(
                new PlantMappingV1(
                    candidate.ObservationId,
                    plantId,
                    WasCreated: true));
        }

        await context.SaveChangesAsync(cancellationToken);

        if (currentMap is not null)
        {
            currentMap.Supersede();
            await context.SaveChangesAsync(cancellationToken);
        }

        newMap.Confirm(actorId, now);
        AddZoneMapPublishedOutbox(
            context,
            envelope,
            newMap,
            plantMappings,
            now);
        AddAudit(
            context,
            envelope,
            actorId,
            newMap,
            currentMap,
            plantMappings,
            now);

        return InboxHandlerResult.Completed(
            SerializeResult(mapVersionId, nextVersion));
    }

    private async Task<InboxHandlerResult?> ValidateSourceAsync(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<MappingCandidatesApprovedV1> envelope,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        var farm = await context.Farms
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == payload.FarmId &&
                    candidate.TenantId == envelope.TenantId,
                cancellationToken);
        if (farm is null)
        {
            return Permanent(
                MappingPublicationErrorCodes.FarmNotFound,
                "Farm was not found in the message tenant.");
        }

        if (farm.Status != GeneralStatus.Active || farm.DeletedAt is not null)
        {
            return Permanent(
                MappingPublicationErrorCodes.FarmInactive,
                "Farm is inactive.");
        }

        var zone = await context.FarmZones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == payload.ZoneId &&
                    candidate.FarmId == payload.FarmId,
                cancellationToken);
        if (zone is null)
        {
            return Permanent(
                MappingPublicationErrorCodes.ZoneNotFound,
                "Zone was not found in the message farm.");
        }

        if (zone.Status != GeneralStatus.Active || zone.DeletedAt is not null)
        {
            return Permanent(
                MappingPublicationErrorCodes.ZoneInactive,
                "Zone is inactive.");
        }

        var access = await accessService.CheckZoneAsync(
            actorId,
            envelope.TenantId,
            payload.FarmId,
            payload.ZoneId,
            FarmAccessLevel.Manager,
            cancellationToken);
        if (!access.IsAllowed)
        {
            return Permanent(
                MappingPublicationErrorCodes.AccessDenied,
                $"Actor is not allowed to publish this Zone map: {access.Reason}.");
        }

        var mission = await context.MissionPublicationStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == payload.MissionId,
                cancellationToken);
        if (mission is null)
        {
            return Permanent(
                MappingPublicationErrorCodes.MissionNotFound,
                "Source Mission was not found.");
        }

        if (mission.TenantId != envelope.TenantId ||
            mission.FarmId != payload.FarmId ||
            mission.ZoneId != payload.ZoneId ||
            mission.MissionType != MissionType.Mapping ||
            mission.Status != MissionStatus.Completed ||
            mission.ProcessingStatus != ProcessingStatus.ReviewRequired)
        {
            return Permanent(
                MappingPublicationErrorCodes.MissionInvalid,
                "Source Mission is not a completed mapping flight awaiting review in the same tenant/Farm/Zone.");
        }

        return null;
    }

    private static InboxHandlerResult? ValidateMatchedPlants(
        MappingCandidatesApprovedV1 payload,
        Guid[] matchedIds,
        IReadOnlyDictionary<Guid, Plant> matchedPlants)
    {
        if (matchedPlants.Count != matchedIds.Length)
        {
            return Permanent(
                MappingPublicationErrorCodes.PlantNotFound,
                "One or more resolved Plants were not found.");
        }

        if (matchedPlants.Values.Any(plant =>
                plant.FarmId != payload.FarmId ||
                plant.ZoneId != payload.ZoneId ||
                plant.LifecycleStatus != PlantLifecycleStatus.Active))
        {
            return Permanent(
                MappingPublicationErrorCodes.PlantInvalid,
                "Resolved Plants must be active and belong to the message Farm/Zone.");
        }

        return null;
    }

    private void AddZoneMapPublishedOutbox(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<MappingCandidatesApprovedV1> source,
        ZoneMapVersion mapVersion,
        IReadOnlyList<PlantMappingV1> plantMappings,
        DateTimeOffset publishedAt)
    {
        var payload = source.Payload;
        var published = new ZoneMapPublishedV1(
            source.MessageId,
            payload.ApprovalId,
            payload.MissionId,
            payload.FarmId,
            payload.ZoneId,
            mapVersion.Id,
            mapVersion.VersionNumber,
            publishedAt,
            plantMappings);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.ZoneMapPublishedV1,
            Guid.NewGuid(),
            source.CorrelationId,
            source.TenantId,
            source.ActorId,
            publishedAt,
            published);

        context.OutboxMessages.Add(
            outboxMessageFactory.Create(
                envelope,
                IntegrationEventTypes.ZoneMapPublishedV1,
                payload.ZoneId.ToString("D")));
    }

    private void AddAudit(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<MappingCandidatesApprovedV1> source,
        Guid actorId,
        ZoneMapVersion mapVersion,
        ZoneMapVersion? previousMap,
        List<PlantMappingV1> plantMappings,
        DateTimeOffset createdAt)
    {
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            PreviousMapVersionId = previousMap?.Id,
            PreviousVersionNumber = previousMap?.VersionNumber
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            MapVersionId = mapVersion.Id,
            mapVersion.VersionNumber,
            mapVersion.SourceApprovalId,
            PlantCount = plantMappings.Count,
            CreatedPlantCount = plantMappings.Count(mapping =>
                mapping.WasCreated)
        });
        auditWriter.AddUserAction(
            context,
            source.TenantId,
            source.Payload.FarmId,
            actorId,
            source.CorrelationId,
            "ZoneMapVersion",
            mapVersion.Id,
            "PUBLISH",
            oldData,
            newData,
            createdAt);
    }

    private static bool TryConvertMeasurements(
        MappingCandidatesApprovedV1 payload,
        out MappingMeasurements measurements)
    {
        const double maximumSpacing = 99_999.999;
        const double maximumAccuracy = 99_999.999;

        if (payload.RowSpacingM > maximumSpacing ||
            payload.PlantSpacingM > maximumSpacing ||
            payload.Candidates.Any(candidate =>
                candidate.LocationAccuracyM > maximumAccuracy))
        {
            measurements = default;
            return false;
        }

        measurements = new MappingMeasurements(
            decimal.Round((decimal)payload.GridBearingDeg, 2),
            decimal.Round((decimal)payload.RowSpacingM, 3),
            decimal.Round((decimal)payload.PlantSpacingM, 3));
        return true;
    }

    private static Point CreatePoint(double longitude, double latitude) =>
        new(longitude, latitude)
        {
            SRID = 4326
        };

    private static InboxHandlerResult Permanent(
        string code,
        string error) =>
        InboxHandlerResult.PermanentFailure(code, error);

    private static string SerializeResult(
        Guid mapVersionId,
        int versionNumber) =>
        JsonSerializer.Serialize(new
        {
            MapVersionId = mapVersionId,
            VersionNumber = versionNumber
        });

    private readonly record struct MappingMeasurements(
        decimal GridBearingDeg,
        decimal RowSpacingM,
        decimal PlantSpacingM);

    private sealed record PlantPositionSnapshot(
        Point? Location,
        int? RowIndex,
        int? ColumnIndex)
    {
        public static PlantPositionSnapshot From(Plant plant) =>
            new(
                plant.Location is null
                    ? null
                    : CreatePoint(plant.Location.X, plant.Location.Y),
                plant.RowIndex,
                plant.ColumnIndex);

        public bool IsDifferentFrom(
            Point location,
            int rowIndex,
            int columnIndex) =>
            Location is null ||
            !Location.EqualsExact(location) ||
            RowIndex != rowIndex ||
            ColumnIndex != columnIndex;
    }
}
