using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionPlantGpsStore(
    MissionsDbContext db,
    TimeProvider timeProvider) : IMissionPlantGpsStore
{
    public async Task<IReadOnlyList<MissionPlantGpsRow>?> GetAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        CancellationToken cancellationToken)
    {
        var missionExists = await db.DroneMissions.AsNoTracking().AnyAsync(
            mission => mission.Id == missionId && mission.FarmId == farmId &&
                mission.TenantId == tenantId, cancellationToken);
        if (!missionExists)
            return null;

        return await db.Database.SqlQueryRaw<MissionPlantGpsRow>(
                "SELECT tree_id AS \"TreeId\", row_number AS \"Row\", " +
                "col_number AS \"Col\", latitude AS \"Latitude\", " +
                "longitude AS \"Longitude\", confidence AS \"Confidence\", " +
                "status AS \"Status\", source AS \"Source\" " +
                "FROM mission.mission_plant_gps WHERE mission_id = {0} " +
                "ORDER BY row_number, col_number, tree_id",
                missionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<MissionPlantGpsReplaceResult> ReplaceAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        IReadOnlyList<MissionPlantGpsRow> rows,
        MappingProcessingCompletion completion,
        CancellationToken cancellationToken)
    {
        var missionExists = await db.DroneMissions.AsNoTracking().AnyAsync(
            mission => mission.Id == missionId && mission.FarmId == farmId &&
                mission.TenantId == tenantId, cancellationToken);
        if (!missionExists)
        {
            return new MissionPlantGpsReplaceResult(MissionPlantGpsReplaceStatus.NotFound);
        }

        var completedAt = timeProvider.GetUtcNow();
        if (!IsValidCompletion(completion, completedAt))
        {
            return new MissionPlantGpsReplaceResult(
                MissionPlantGpsReplaceStatus.Invalid,
                Errors: ["Mapping processing completion metadata is incomplete or invalid."]);
        }

        var modelExists = await db.AiModelVersions.AsNoTracking().AnyAsync(
            model => model.Id == completion.ModelVersionId &&
                     model.ModelType == AiModelType.PlantDetection &&
                     model.IsActive,
            cancellationToken);
        if (!modelExists)
        {
            return new MissionPlantGpsReplaceResult(
                MissionPlantGpsReplaceStatus.Conflict,
                Errors: ["The active mapping model version was not found."]);
        }

        var normalizedRows = rows.Select(row => row with
        {
            Latitude = decimal.Round(row.Latitude, 12, MidpointRounding.ToEven),
            Longitude = decimal.Round(row.Longitude, 12, MidpointRounding.ToEven),
            Confidence = decimal.Round(row.Confidence, 15, MidpointRounding.ToEven)
        }).ToArray();
        var outputFingerprint = MissionPlantGpsFingerprint.Compute(normalizedRows);
        var snapshot = new MappingProcessingSnapshot(
            completion.SourceVersion,
            completion.GridBearingDeg,
            completion.RowSpacingM,
            completion.PlantSpacingM,
            completion.AlgorithmVersion.Trim(),
            completion.InputFingerprint.ToLowerInvariant(),
            outputFingerprint);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({completion.ClientOperationId}::text, 0))",
            cancellationToken);

        var existingJob = await db.AiProcessingJobs.AsNoTracking()
            .SingleOrDefaultAsync(
                job => job.ClientOperationId == completion.ClientOperationId,
                cancellationToken);
        if (existingJob is not null)
        {
            MappingProcessingSnapshot? existingSnapshot;
            try
            {
                existingSnapshot = existingJob.Parameters.Deserialize<MappingProcessingSnapshot>();
            }
            catch (JsonException)
            {
                existingSnapshot = null;
            }

            if (existingJob.MissionId == missionId &&
                existingJob.ModelVersionId == completion.ModelVersionId &&
                existingJob.JobType == AiJobType.Mapping &&
                existingJob.Status == AiJobStatus.Completed &&
                existingSnapshot == snapshot)
            {
                var currentRows = await db.Database.SqlQueryRaw<MissionPlantGpsRow>(
                        "SELECT tree_id AS \"TreeId\", row_number AS \"Row\", " +
                        "col_number AS \"Col\", latitude AS \"Latitude\", " +
                        "longitude AS \"Longitude\", confidence AS \"Confidence\", " +
                        "status AS \"Status\", source AS \"Source\" " +
                        "FROM mission.mission_plant_gps WHERE mission_id = {0}",
                        missionId)
                    .ToListAsync(cancellationToken);
                if (string.Equals(
                        MissionPlantGpsFingerprint.Compute(currentRows),
                        snapshot.OutputFingerprint,
                        StringComparison.Ordinal))
                {
                    return new MissionPlantGpsReplaceResult(
                        MissionPlantGpsReplaceStatus.Saved,
                        existingJob.Id);
                }
            }

            return new MissionPlantGpsReplaceResult(
                MissionPlantGpsReplaceStatus.Conflict,
                Errors: ["ClientOperationId was already used for a different mapping import."]);
        }

        using var inputManifest = JsonSerializer.SerializeToDocument(new
        {
            source = completion.InputSource.Trim(),
            fingerprint = snapshot.InputFingerprint
        });
        using var outputManifest = JsonSerializer.SerializeToDocument(new
        {
            rowCount = normalizedRows.Length,
            fingerprint = snapshot.OutputFingerprint
        });
        var processingJob = AiProcessingJob.RecordCompletedMappingRun(
            missionId,
            completion.ModelVersionId,
            snapshot,
            inputManifest,
            outputManifest,
            completion.StartedAt,
            completedAt,
            completion.ClientOperationId);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM mission.mission_plant_gps WHERE mission_id = {missionId}",
            cancellationToken);

        foreach (var row in normalizedRows)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO mission.mission_plant_gps (mission_id, tree_id, row_number, col_number, latitude, longitude, confidence, status, source) VALUES ({missionId}, {row.TreeId}, {row.Row}, {row.Col}, {row.Latitude}, {row.Longitude}, {row.Confidence}, {row.Status}, {row.Source})",
                cancellationToken);
        }

        db.AiProcessingJobs.Add(processingJob);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MissionPlantGpsReplaceResult(
            MissionPlantGpsReplaceStatus.Saved,
            processingJob.Id);
    }

    private static bool IsValidCompletion(
        MappingProcessingCompletion completion,
        DateTimeOffset completedAt) =>
        completion.ClientOperationId != Guid.Empty &&
        completion.ModelVersionId != Guid.Empty &&
        completion.SourceVersion >= 1 &&
        completion.GridBearingDeg is >= 0 and < 360 &&
        completion.RowSpacingM > 0 &&
        completion.PlantSpacingM > 0 &&
        !string.IsNullOrWhiteSpace(completion.AlgorithmVersion) &&
        !string.IsNullOrWhiteSpace(completion.InputSource) &&
        completion.InputFingerprint is { Length: 64 } &&
        completion.InputFingerprint.All(Uri.IsHexDigit) &&
        completion.StartedAt.Offset == TimeSpan.Zero &&
        completion.StartedAt <= completedAt;

}
