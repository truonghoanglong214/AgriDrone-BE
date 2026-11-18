using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService : IPlantHealthReviewService, IPlantHealthResultPublisher
{
    private readonly SurveyResultPublicationDbContext _db;
    private readonly IPlantHealthReviewAuthorizationGuard _authorization;
    private readonly IExecutionContext _execution;
    private readonly IPlantHealthSourceContextQuery _source;
    private readonly TimeProvider _clock;
    private readonly OutboxMessageFactory _outbox;
    private readonly IPlantHealthPublicationCheckpoint _publicationCheckpoint;

    public PlantHealthReviewService(SurveyResultPublicationDbContext db, IPlantHealthReviewAuthorizationGuard authorization,
        IExecutionContext execution, IPlantHealthSourceContextQuery source, TimeProvider clock,
        OutboxMessageFactory outbox, IPlantHealthPublicationCheckpoint publicationCheckpoint)
    { _db = db; _authorization = authorization; _execution = execution; _source = source; _clock = clock;
        _outbox = outbox; _publicationCheckpoint = publicationCheckpoint; }

    private sealed class ReviewFault(AppError error) : Exception(error.Description)
    { public AppError Error { get; } = error; }
    private sealed record Prepared(HealthReviewKind Kind, Guid SubjectId, uint? ExpectedVersion,
        HealthReviewDecision Decision, string Reason, HealthReviewSnapshot Snapshot);
    private sealed record Session(SurveyOrder Order, SurveyResult Result, PlantHealthImportBatch Batch,
        Dictionary<Guid, PlantHealthFinding> Findings, Dictionary<Guid, PlantHealthBoundaryException> Exceptions,
        Dictionary<Guid, DiseaseZone> Zones, Dictionary<(HealthReviewKind Kind, Guid Id), PlantHealthReviewState> Heads,
        Dictionary<Guid, PlantHealthReviewEntry> Reviews, Dictionary<Guid, PlantCondition> Conditions,
        Dictionary<Guid, SourcedGuidance> Guidance, Dictionary<Guid, Plant> Plants, PlantHealthAnalysisReadyV4 Source,
        HashSet<Guid> AllowedMedia, DateTimeOffset Now);

    private Guid Actor()
    {
        if (_execution.ActorId is not Guid actor || _execution.Source != ExecutionContextSource.Http)
            throw new ReviewFault(AppError.Unauthorized("PlantHealthReview.CurrentUserRequired", "An authenticated SystemManager is required."));
        return actor;
    }

    private async Task<(SurveyOrder Order, SurveyResult Result)> AuthorizeAsync(Guid orderId, bool mutation, CancellationToken token)
    {
        var actor = Actor();
        var order = mutation
            ? await _db.SurveyOrders.FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {orderId} FOR UPDATE").SingleOrDefaultAsync(token)
            : await _db.SurveyOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, token);
        if (order is null || !await _authorization.AuthorizeAsync(order.TenantId, order.FarmId, actor, token)) NotFound();
        var result = mutation
            ? await _db.SurveyResults.FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_results WHERE survey_order_id = {orderId} FOR UPDATE").SingleOrDefaultAsync(token)
            : await _db.SurveyResults.AsNoTracking().SingleOrDefaultAsync(r => r.SurveyOrderId == orderId, token);
        if (result is null || result!.ServiceType != SurveyServiceType.PlantHealth) NotFound();
        return (order!, result!);
    }

    private async Task<Session> LoadAsync(SurveyOrder order, SurveyResult result, Guid? requestedBatch, bool mutation, CancellationToken token, bool lockReviewSources = false)
    {
        var batchId = requestedBatch ?? result.ActiveImportBatchId;
        var batch = await _db.HealthImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == batchId &&
            b.TenantId == order.TenantId && b.FarmId == order.FarmId && b.SurveyResultId == result.Id, token);
        if (batch is null) NotFound();
        var ids = await _db.HealthBatchFindings.Where(f => f.BatchId == batchId).Select(f => f.FindingId).ToArrayAsync(token);
        var findings = await _db.HealthFindings.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, token);
        var exceptions = await _db.HealthBoundaryExceptions.AsNoTracking().Where(e => e.BatchId == batchId).ToDictionaryAsync(e => e.Id, token);
        var zonesQuery = lockReviewSources
            ? _db.DiseaseZones.FromSqlInterpolated($"SELECT *, xmin FROM plant.disease_zones WHERE import_batch_id = {batchId} AND source_schema_version = 4 ORDER BY id FOR SHARE")
            : _db.DiseaseZones.Where(z => z.ImportBatchId == batchId && z.SourceSchemaVersion == 4);
        var zones = await zonesQuery.Include(z => z.Memberships).ToDictionaryAsync(z => z.Id, token);
        var heads = lockReviewSources
            ? await _db.HealthReviewStates.FromSqlInterpolated($"SELECT *, xmin FROM plant.plant_health_review_states WHERE batch_id = {batchId} ORDER BY kind, subject_id FOR SHARE").ToListAsync(token)
            : await _db.HealthReviewStates.Where(h => h.BatchId == batchId).ToListAsync(token);
        var reviewIds = heads.Select(h => h.CurrentReviewId).ToArray();
        var reviews = await _db.HealthReviews.AsNoTracking().Where(r => reviewIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
        var conditions = mutation
            ? await _db.PlantConditions.FromSqlRaw("SELECT *, xmin FROM plant.plant_conditions WHERE plant_health_label_kind IS NOT NULL FOR SHARE").ToDictionaryAsync(c => c.Id, token)
            : await _db.PlantConditions.AsNoTracking().Where(c => c.PlantHealthLabelKind != null).ToDictionaryAsync(c => c.Id, token);
        var guidance = mutation
            ? await _db.SourcedGuidance.FromSqlRaw("SELECT *, xmin FROM plant.sourced_guidance FOR SHARE").ToDictionaryAsync(g => g.Id, token)
            : await _db.SourcedGuidance.AsNoTracking().ToDictionaryAsync(g => g.Id, token);
        var source = batch!.OriginalPayload.Deserialize<PlantHealthAnalysisReadyV4>() ?? throw new InvalidOperationException("Import source snapshot is missing.");
        var media = await _source.GetAsync(batch.MissionId, batch.JobId, token);
        var plants = lockReviewSources
            ? await _db.Plants.FromSqlInterpolated($"SELECT *, xmin FROM plant.plants WHERE farm_id = {order.FarmId} ORDER BY id FOR SHARE").ToDictionaryAsync(p => p.Id, token)
            : await _db.Plants.AsNoTracking().Where(p => p.FarmId == order.FarmId).ToDictionaryAsync(p => p.Id, token);
        return new(order, result, batch, findings, exceptions, zones,
            heads.ToDictionary(h => (h.Kind, h.SubjectId)), reviews, conditions, guidance, plants,
            source, media?.EvidenceMediaIds.ToHashSet() ?? [], _clock.GetUtcNow());
    }

    private async Task<Result<T>> ReadAsync<T>(Guid orderId, Guid? batchId, Func<Session, CancellationToken, Task<T>> read, CancellationToken token)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var context = await AuthorizeAsync(orderId, false, token);
            var session = await LoadAsync(context.Order, context.Result, batchId, false, token);
            var output = await read(session, token);
            await tx.CommitAsync(token);
            return Result.Success(output);
        }
        catch (ReviewFault error) { return Result.Failure<T>(error.Error); }
        catch (PostgresException error) when (IsConcurrency(error))
        { return Result.Failure<T>(ConflictError("VersionConflict", "Review context changed; reload and retry.")); }
    }

    private async Task<Result<HealthReviewOutcome>> MutateAsync<TRequest>(Guid orderId, Guid operationId, Guid batchId,
        uint expectedResultVersion, string action, Guid? subjectId, TRequest request,
        Func<Session, CancellationToken, Task<IReadOnlyList<Prepared>>> prepare, CancellationToken token)
    {
        try
        {
            if (operationId == Guid.Empty || batchId == Guid.Empty) Invalid("InvalidRequest", "OperationId and BatchId are required.");
            var fingerprint = PlantHealthV4Fingerprint.ForValue(new { orderId, action, subjectId, request });
            await using var tx = await _db.Database.BeginTransactionAsync(token);
            var context = await AuthorizeAsync(orderId, true, token);
            var actor = Actor();
            var receipt = await _db.HealthReviewOperations.AsNoTracking().SingleOrDefaultAsync(o =>
                o.TenantId == context.Order.TenantId && o.ActorId == actor && o.OperationId == operationId, token);
            if (receipt is not null)
            {
                if (receipt.Fingerprint != fingerprint || receipt.SurveyResultId != context.Result.Id || receipt.BatchId != batchId)
                    Conflict("OperationConflict", "OperationId has already been used for another request.");
                var old = receipt.Outcome.Deserialize<HealthReviewOutcome>() ?? throw new InvalidOperationException("Operation receipt is incomplete.");
                await tx.CommitAsync(token);
                return Result.Success(old with { IsReplay = true, IsCurrentBatch = context.Result.ActiveImportBatchId == batchId });
            }
            if (context.Order.Status != SurveyOrderStatus.PendingReview || context.Result.Status != SurveyResultStatus.PendingReview)
                Conflict("ResultLocked", "Only a pending Plant Health result can be reviewed.");
            if (context.Result.ActiveImportBatchId != batchId) Conflict("BatchChanged", "The active import batch changed; reload before reviewing.");
            if (context.Result.Version != expectedResultVersion) Conflict("VersionConflict", "Result version changed; reload before reviewing.");
            var session = await LoadAsync(context.Order, context.Result, batchId, true, token);
            var prepared = await prepare(session, token);
            foreach (var item in prepared) CheckHead(session, item.Kind, item.SubjectId, item.ExpectedVersion);
            foreach (var item in prepared)
            {
                var prior = Current(session, item.Kind, item.SubjectId);
                var entry = PlantHealthReviewEntry.Create(batchId, session.Order.TenantId, session.Order.FarmId, session.Result.Id,
                    item.Kind, item.SubjectId, prior?.Id, item.Decision, actor, session.Now, item.Reason, item.Snapshot);
                _db.HealthReviews.Add(entry); session.Reviews.Add(entry.Id, entry);
                if (session.Heads.TryGetValue((item.Kind, item.SubjectId), out var head)) head.Advance(entry);
                else
                {
                    head = PlantHealthReviewState.Create(entry); _db.HealthReviewStates.Add(head);
                    session.Heads.Add((item.Kind, item.SubjectId), head);
                }
                _db.AddAuditLog(AuditLog.ForUserAction(session.Order.TenantId, session.Order.FarmId, actor, _execution.CorrelationId,
                    "PlantHealthReview", entry.Id, $"PlantHealth.{entry.Kind}.{entry.Decision}",
                    prior?.Snapshot, JsonSerializer.SerializeToDocument(new { entry.BatchId, entry.SubjectId, entry.Decision,
                        entry.Reason, entry.PreviousReviewId, Snapshot = entry.Snapshot.RootElement }), session.Now));
            }
            List<Guid> invalidated = [];
            foreach (var zone in session.Zones.Values)
            {
                var zoneReview = Current(session, HealthReviewKind.Zone, zone.Id);
                if (zoneReview is null) continue;
                var issues = ZoneIssues(session, zone);
                var changed = prepared.Any(p => p.SubjectId == zone.Id && p.Kind is HealthReviewKind.Zone or HealthReviewKind.Guidance);
                var boundMembers = zoneReview.ReadSnapshot().Members?.Select(m => m.FindingId).ToHashSet() ?? [];
                var membersChanged = prepared.Any(p => p.Kind == HealthReviewKind.Finding && boundMembers.Contains(p.SubjectId));
                if (!changed && !membersChanged && (issues.Count == 0 || zone.Status != DiseaseZoneStatus.Reviewed)) continue;
                var guidance = Current(session, HealthReviewKind.Guidance, zone.Id);
                if (guidance?.ReadSnapshot().BoundReviewId != zoneReview.Id) guidance = null;
                if (membersChanged || zone.Status == DiseaseZoneStatus.Reviewed && issues.Count != 0) invalidated.Add(zone.Id);
                zone.ProjectHealthReview(zoneReview, guidance, issues.Count == 0, session.Now);
            }
            session.Result.RecordHumanReview(batchId, session.Now);
            await _db.SaveChangesAsync(token);
            var versions = prepared.Select(p => session.Heads[(p.Kind, p.SubjectId)]).Select(h =>
                new HealthReviewedVersion(h.Kind.ToString(), h.SubjectId, h.CurrentReviewId, h.Version,
                    h.Kind is HealthReviewKind.Zone or HealthReviewKind.Guidance ? session.Zones[h.SubjectId].Version :
                    h.Kind == HealthReviewKind.Boundary ? session.Exceptions[h.SubjectId].Version : session.Findings[h.SubjectId].Version)).ToArray();
            var outcome = new HealthReviewOutcome(operationId, session.Result.Id, batchId, session.Result.Version,
                session.Result.ReviewRevision, versions, invalidated);
            _db.HealthReviewOperations.Add(PlantHealthReviewOperation.Create(session.Order.TenantId, actor, operationId,
                session.Result.Id, batchId, fingerprint, JsonSerializer.SerializeToDocument(outcome), session.Now));
            await _db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return Result.Success(outcome);
        }
        catch (ReviewFault error) { _db.ChangeTracker.Clear(); return Result.Failure<HealthReviewOutcome>(error.Error); }
        catch (DbUpdateConcurrencyException)
        { _db.ChangeTracker.Clear(); return Result.Failure<HealthReviewOutcome>(ConflictError("VersionConflict", "A review dependency changed; reload and retry.")); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg && IsConcurrency(pg))
        { _db.ChangeTracker.Clear(); return Result.Failure<HealthReviewOutcome>(ConflictError("VersionConflict", "A concurrent review won; reload and retry.")); }
        catch (PostgresException error) when (IsConcurrency(error))
        { _db.ChangeTracker.Clear(); return Result.Failure<HealthReviewOutcome>(ConflictError("VersionConflict", "Review authorization/context changed; reload and retry.")); }
    }

    private static bool IsConcurrency(PostgresException e) => e.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
    private static PlantHealthReviewEntry? Current(Session s, HealthReviewKind kind, Guid subjectId) =>
        s.Heads.TryGetValue((kind, subjectId), out var head) ? s.Reviews[head.CurrentReviewId] : null;
    private static void CheckHead(Session s, HealthReviewKind kind, Guid id, uint? expected)
    {
        var exists = s.Heads.TryGetValue((kind, id), out var head);
        if (exists ? !expected.HasValue || head!.Version != expected : expected.HasValue)
            Conflict("VersionConflict", "Review version changed; reload the current review.");
    }
    [DoesNotReturn]
    private static void NotFound() => throw new ReviewFault(AppError.NotFound("PlantHealthReview.NotFound", "Plant Health review resource was not found."));
    [DoesNotReturn]
    private static void Invalid(string code, string message) => throw new ReviewFault(AppError.Validation($"PlantHealthReview.{code}", message));
    [DoesNotReturn]
    private static void Conflict(string code, string message) => throw new ReviewFault(ConflictError(code, message));
    private static AppError ConflictError(string code, string message) => AppError.Conflict($"PlantHealthReview.{code}", message);
}
