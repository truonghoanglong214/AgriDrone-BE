using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Processing;
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
        var mission = await db.DroneMissions.AsNoTracking()
            .Where(value => value.Id == missionId && value.FarmId == farmId && value.TenantId == tenantId)
            .Select(value => new { value.CaptureAttemptId })
            .SingleOrDefaultAsync(cancellationToken);
        if (mission is null)
        {
            return new MissionPlantGpsReplaceResult(MissionPlantGpsReplaceStatus.NotFound);
        }
        if (mission.CaptureAttemptId is Guid attemptId &&
            await db.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value" FROM survey.capture_attempts
                WHERE id = {attemptId} AND tenant_id = {tenantId} AND farm_id = {farmId}
                  AND mapping_source_attempt_id IS NOT NULL
                """).AnyAsync(cancellationToken))
        {
            return new MissionPlantGpsReplaceResult(
                MissionPlantGpsReplaceStatus.Conflict,
                Errors: ["A disease capture uses its approved source map and cannot submit plant GPS mapping output."]);
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
        var submission = new MappingProcessingSnapshot(
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
                existingSnapshot?.MatchesSubmission(submission) == true)
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
                        submission.OutputFingerprint,
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

        var grid = MappingGridEstimator.Estimate(normalizedRows);
        var snapshot = submission with
        {
            GridBearingDeg = grid.BearingDeg ?? submission.GridBearingDeg,
            RowSpacingM = grid.RowSpacingM ?? submission.RowSpacingM,
            PlantSpacingM = grid.PlantSpacingM ?? submission.PlantSpacingM,
            SubmittedGrid = new MappingProcessingSnapshot.GridMeasurements(
                submission.GridBearingDeg, submission.RowSpacingM, submission.PlantSpacingM)
        };

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
        double.IsFinite(completion.RowSpacingM) && completion.RowSpacingM > 0 &&
        double.IsFinite(completion.PlantSpacingM) && completion.PlantSpacingM > 0 &&
        !string.IsNullOrWhiteSpace(completion.AlgorithmVersion) &&
        !string.IsNullOrWhiteSpace(completion.InputSource) &&
        completion.InputFingerprint is { Length: 64 } &&
        completion.InputFingerprint.All(Uri.IsHexDigit) &&
        completion.StartedAt.Offset == TimeSpan.Zero &&
        completion.StartedAt <= completedAt;

}
