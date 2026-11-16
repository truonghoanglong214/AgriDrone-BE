using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService
{
    public async Task<Result<PublishPlantHealthSurveyResultResponse>> PublishResultAsync(Guid orderId,
        PublishPlantHealthSurveyResultRequest request, CancellationToken token)
    {
        try
        {
            if (request.OperationId == Guid.Empty || request.BatchId == Guid.Empty || request.ExpectedReviewRevision < 0 ||
                request.ApprovalFingerprint is not { Length: 64 })
                Invalid("InvalidRequest", "OperationId, BatchId, expected revision and the exact approval fingerprint are required.");
            var fingerprint = PlantHealthV4Fingerprint.ForValue(new { orderId, Action = "PublishPlantHealthSurveyResult", Request = request });
            return await _db.ExecuteInTransactionAsync(async ct =>
            {
                var context = await AuthorizeAsync(orderId, true, ct);
                var actor = Actor();
                var receipt = await _db.HealthReviewOperations.AsNoTracking().SingleOrDefaultAsync(o =>
                    o.TenantId == context.Order.TenantId && o.ActorId == actor && o.OperationId == request.OperationId, ct);
                if (receipt is not null)
                {
                    if (receipt.Fingerprint != fingerprint || receipt.SurveyResultId != context.Result.Id || receipt.BatchId != request.BatchId)
                        Conflict("OperationConflict", "OperationId has already been used for another request.");
                    var stored = receipt.Outcome.Deserialize<PublishPlantHealthSurveyResultResponse>()
                        ?? throw new InvalidOperationException("Publication receipt is incomplete.");
                    return Result.Success(stored with { IsReplay = true });
                }
                if (context.Order.Status != SurveyOrderStatus.PendingReview || context.Result.Status != SurveyResultStatus.Approved)
                    Conflict("ResultLocked", "Publication requires an Approved result and PendingReview Order.");
                if (context.Result.Version != request.ExpectedResultVersion || context.Result.ReviewRevision != request.ExpectedReviewRevision)
                    Conflict("VersionConflict", "The approved result changed; reload before publishing.");
                if (context.Result.ActiveImportBatchId != request.BatchId || context.Result.ApprovedBatchId != request.BatchId ||
                    context.Result.ApprovalFingerprint != request.ApprovalFingerprint)
                    Conflict("ApprovalSnapshotChanged", "Publication must match the exact approved batch and fingerprint.");

                // Separate from Farm row locks held by authorization: concurrent Orders in a Farm serialize
                // version allocation/latest writes without upgrading their shared authorization locks.
                var farmLock = $"plant-health-publication:{context.Order.TenantId:D}:{context.Order.FarmId:D}";
                await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({farmLock}, 0))", ct);
                var s = await LoadAsync(context.Order, context.Result, request.BatchId, true, ct, lockReviewSources: true);
                var approval = s.Result.ApprovalSnapshot?.Deserialize<PlantHealthApprovalSnapshot>();
                if (approval is null) Conflict("ApprovalSnapshotChanged", "Approval snapshot is missing.");
                ValidateFrozenApproval(s, approval);
                var source = await LockApprovalSourceAsync(s, ct);
                ValidateApprovalSource(s, source);
                s = s with { AllowedMedia = source.EvidenceMediaIds.ToHashSet(), Now = _clock.GetUtcNow() };
                if (!Summary(s, forPublication: true).ReadyForApproval)
                    Conflict("PublicationBlocked", "An approved review dependency is stale or no longer effective.");
                ValidateImportedReviewSet(s);
                await ValidateApprovalGeometryAsync(s, ct);
                var evidence = approval.EvidenceMediaIds.ToArray();
                if (evidence.Length == 0 || evidence.Any(id => !s.AllowedMedia.Contains(id)) ||
                    source.InputMediaIds!.Any(id => !evidence.Contains(id)))
                    Conflict("ProvenanceIncomplete", "Frozen capture evidence is no longer available in this source Mission.");
                var lockedEvidence = await _db.Database.SqlQuery<ApprovalSourceLock>($"""
                    SELECT id AS "Id" FROM media.media_assets
                    WHERE id = ANY({evidence}) AND tenant_id = {s.Order.TenantId} AND farm_id = {s.Order.FarmId}
                    ORDER BY id FOR SHARE
                    """).ToListAsync(ct);
                if (lockedEvidence.Count != evidence.Length) Conflict("ProvenanceIncomplete", "Frozen evidence ownership or availability changed.");

                var version = checked((await _db.HealthPublications.Where(p => p.TenantId == s.Order.TenantId && p.FarmId == s.Order.FarmId)
                    .MaxAsync(p => (long?)p.PublicationVersion, ct) ?? 0) + 1);
                var publicationId = Guid.CreateVersion7();
                var comparison = await ResolvePublishedComparisonAsync(s, ct);
                var payload = BuildPublishedPayload(s, approval, request.OperationId, publicationId, version, actor) with { Comparison = comparison };
                if (payload.Findings.Count != approval.AcceptedFindingCount)
                    Conflict("ApprovalSnapshotChanged", "The accepted finding set differs from the approval snapshot.");
                var plantIds = payload.Findings.Select(f => f.PlantId).Distinct().Order().ToArray();
                var current = await _db.LatestReviewedHealth.FromSqlInterpolated($"""
                    SELECT *, xmin FROM plant.latest_reviewed_plant_health
                    WHERE plant_id = ANY({plantIds}) ORDER BY plant_id FOR UPDATE
                    """).ToDictionaryAsync(p => p.PlantId, ct);
                List<Guid> advanced = [];
                var staged = 0;
                // Locked orders must not advance the Owner-visible latest projection during internal review.
                foreach (var group in payload.Findings.Where(_ => !s.Order.PostFlightSettlement)
                             .GroupBy(f => f.PlantId).OrderBy(g => g.Key))
                {
                    var values = group.OrderBy(f => f.FindingId).ToArray();
                    if (current.TryGetValue(group.Key, out var latest))
                    {
                        if (latest.TryAdvance(s.Order.TenantId, payload, values)) advanced.Add(group.Key);
                    }
                    else
                    {
                        _db.LatestReviewedHealth.Add(LatestReviewedPlantHealth.Create(s.Order.TenantId, payload, values));
                        advanced.Add(group.Key);
                    }
                    await _publicationCheckpoint.AfterPlantStagedAsync(publicationId, group.Key, ++staged, ct);
                }
                // The checkpoint/geometry work can take time. Check effective windows at the final gate.
                s = s with { Now = _clock.GetUtcNow() };
                if (!Summary(s, forPublication: true).ReadyForApproval)
                    Conflict("PublicationBlocked", "A guidance or catalogue dependency expired before publication.");
                payload = payload with { PublishedAt = s.Now };
                var publicationErrors = PlantHealthPublicationValidators.Validate(payload);
                if (publicationErrors.Count != 0) Invalid("InvalidPublication", string.Join("; ", publicationErrors));
                var publication = PlantHealthPublication.Create(s.Order.TenantId, payload);
                _db.HealthPublications.Add(publication);
                foreach (var zone in payload.DiseaseZones) s.Zones[zone.ZoneId].Publish(actor, s.Now);
                s.Result.PublishReviewedPlantHealth(publication, request.ExpectedResultVersion);
                s.Order.RecordPublishedPlantHealthComparison(comparison.PreviousOrderId, comparison.PolicyVersion, comparison.Reason);
                s.Order.RecordReviewedServicePublication(s.Now);
                AddPublicationOutbox(s, payload);
                _db.AddAuditLog(AuditLog.ForUserAction(s.Order.TenantId, s.Order.FarmId, actor, _execution.CorrelationId,
                    "SurveyResult", s.Result.Id, "PlantHealth.ResultPublished", null, JsonSerializer.SerializeToDocument(new
                    { publication.Id, publication.PublicationVersion, publication.BatchId, publication.ApprovalFingerprint,
                        publication.Fingerprint, payload.Conclusion, comparison, AdvancedPlantIds = advanced, OperationId = request.OperationId }), s.Now));
                var output = new PublishPlantHealthSurveyResultResponse(request.OperationId, publication.Id, s.Result.Id, s.Batch.Id,
                    version, "PUBLISHED", s.Order.PostFlightSettlement ? "AWAITING_PRICING" : "COMPLETED", payload.ResultNature, payload.TeamLabelLimitation, payload.Conclusion,
                    payload.Findings.Count, payload.DiseaseZones.Count, advanced, actor, s.Now, publication.Fingerprint);
                _db.HealthReviewOperations.Add(PlantHealthReviewOperation.Create(s.Order.TenantId, actor, request.OperationId,
                    s.Result.Id, s.Batch.Id, fingerprint, JsonSerializer.SerializeToDocument(output), s.Now));
                // One save/commit for publication history, latest, zones, Order, receipt, audit and both Outbox rows.
                await _db.SaveChangesAsync(ct);
                return Result.Success(output);
            }, token);
        }
        catch (ReviewFault error) { _db.ChangeTracker.Clear(); return Result.Failure<PublishPlantHealthSurveyResultResponse>(error.Error); }
        catch (DbUpdateConcurrencyException)
        { _db.ChangeTracker.Clear(); return Result.Failure<PublishPlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "A publication dependency changed; reload and retry.")); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg && IsConcurrency(pg))
        { _db.ChangeTracker.Clear(); return Result.Failure<PublishPlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "A concurrent publication won; reload and retry.")); }
        catch (PostgresException error) when (IsConcurrency(error))
        { _db.ChangeTracker.Clear(); return Result.Failure<PublishPlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "Publication authorization or source changed; reload and retry.")); }
        catch { _db.ChangeTracker.Clear(); throw; }
    }

    private static void ValidateFrozenApproval(Session s, PlantHealthApprovalSnapshot approval)
    {
        if (PlantHealthV4Fingerprint.ForValue(approval) != s.Result.ApprovalFingerprint ||
            approval.ResultId != s.Result.Id || approval.SurveyOrderId != s.Order.Id || approval.TenantId != s.Order.TenantId || approval.FarmId != s.Order.FarmId ||
            approval.BatchId != s.Batch.Id || approval.ReviewRevision != s.Result.ReviewRevision || approval.ReviewRevision != s.Result.ApprovedReviewRevision ||
            approval.MissionId != s.Batch.MissionId || approval.JobId != s.Batch.JobId || approval.HandoffId != s.Batch.HandoffId ||
            approval.SourceVersion != s.Batch.SourceVersion || approval.SourceFingerprint != s.Batch.Fingerprint ||
            approval.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId || approval.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId ||
            approval.ResultNature != s.Result.ResultNature || approval.TeamLabelLimitation != s.Result.TeamLabelLimitation ||
            approval.Provenance != s.Source.Provenance || approval.ApprovedBy != s.Result.ReviewedBy || DatabaseTime(approval.ApprovedAt) != s.Result.ReviewedAt)
            Conflict("ApprovalSnapshotChanged", "Frozen approval identity, provenance or fingerprint changed.");
        var required = s.Heads.Values.Where(h => h.Kind != HealthReviewKind.Guidance || IsAccepted(Current(s, HealthReviewKind.Zone, h.SubjectId))).ToArray();
        if (required.Length != approval.Reviews.Count) Conflict("ApprovalSnapshotChanged", "The required review set differs from approval.");
        foreach (var approved in approval.Reviews)
        {
            var head = required.SingleOrDefault(h => h.CurrentReviewId == approved.ReviewId && h.SubjectId == approved.SubjectId && h.Kind.ToString() == approved.Kind);
            if (head is null || head.Version != approved.HeadVersion || !s.Reviews.TryGetValue(approved.ReviewId, out var review) ||
                review.Decision.ToString() != approved.Decision || PlantHealthV4Fingerprint.ForValue(review.ReadSnapshot()) != approved.SnapshotFingerprint)
                Conflict("ApprovalSnapshotChanged", "A frozen review ID, decision, head version or value changed.");
        }
    }

    private static PlantHealthPreliminaryResultPublishedV1 BuildPublishedPayload(Session s, PlantHealthApprovalSnapshot approval,
        Guid operationId, Guid publicationId, long version, Guid actor)
    {
        var findings = s.Findings.Values.Where(f => IsAccepted(Current(s, HealthReviewKind.Finding, f.Id))).OrderBy(f => f.Id).Select(f =>
        {
            var review = Current(s, HealthReviewKind.Finding, f.Id)!;
            var value = review.ReadSnapshot();
            var original = f.Original.Deserialize<PlantHealthFindingV4>()!;
            return new PublishedPlantHealthFindingV1(f.Id, f.SourceFindingId, f.SourceVersion, value.PlantId!.Value, review.Id,
                review.Decision.ToString(), value.ClassCode!, value.ConditionId!.Value, value.ConditionRevision!.Value,
                value.Position!, DatabaseTime(original.ObservedAt), original.Confidence, original.Uncertainty, original.Limitations, review.ReviewedBy, review.ReviewedAt);
        }).ToArray();
        var zones = s.Zones.Values.Where(z => IsAccepted(Current(s, HealthReviewKind.Zone, z.Id))).OrderBy(z => z.Id).Select(z =>
        {
            var review = Current(s, HealthReviewKind.Zone, z.Id)!;
            var value = review.ReadSnapshot();
            var guidanceReview = Current(s, HealthReviewKind.Guidance, z.Id)!;
            PublishedPlantHealthGuidanceV1? guidance = null;
            if (guidanceReview.Decision == HealthReviewDecision.Select)
            {
                var entry = s.Guidance[guidanceReview.ReadSnapshot().Guidance!.Id];
                guidance = new(entry.Id, entry.Code, entry.VersionNumber, entry.SuspectedClassCode, entry.SourceTitle,
                    entry.SourceReference, entry.GuidanceText, entry.EffectiveFrom, entry.EffectiveTo);
            }
            return new PublishedPlantHealthZoneV1(z.Id, review.Id, value.ClassCode!, value.Geometry!, z.CurrentMembershipVersion,
                value.Members!.Select(m => new PublishedPlantHealthZoneMemberV1(m.FindingId, m.ReviewId, m.PlantId)).ToArray(),
                guidanceReview.Id, guidanceReview.Decision.ToString(), guidance);
        }).ToArray();
        return new(operationId, publicationId, s.Batch.HandoffId, s.Batch.JobId, s.Order.Id, s.Batch.MissionId, s.Order.FarmId,
            s.Result.Id, s.Batch.Id, s.Batch.SourceVersion, s.Batch.FarmBoundaryVersionId, s.Batch.FarmBaseMapVersionId, version,
            approval.ReviewRevision, s.Result.ApprovalFingerprint!, "PUBLISHED", approval.ResultNature, approval.TeamLabelLimitation,
            approval.Conclusion, approval.Provenance, approval.ApprovedBy, approval.ApprovedAt, actor, s.Now, findings, zones);
    }

    // PostgreSQL timestamps use microseconds; snapshots can retain .NET's finer 100 ns ticks.
    private static DateTimeOffset DatabaseTime(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, value.Offset);

    private void AddPublicationOutbox(Session s, PlantHealthPreliminaryResultPublishedV1 payload)
    {
        if (s.Order.PostFlightSettlement) return; // Settlement emits delivery events after payment, never during internal review.
        var outcome = IntegrationEventEnvelopeFactory.Create(IntegrationEventDescriptors.PlantHealthPreliminaryResultPublishedV1,
            Guid.CreateVersion7(), _execution.CorrelationId, s.Order.TenantId, payload.PublishedBy, payload.PublishedAt, payload,
            causationId: payload.CausationId);
        _db.OutboxMessages.Add(_outbox.Create(outcome, outcome.EventType, s.Order.FarmId.ToString()));
        var notification = new PlantHealthPublicationNotificationRequestedV1(payload.CausationId, payload.PublicationId,
            s.Order.Id, s.Result.Id, s.Order.FarmId, payload.PublicationVersion, payload.ResultNature, payload.TeamLabelLimitation, payload.PublishedAt);
        var intent = IntegrationEventEnvelopeFactory.Create(IntegrationEventDescriptors.PlantHealthPublicationNotificationRequestedV1,
            Guid.CreateVersion7(), _execution.CorrelationId, s.Order.TenantId, payload.PublishedBy, payload.PublishedAt, notification,
            causationId: payload.CausationId);
        _db.OutboxMessages.Add(_outbox.Create(intent, intent.EventType, s.Order.FarmId.ToString()));
    }
}
