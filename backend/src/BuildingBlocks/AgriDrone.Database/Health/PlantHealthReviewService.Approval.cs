using System.Data;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.ApprovePlantHealthSurveyResult;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService
{
    private sealed record ApprovalSourceLock(Guid Id);
    private sealed record ApprovalGeometryError(Guid Id);

    public async Task<Result<ApprovePlantHealthSurveyResultResponse>> ApproveResultAsync(
        Guid orderId, ApprovePlantHealthSurveyResultRequest request, CancellationToken token)
    {
        try
        {
            if (request.OperationId == Guid.Empty || request.BatchId == Guid.Empty || request.ExpectedReviewRevision < 0 ||
                string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
                Invalid("InvalidRequest", "OperationId, BatchId, expected revision and a reason of at most 2000 characters are required.");
            var requestFingerprint = PlantHealthV4Fingerprint.ForValue(new { orderId, Action = "ApprovePlantHealthSurveyResult", Request = request });
            await using var tx = await _db.Database.BeginTransactionAsync(token);
            var context = await AuthorizeAsync(orderId, true, token);
            var actor = Actor();
            var receipt = await _db.HealthReviewOperations.AsNoTracking().SingleOrDefaultAsync(o =>
                o.TenantId == context.Order.TenantId && o.ActorId == actor && o.OperationId == request.OperationId, token);
            if (receipt is not null)
            {
                if (receipt.Fingerprint != requestFingerprint || receipt.SurveyResultId != context.Result.Id || receipt.BatchId != request.BatchId)
                    Conflict("OperationConflict", "OperationId has already been used for another request.");
                var stored = receipt.Outcome.Deserialize<ApprovePlantHealthSurveyResultResponse>()
                    ?? throw new InvalidOperationException("Approval receipt is incomplete.");
                await tx.CommitAsync(token);
                return Result.Success(stored with { IsReplay = true, CurrentStatus = ResultStatus(context.Result.Status) });
            }
            if (context.Order.Status != SurveyOrderStatus.PendingReview || context.Result.Status != SurveyResultStatus.PendingReview)
                Conflict("ResultLocked", "Approval requires a pending Order and pending Plant Health result.");
            if (context.Result.ActiveImportBatchId != request.BatchId) Conflict("BatchChanged", "The active batch changed; reload the review candidate.");
            if (context.Result.Version != request.ExpectedResultVersion || context.Result.ReviewRevision != request.ExpectedReviewRevision)
                Conflict("VersionConflict", "The reviewed result changed; reload before approving.");

            var s = await LoadAsync(context.Order, context.Result, request.BatchId, true, token, lockReviewSources: true);
            var source = await LockApprovalSourceAsync(s, token);
            ValidateApprovalSource(s, source);
            s = s with { AllowedMedia = source.EvidenceMediaIds.ToHashSet(), Now = _clock.GetUtcNow() };
            if (!Summary(s).ReadyForApproval) Conflict("ApprovalBlocked", "Resolve all current finding, boundary, zone and guidance blockers before approval.");
            ValidateImportedReviewSet(s);
            await ValidateApprovalGeometryAsync(s, token);

            var requiredHeads = s.Heads.Values.Where(h => h.Kind != HealthReviewKind.Guidance ||
                    IsAccepted(Current(s, HealthReviewKind.Zone, h.SubjectId)))
                .OrderBy(h => h.Kind).ThenBy(h => h.SubjectId).ToArray();
            foreach (var head in requiredHeads)
            {
                var review = s.Reviews[head.CurrentReviewId];
                if (review.ReviewedBy == Guid.Empty || review.ReviewedAt == default || review.ReviewedAt.Offset != TimeSpan.Zero ||
                    review.ReviewedAt > s.Now || string.IsNullOrWhiteSpace(review.Reason) || review.Reason.Length > 2000 ||
                    review.ReadSnapshot().EvidenceMediaIds is null)
                    Conflict("ProvenanceIncomplete", "Every required decision needs reviewer, time, reason and an evidence snapshot.");
            }
            var evidence = s.Source.Findings.SelectMany(f => f.EvidenceMedia).Concat(s.Source.BoundaryExceptions.SelectMany(e => e.EvidenceMedia))
                .Concat(s.Source.DiseaseZones.SelectMany(z => z.EvidenceMedia)).Select(e => e.MediaAssetId)
                .Concat(requiredHeads.SelectMany(h => s.Reviews[h.CurrentReviewId].ReadSnapshot().EvidenceMediaIds ?? []))
                .Concat(source.InputMediaIds!).Distinct().Order().ToArray();
            if (evidence.Length == 0 || evidence.Any(id => !s.AllowedMedia.Contains(id)))
                Conflict("ProvenanceIncomplete", "Approval requires source-owned capture/review evidence.");
            var lockedMedia = await _db.Database.SqlQuery<ApprovalSourceLock>($"""
                SELECT id AS "Id" FROM media.media_assets
                WHERE id = ANY({evidence}) AND tenant_id = {s.Order.TenantId} AND farm_id = {s.Order.FarmId}
                ORDER BY id FOR SHARE
                """).ToListAsync(token);
            if (lockedMedia.Count != evidence.Length) Conflict("ProvenanceIncomplete", "Evidence ownership or availability changed.");

            // Re-evaluate temporal catalogue/guidance validity after potentially large geometry/evidence checks.
            s = s with { Now = _clock.GetUtcNow() };
            var summary = Summary(s);
            if (!summary.ReadyForApproval) Conflict("ApprovalBlocked", "A review dependency or guidance effective window changed during approval.");
            var selections = requiredHeads.Select(h =>
            {
                var review = s.Reviews[h.CurrentReviewId];
                return new PlantHealthApprovedReview(review.Id, h.SubjectId, h.Kind.ToString(), review.Decision.ToString(),
                    h.Version, PlantHealthV4Fingerprint.ForValue(review.ReadSnapshot()));
            }).ToArray();
            var snapshot = new PlantHealthApprovalSnapshot(request.OperationId, s.Result.Id, s.Order.Id, s.Order.TenantId, s.Order.FarmId,
                s.Batch.Id, s.Batch.MissionId, s.Batch.HandoffId, s.Batch.JobId, s.Batch.FarmBoundaryVersionId, s.Batch.FarmBaseMapVersionId,
                s.Batch.SourceVersion, s.Result.ReviewRevision, PlantHealthV4Values.ResultNature, s.Source.TeamLabelLimitation,
                s.Source.Provenance, s.Batch.Fingerprint, selections, evidence, summary.AcceptedFindings,
                summary.AcceptedFindings == 0 ? "NO_ACCEPTED_SUSPECTED_FINDINGS" : "HAS_REVIEWED_SUSPECTED_FINDINGS",
                actor, s.Now, request.Reason.Trim());
            s.Result.ApproveReviewedPlantHealth(snapshot, request.ExpectedResultVersion);
            _db.AddAuditLog(AuditLog.ForUserAction(s.Order.TenantId, s.Order.FarmId, actor, _execution.CorrelationId,
                "SurveyResult", s.Result.Id, "PlantHealth.ResultApproved", null, JsonSerializer.SerializeToDocument(new
                { BatchId = s.Batch.Id, s.Result.ReviewRevision, s.Result.ApprovalFingerprint, snapshot.Conclusion, snapshot.AcceptedFindingCount,
                    snapshot.Reason, ReviewIds = selections.Select(r => r.ReviewId).ToArray() }), s.Now));
            await _db.SaveChangesAsync(token);
            var output = ApprovalResponse(s.Result, snapshot);
            _db.HealthReviewOperations.Add(PlantHealthReviewOperation.Create(s.Order.TenantId, actor, request.OperationId,
                s.Result.Id, s.Batch.Id, requestFingerprint, JsonSerializer.SerializeToDocument(output), s.Now));
            await _db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return Result.Success(output);
        }
        catch (ReviewFault error) { _db.ChangeTracker.Clear(); return Result.Failure<ApprovePlantHealthSurveyResultResponse>(error.Error); }
        catch (DbUpdateConcurrencyException)
        { _db.ChangeTracker.Clear(); return Result.Failure<ApprovePlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "The approval candidate changed; reload and retry.")); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg && IsConcurrency(pg))
        { _db.ChangeTracker.Clear(); return Result.Failure<ApprovePlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "A concurrent approval or source update won; reload and retry.")); }
        catch (PostgresException error) when (IsConcurrency(error))
        { _db.ChangeTracker.Clear(); return Result.Failure<ApprovePlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "Approval authorization or dependencies changed; reload and retry.")); }
    }

    public async Task<Result<ApprovePlantHealthSurveyResultResponse>> GetApprovalAsync(Guid orderId, CancellationToken token)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var context = await AuthorizeAsync(orderId, false, token);
            var snapshot = context.Result.ApprovalSnapshot?.Deserialize<PlantHealthApprovalSnapshot>();
            if (snapshot is null) NotFound();
            if (context.Result.ApprovalFingerprint != PlantHealthV4Fingerprint.ForValue(snapshot))
                Conflict("ApprovalSnapshotChanged", "Stored approval metadata does not match its fingerprint.");
            var output = ApprovalResponse(context.Result, snapshot);
            await tx.CommitAsync(token);
            return Result.Success(output);
        }
        catch (ReviewFault error) { return Result.Failure<ApprovePlantHealthSurveyResultResponse>(error.Error); }
        catch (PostgresException error) when (IsConcurrency(error))
        { return Result.Failure<ApprovePlantHealthSurveyResultResponse>(ConflictError("VersionConflict", "Approval context changed; reload and retry.")); }
    }

    private async Task<PlantHealthSourceContext> LockApprovalSourceAsync(Session s, CancellationToken token)
    {
        var job = await _db.Database.SqlQuery<ApprovalSourceLock>($"""
            SELECT j.id AS "Id" FROM mission.ai_processing_jobs j
            JOIN mission.drone_missions m ON m.id = j.mission_id
            WHERE j.id = {s.Batch.JobId} AND m.id = {s.Batch.MissionId}
              AND m.tenant_id = {s.Order.TenantId} AND m.farm_id = {s.Order.FarmId}
            FOR SHARE OF m, j
            """).SingleOrDefaultAsync(token);
        var map = await _db.Database.SqlQuery<ApprovalSourceLock>($"""
            SELECT bm.id AS "Id" FROM farm.farm_base_map_versions bm
            JOIN farm.farm_boundaries fb ON fb.id = bm.farm_boundary_version_id AND fb.farm_id = bm.farm_id AND fb.tenant_id = bm.tenant_id
            WHERE bm.id = {s.Batch.FarmBaseMapVersionId} AND fb.id = {s.Batch.FarmBoundaryVersionId}
              AND bm.tenant_id = {s.Order.TenantId} AND bm.farm_id = {s.Order.FarmId}
              AND bm.published_at IS NOT NULL AND fb.reviewed_at IS NOT NULL AND fb.status IN ('APPROVED','SUPERSEDED')
            FOR SHARE OF fb, bm
            """).SingleOrDefaultAsync(token);
        var source = await _source.GetAsync(s.Batch.MissionId, s.Batch.JobId, token);
        if (job is null || map is null || source is null) Conflict("SourceContextChanged", "The capture/job context is unavailable or no longer matches this result.");
        return source;
    }

    private static void ValidateApprovalSource(Session s, PlantHealthSourceContext source)
    {
        if (V4BusinessContractValidators.Validate(s.Source).Count != 0 ||
            s.Result.ResultNature != PlantHealthV4Values.ResultNature || s.Result.TeamLabelLimitation != s.Source.TeamLabelLimitation ||
            s.Result.SourceMissionId != s.Batch.MissionId || s.Result.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId ||
            s.Result.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId ||
            s.Source.SurveyOrderId != s.Order.Id || s.Source.FarmId != s.Order.FarmId || s.Source.MissionId != s.Batch.MissionId ||
            s.Source.JobId != s.Batch.JobId || s.Source.HandoffId != s.Batch.HandoffId ||
            s.Source.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId || s.Source.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId ||
            s.Batch.Fingerprint != PlantHealthV4Fingerprint.ForPayload(s.Source) ||
            !source.Completed || !source.IsPlantHealth || source.TenantId != s.Order.TenantId || source.FarmId != s.Order.FarmId ||
            source.SurveyOrderId != s.Order.Id || source.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId ||
            source.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId || source.SourceVersion != s.Batch.SourceVersion ||
            source.Provenance != s.Source.Provenance || source.TeamLabelLimitation != s.Source.TeamLabelLimitation)
            Conflict("ProvenanceIncomplete", "Result, source provenance or capture versions no longer match the reviewed batch.");
        if (source.InputMediaIds is not { Count: > 0 } || source.InputMediaIds.Contains(Guid.Empty) ||
            source.InputMediaIds.Distinct().Count() != source.InputMediaIds.Count || source.ProcessedImageCount is not > 0 ||
            source.ProcessedImageCount > source.InputMediaIds.Count || source.InputMediaIds.Any(id => !source.EvidenceMediaIds.Contains(id)))
            Conflict("ProvenanceIncomplete", "A completed run with registered input media and a positive processed-image count is required, including for zero-finding results.");
    }

    private static void ValidateImportedReviewSet(Session s)
    {
        if (s.Findings.Count != s.Source.Findings.Count || s.Exceptions.Count != s.Source.BoundaryExceptions.Count || s.Zones.Count != s.Source.DiseaseZones.Count)
            Conflict("SourceContextChanged", "The pending item set differs from the original handoff.");
        var originals = s.Source.Findings.ToDictionary(f => f.FindingId);
        foreach (var finding in s.Findings.Values)
        {
            var original = finding.Original.Deserialize<PlantHealthFindingV4>();
            if (!originals.TryGetValue(finding.SourceFindingId, out var expected) || finding.SourceVersion != expected.FindingVersion ||
                original is null || finding.Fingerprint != PlantHealthV4Fingerprint.ForFinding(expected) || finding.Fingerprint != PlantHealthV4Fingerprint.ForFinding(original))
                Conflict("SourceContextChanged", "A canonical source finding differs from the reviewed handoff.");
        }
        var exceptions = s.Source.BoundaryExceptions.ToDictionary(e => e.SourceReferenceId, StringComparer.Ordinal);
        foreach (var item in s.Exceptions.Values)
            if (!exceptions.TryGetValue(item.SourceReferenceId, out var expected) ||
                PlantHealthV4Fingerprint.ForValue(item.Original.Deserialize<HealthBoundaryExceptionV4>()) != PlantHealthV4Fingerprint.ForValue(expected))
                Conflict("SourceContextChanged", "Boundary exception provenance differs from the handoff.");
        var zones = s.Source.DiseaseZones.ToDictionary(z => z.SourceProposalId, StringComparer.Ordinal);
        foreach (var zone in s.Zones.Values)
        {
            var review = Current(s, HealthReviewKind.Zone, zone.Id)!;
            if (!zones.TryGetValue(zone.SourceProposalId, out var expected) ||
                PlantHealthV4Fingerprint.ForValue(zone.ProposalEvidence.Deserialize<ProposedDiseaseZoneV4>()) != PlantHealthV4Fingerprint.ForValue(expected) ||
                zone.CurrentHealthReviewId != review.Id || zone.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId || zone.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId ||
                review.Decision == HealthReviewDecision.Reject && zone.Status != DiseaseZoneStatus.Rejected)
                Conflict("DependencyChanged", "Zone review projection or source differs from the active candidate.");
            if (!IsAccepted(review)) continue;
            var value = review.ReadSnapshot();
            var guidance = Current(s, HealthReviewKind.Guidance, zone.Id)!;
            var reviewedPlants = zone.Memberships.Where(m => m.Kind == DiseaseZoneMembershipKind.Reviewed && m.MembershipVersion == zone.CurrentMembershipVersion)
                .Select(m => m.PlantId).Order().ToArray();
            if (zone.Status != DiseaseZoneStatus.Reviewed || zone.ReviewedGeometry is null || zone.HasBlockingItems ||
                zone.ReviewedSuspectedClassCode != value.ClassCode || zone.ReviewedPlantConditionId != value.ConditionId ||
                zone.GuidanceDecision != guidance.Decision.ToString() || zone.SelectedSourcedGuidanceId != guidance.ReadSnapshot().Guidance?.Id ||
                !reviewedPlants.SequenceEqual(value.Members!.Select(m => (Guid?)m.PlantId).Order()))
                Conflict("DependencyChanged", "Re-review the zone before approving its current member/guidance projection.");
        }
    }

    private async Task ValidateApprovalGeometryAsync(Session s, CancellationToken token)
    {
        var accepted = s.Findings.Values.Where(f => IsAccepted(Current(s, HealthReviewKind.Finding, f.Id))).ToArray();
        var points = accepted.Select(f =>
        {
            var value = Current(s, HealthReviewKind.Finding, f.Id)!.ReadSnapshot();
            var point = Point(value.Position);
            return new { Id = f.Id, Longitude = point.X, Latitude = point.Y, HasEvidence = value.EvidenceMediaIds is { Count: > 0 } };
        }).ToArray();
        var pointJson = JsonSerializer.Serialize(points);
        var badPoints = await _db.Database.SqlQuery<ApprovalGeometryError>($"""
            SELECT p."Id" FROM farm.farm_boundaries b CROSS JOIN jsonb_to_recordset({pointJson}::jsonb)
                AS p("Id" uuid, "Longitude" double precision, "Latitude" double precision, "HasEvidence" boolean)
            WHERE b.id = {s.Batch.FarmBoundaryVersionId} AND (
                NOT ST_Covers(b.geometry, ST_SetSRID(ST_MakePoint(p."Longitude",p."Latitude"),4326)) OR
                (NOT p."HasEvidence" AND ST_Distance(ST_Boundary(b.geometry)::geography,
                    ST_SetSRID(ST_MakePoint(p."Longitude",p."Latitude"),4326)::geography) <= {PlantHealthV4Values.BoundaryReviewThresholdMeters}))
            """).AnyAsync(token);
        if (badPoints) Conflict("ApprovalBlocked", "Reviewed positions or near-boundary evidence require another review.");
        var zoneGeometry = s.Zones.Values.Where(z => IsAccepted(Current(s, HealthReviewKind.Zone, z.Id))).Select(z =>
        {
            var value = Current(s, HealthReviewKind.Zone, z.Id)!.ReadSnapshot();
            if (value.Geometry is null) Conflict("ApprovalBlocked", "Reviewed zone geometry is missing.");
            var geoJson = new GeoJsonPolygonV3("Polygon", value.Geometry.Select(r => r.Select(p => (IReadOnlyList<double>)p).ToArray()).ToArray());
            var geometry = Polygon(geoJson);
            if (!geometry.EqualsExact(z.ReviewedGeometry)) Conflict("DependencyChanged", "Zone geometry projection changed.");
            foreach (var member in value.Members!)
                if (!geometry.Covers(Point(Current(s, HealthReviewKind.Finding, member.FindingId)!.ReadSnapshot().Position)))
                    Conflict("ApprovalBlocked", "A reviewed member is outside its zone geometry.");
            return new { Id = z.Id, Wkt = geometry.AsText() };
        }).ToArray();
        var zoneJson = JsonSerializer.Serialize(zoneGeometry);
        var badZones = await _db.Database.SqlQuery<ApprovalGeometryError>($"""
            SELECT z."Id" FROM farm.farm_boundaries b CROSS JOIN jsonb_to_recordset({zoneJson}::jsonb) AS z("Id" uuid, "Wkt" text)
            WHERE b.id = {s.Batch.FarmBoundaryVersionId} AND
                (NOT ST_IsValid(ST_GeomFromText(z."Wkt",4326)) OR NOT ST_Covers(b.geometry,ST_GeomFromText(z."Wkt",4326)))
            """).AnyAsync(token);
        if (badZones) Conflict("ApprovalBlocked", "Reviewed zone geometry no longer satisfies the capture boundary.");
    }

    private static string ResultStatus(SurveyResultStatus status) => status switch
    {
        SurveyResultStatus.PendingReview => "PENDING_REVIEW", SurveyResultStatus.Approved => "APPROVED",
        SurveyResultStatus.Published => "PUBLISHED", _ => throw new InvalidOperationException("Unknown result state.")
    };
    private static ApprovePlantHealthSurveyResultResponse ApprovalResponse(SurveyResult result, PlantHealthApprovalSnapshot snapshot) =>
        new(snapshot.OperationId, result.Id, snapshot.BatchId, result.Version, snapshot.ReviewRevision, "APPROVED", snapshot.ResultNature,
            snapshot.TeamLabelLimitation, snapshot.Conclusion, snapshot.AcceptedFindingCount, snapshot.ApprovedBy, snapshot.ApprovedAt,
            result.ApprovalFingerprint!, CurrentStatus: ResultStatus(result.Status));
}
