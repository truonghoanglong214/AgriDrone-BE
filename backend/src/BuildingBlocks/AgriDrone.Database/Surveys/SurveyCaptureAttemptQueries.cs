using System.Data;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyCaptureAttemptQueries(
    AgriDroneSchemaDbContext db,
    SurveyResultReviewTransaction resultReview) : ISurveyCaptureAttemptQueries
{
    public async Task<Result<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>> GetManagerCaptureAttemptsAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                AppError.Validation("OrderId.Required", "OrderId is required."));
        }

        if (actorId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                AppError.Unauthorized("CaptureAttempts.Unauthorized", "Valid actor is required."));
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var orderScope = await (
                from order in db.Set<SurveyOrder>().AsNoTracking()
                join farm in db.Set<Farm>().AsNoTracking()
                    on new { order.TenantId, order.FarmId }
                    equals new { farm.TenantId, FarmId = farm.Id }
                where order.Id == orderId && farm.DeletedAt == null
                select new
                {
                    order.Id,
                    order.TenantId,
                    order.FarmId,
                    order.Scope,
                    order.Version
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (orderScope is null)
            {
                return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                    AppError.NotFound("Order.NotFound", "Survey order was not found."));
            }

            // ADR 0020 Rank 1: Verify active assigned Manager BEFORE reading capture attempts.
            if (!await resultReview.IsAssignedManagerAsync(
                    orderScope.TenantId,
                    orderScope.FarmId,
                    actorId,
                    cancellationToken))
            {
                return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                    AppError.Forbidden(
                        "CaptureAttempts.ManagerForbidden",
                        "The actor is not an active assigned Manager for this farm."));
            }

            if (orderScope.Scope != ContractScope.V3)
            {
                return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                    AppError.NotFound("Order.NotFound", "Survey order was not found."));
            }

            var attempts = await db.Set<CaptureAttempt>().AsNoTracking()
                .Where(attempt =>
                    attempt.OrderId == orderScope.Id &&
                    attempt.TenantId == orderScope.TenantId &&
                    attempt.FarmId == orderScope.FarmId)
                .OrderBy(attempt => attempt.AttemptIndex)
                .ThenBy(attempt => attempt.Id)
                .Select(attempt => new
                {
                    attempt.Id,
                    attempt.AttemptIndex,
                    attempt.Status,
                    attempt.Version,
                    attempt.SupersededAttemptId,
                    attempt.MappingSourceAttemptId,
                    attempt.ParentMissionId,
                    attempt.StartedAt,
                    attempt.EndedAt,
                    attempt.UploadManifestReference
                })
                .ToListAsync(cancellationToken);

            if (attempts.Count == 0)
            {
                await tx.CommitAsync(cancellationToken);
                return Result.Success<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>([]);
            }

            var attemptIds = attempts.Select(a => a.Id).ToList();

            var missionsByAttempt = await db.Set<DroneMission>().AsNoTracking()
                .Where(mission =>
                    mission.SurveyOrderId == orderScope.Id &&
                    mission.TenantId == orderScope.TenantId &&
                    mission.FarmId == orderScope.FarmId &&
                    mission.CaptureAttemptId.HasValue &&
                    attemptIds.Contains(mission.CaptureAttemptId.Value))
                .Select(mission => new
                {
                    AttemptId = mission.CaptureAttemptId!.Value,
                    MissionId = mission.Id
                })
                .ToDictionaryAsync(x => x.AttemptId, x => x.MissionId, cancellationToken);

            var manifestIds = attempts
                .Select(attempt => Guid.TryParse(attempt.UploadManifestReference, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToArray();
            var manifests = await (
                from manifest in db.Set<CaptureInputManifest>().AsNoTracking()
                join job in db.Set<AiProcessingJob>().AsNoTracking().Where(job => job.JobType == AiJobType.Mapping)
                    on new { OperationId = (Guid?)manifest.Id, manifest.MissionId }
                    equals new { OperationId = job.ClientOperationId, job.MissionId } into jobs
                from job in jobs.DefaultIfEmpty()
                where manifestIds.Contains(manifest.Id) &&
                      manifest.SurveyOrderId == orderScope.Id &&
                      manifest.TenantId == orderScope.TenantId && manifest.FarmId == orderScope.FarmId &&
                      (manifest.Status == CaptureInputManifestStatus.Uploaded || manifest.Status == CaptureInputManifestStatus.Approved)
                select new
                {
                    manifest.Id, manifest.CaptureAttemptId, manifest.ManifestFingerprint,
                    ProcessingJobId = job == null ? (Guid?)null : job.Id
                })
                .ToDictionaryAsync(item => item.CaptureAttemptId, cancellationToken);

            var histories = await db.Set<CaptureAttemptHistory>().AsNoTracking()
                .Where(history => attemptIds.Contains(history.CaptureAttemptId))
                .OrderBy(history => history.CaptureAttemptId)
                .ThenBy(history => history.TransitionSequence)
                .ThenBy(history => history.Id)
                .Select(history => new
                {
                    history.CaptureAttemptId,
                    history.TransitionSequence,
                    history.Action,
                    history.FromStatus,
                    history.ToStatus,
                    history.Reason,
                    history.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var historyByAttempt = histories
                .GroupBy(h => h.CaptureAttemptId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ManagerSurveyOrderCaptureAttemptHistoryResponse>)group
                        .Select(h => new ManagerSurveyOrderCaptureAttemptHistoryResponse(
                            TransitionSequence: h.TransitionSequence,
                            Action: h.Action,
                            FromStatus: h.FromStatus,
                            ToStatus: h.ToStatus,
                            Reason: h.Reason,
                            CreatedAt: h.CreatedAt))
                        .ToArray());

            var items = attempts
                .Select(attempt =>
                {
                    manifests.TryGetValue(attempt.Id, out var manifest);
                    return new ManagerSurveyOrderCaptureAttemptResponse(
                        CaptureAttemptId: attempt.Id,
                        AttemptIndex: attempt.AttemptIndex,
                        Status: attempt.Status,
                        Version: attempt.Version,
                        MissionId: missionsByAttempt.TryGetValue(attempt.Id, out var missionId)
                            ? missionId
                            : null,
                        ParentAttemptId: attempt.SupersededAttemptId,
                        ParentMissionId: attempt.ParentMissionId,
                        StartedAt: attempt.StartedAt,
                        EndedAt: attempt.EndedAt,
                        History: historyByAttempt.TryGetValue(attempt.Id, out var attemptHistory)
                            ? attemptHistory
                            : [],
                        ManifestId: manifest?.Id,
                        ManifestFingerprint: manifest?.ManifestFingerprint,
                        ProcessingJobId: manifest?.ProcessingJobId,
                        OrderVersion: orderScope.Version,
                        MappingSourceAttemptId: attempt.MappingSourceAttemptId);
                })
                .ToArray();

            await tx.CommitAsync(cancellationToken);
            return Result.Success<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(items);
        }
        catch (PostgresException exception) when (IsReadConflict(exception))
        {
            return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                AppError.Conflict(
                    "CaptureAttempts.ConcurrencyConflict",
                    "Capture attempt access changed concurrently. Reload and try again."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static bool IsReadConflict(PostgresException exception) =>
        exception.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.LockNotAvailable;
}
