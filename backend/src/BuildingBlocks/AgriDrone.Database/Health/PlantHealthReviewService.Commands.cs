using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService
{
    public Task<Result<HealthReviewOutcome>> ReviewFindingsAsync(Guid orderId, HealthFindingBatchRequest request, CancellationToken token)
    {
        var normalized = request with { Items = request.Items?.OrderBy(i => i?.FindingId).Select(i => i is null ? null! : i with
            { EvidenceMediaIds = i.EvidenceMediaIds?.Order().ToArray()! }).ToArray()! };
        return MutateAsync(orderId, request.OperationId, request.BatchId, request.ExpectedResultVersion, "Findings", null, normalized,
            async (s, ct) =>
            {
                if (normalized.Items is not { Count: > 0 and <= 100 } || normalized.Items.Any(i => i is null) ||
                    normalized.Items.Select(i => i.FindingId).Distinct().Count() != normalized.Items.Count)
                    Invalid("InvalidRequest", "Batch requires one to 100 unique finding items.");
                List<Prepared> items = [];
                foreach (var item in normalized.Items) items.Add(await PrepareFindingAsync(s, item, ct));
                return items;
            }, token);
    }

    private async Task<Prepared> PrepareFindingAsync(Session s, HealthFindingReviewItem item, CancellationToken token)
    {
        if (!s.Findings.TryGetValue(item.FindingId, out var finding)) NotFound();
        if (finding.SourceVersion != item.ExpectedSourceVersion) Conflict("VersionConflict", "Finding source revision changed.");
        Evidence(s, item.Reason, item.EvidenceMediaIds);
        var decision = Decision(item.Decision, HealthReviewDecision.Approve, HealthReviewDecision.Correct,
            HealthReviewDecision.Reject, HealthReviewDecision.NonDisease, HealthReviewDecision.Ambiguous);
        var corrected = item.CorrectedClassCode is not null || item.CorrectedPlantId.HasValue || item.CorrectedPosition is not null;
        if ((decision == HealthReviewDecision.Correct) != corrected)
            Invalid("InvalidDecision", "Only Correct accepts corrections, and it requires at least one explicit corrected field.");
        var source = finding.Original.Deserialize<PlantHealthFindingV4>() ?? throw new InvalidOperationException("Finding source is missing.");
        HealthReviewSnapshot snapshot;
        if (decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct)
        {
            var condition = Condition(s, item.CorrectedClassCode ?? finding.SuspectedClassCode);
            var plantId = item.CorrectedPlantId ?? finding.PlantId;
            PlantAssociation(s, plantId);
            if (item.CorrectedPosition is not null && item.CorrectedPosition.Type != "Point") Invalid("InvalidGeometry", "Position must be a GeoJSON Point.");
            var point = Point(item.CorrectedPosition?.Coordinates ?? source.Position.Coordinates);
            var evidence = item.EvidenceMediaIds.Concat(source.EvidenceMedia.Select(m => m.MediaAssetId)).Distinct().Order().ToArray();
            await CheckPositionAsync(s, point, evidence.Length != 0, token);
            snapshot = new(plantId, condition.Code, condition.Id, condition.RevisionNumber, [point.X, point.Y],
                EvidenceMediaIds: evidence, AnnotationRuleVersion: condition.AnnotationRuleVersion);
        }
        else
        {
            if (decision == HealthReviewDecision.NonDisease && !RuleValid(s))
                Invalid("CatalogueMismatch", "Annotation rules must match the configured dataset.");
            snapshot = new(EvidenceMediaIds: item.EvidenceMediaIds.Order().ToArray(), AnnotationRuleVersion: s.Source.Provenance.AnnotationRuleVersion);
        }
        return new(HealthReviewKind.Finding, finding.Id, item.ExpectedReviewVersion, decision, item.Reason, snapshot);
    }

    public Task<Result<HealthReviewOutcome>> ReviewBoundaryAsync(Guid orderId, Guid exceptionId, HealthBoundaryReviewRequest request, CancellationToken token) =>
        MutateAsync(orderId, request.OperationId, request.BatchId, request.ExpectedResultVersion, "Boundary", exceptionId,
            request with { EvidenceMediaIds = request.EvidenceMediaIds?.Order().ToArray()! }, async (s, ct) =>
            {
                if (!s.Exceptions.TryGetValue(exceptionId, out var exception)) NotFound();
                if (exception.Version != request.ExpectedExceptionVersion) Conflict("VersionConflict", "Boundary exception version changed.");
                Evidence(s, request.Reason, request.EvidenceMediaIds);
                var decision = Decision(request.Decision, HealthReviewDecision.AcceptCorrectedPosition,
                    HealthReviewDecision.ExcludeEvidence, HealthReviewDecision.NeedsEvidence);
                var snapshot = new HealthReviewSnapshot(EvidenceMediaIds: request.EvidenceMediaIds!.Order().ToArray());
                if (decision == HealthReviewDecision.NeedsEvidence)
                {
                    if (request.FindingId.HasValue || request.FindingReviewId.HasValue)
                        Invalid("InvalidAssociation", "NeedsEvidence cannot claim a resolved finding association.");
                }
                else
                {
                    var id = request.FindingId.GetValueOrDefault();
                    if (id == Guid.Empty || !s.Findings.ContainsKey(id))
                        Invalid("InvalidAssociation", "A finding in this active batch is required.");
                    var finding = s.Findings[id];
                    var review = Current(s, HealthReviewKind.Finding, id);
                    if (review is null || review.Id != request.FindingReviewId) Conflict("DependencyChanged", "The finding review changed or is missing.");
                    if (Guid.TryParse(exception.SourceReferenceId, out var sourceId) && sourceId != finding.SourceFindingId)
                        Invalid("InvalidAssociation", "Exception source does not match the selected finding.");
                    if (!Guid.TryParse(exception.SourceReferenceId, out _) && request.EvidenceMediaIds!.Count == 0)
                        Invalid("InvalidEvidence", "Associating an external source reference requires explicit supporting media evidence.");
                    var value = review.ReadSnapshot();
                    if (decision == HealthReviewDecision.ExcludeEvidence && review.Decision is not (HealthReviewDecision.Reject or HealthReviewDecision.NonDisease))
                        Invalid("InvalidDecision", "Only rejected/non-disease evidence can be excluded.");
                    if (decision == HealthReviewDecision.AcceptCorrectedPosition)
                    {
                        if (!IsAccepted(review) || FindingIssues(s, id).Count != 0) Invalid("InvalidAssociation", "A valid accepted finding review is required.");
                        await CheckPositionAsync(s, Point(value.Position), request.EvidenceMediaIds!.Count != 0 || value.EvidenceMediaIds is { Count: > 0 }, ct);
                    }
                    snapshot = snapshot with { BoundFindingId = id, BoundReviewId = review.Id, PlantId = value.PlantId, Position = value.Position };
                }
                return [new(HealthReviewKind.Boundary, exceptionId, request.ExpectedReviewVersion, decision, request.Reason, snapshot)];
            }, token);

    public Task<Result<HealthReviewOutcome>> ReviewZoneAsync(Guid orderId, Guid zoneId, HealthZoneReviewRequest request, CancellationToken token) =>
        MutateAsync(orderId, request.OperationId, request.BatchId, request.ExpectedResultVersion, "Zone", zoneId,
            request with { EvidenceMediaIds = request.EvidenceMediaIds?.Order().ToArray()!, Members = request.Members?.OrderBy(m => m?.FindingId).ToArray() }, async (s, ct) =>
            {
                if (!s.Zones.TryGetValue(zoneId, out var zone)) NotFound();
                if (zone.Version != request.ExpectedZoneVersion) Conflict("VersionConflict", "Zone version changed.");
                Evidence(s, request.Reason, request.EvidenceMediaIds);
                var decision = Decision(request.Decision, HealthReviewDecision.Approve, HealthReviewDecision.Correct, HealthReviewDecision.Reject, HealthReviewDecision.Ambiguous);
                var corrected = request.CorrectedGeometry is not null || request.CorrectedClassCode is not null || request.Members is not null;
                if ((decision == HealthReviewDecision.Correct) != corrected)
                    Invalid("InvalidDecision", "Only Correct accepts zone corrections and requires an explicit corrected field.");
                var snapshot = new HealthReviewSnapshot(EvidenceMediaIds: request.EvidenceMediaIds!.Order().ToArray(), AnnotationRuleVersion: s.Source.Provenance.AnnotationRuleVersion);
                if (decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct)
                {
                    var condition = Condition(s, request.CorrectedClassCode ?? zone.SuspectedClassCode);
                    var source = zone.ProposalEvidence.Deserialize<ProposedDiseaseZoneV4>() ?? throw new InvalidOperationException("Zone source is missing.");
                    var geometry = Polygon(request.CorrectedGeometry?.ToGeoJson() ?? source.Geometry);
                    var covered = await _db.Database.SqlQuery<bool>($"SELECT ST_Covers(geometry, {geometry}) AS \"Value\" FROM farm.farm_boundaries WHERE id = {s.Batch.FarmBoundaryVersionId}")
                        .SingleAsync(ct);
                    if (!covered) Invalid("InvalidGeometry", "Reviewed zone must be inside the capture FarmBoundary.");
                    IReadOnlyList<HealthZoneMember> requestedMembers;
                    if (request.Members is not null) requestedMembers = request.Members;
                    else
                    {
                        var resolved = new List<HealthZoneMember>();
                        foreach (var original in source.Memberships)
                        {
                            var matches = s.Findings.Values.Where(f =>
                                original.PlantId.HasValue && f.PlantId == original.PlantId ||
                                original.MappingCandidateId.HasValue && f.MappingCandidateId == original.MappingCandidateId)
                                .Where(f => Current(s, HealthReviewKind.Finding, f.Id) is { } r && IsAccepted(r) && r.ReadSnapshot().ClassCode == condition.Code).ToArray();
                            if (matches.Length != 1) Invalid("InvalidAssociation", "Original members must resolve unambiguously; submit Correct with explicit finding reviews.");
                            var match = matches[0];
                            resolved.Add(new(match.Id, match.SourceVersion, Current(s, HealthReviewKind.Finding, match.Id)!.Id));
                        }
                        requestedMembers = resolved;
                    }
                    if (requestedMembers.Count is < 1 or > 10000 || requestedMembers.Any(m => m is null) ||
                        requestedMembers.Select(m => m.FindingId).Distinct().Count() != requestedMembers.Count)
                        Invalid("InvalidAssociation", "Reviewed members must be a non-empty unique collection of findings.");
                    var members = new List<ReviewedHealthMember>();
                    foreach (var member in requestedMembers)
                    {
                        if (!s.Findings.TryGetValue(member.FindingId, out var finding)) NotFound();
                        var review = Current(s, HealthReviewKind.Finding, member.FindingId);
                        if (finding.SourceVersion != member.SourceVersion || review?.Id != member.FindingReviewId || !IsAccepted(review) || FindingIssues(s, member.FindingId).Count != 0)
                            Conflict("DependencyChanged", "Zone members require current accepted finding reviews.");
                        var value = review!.ReadSnapshot();
                        if (value.ClassCode != condition.Code || !geometry.Covers(Point(value.Position)))
                            Invalid("InvalidAssociation", "Zone members must share the reviewed class and be inside the reviewed geometry.");
                        members.Add(new(finding.Id, finding.SourceVersion, review.Id, value.PlantId!.Value));
                    }
                    if (members.Select(m => m.PlantId).Distinct().Count() != members.Count)
                        Invalid("InvalidAssociation", "A reviewed zone may include each Plant only once.");
                    snapshot = snapshot with { ClassCode = condition.Code, ConditionId = condition.Id, ConditionRevision = condition.RevisionNumber,
                        Geometry = (request.CorrectedGeometry?.ToGeoJson() ?? source.Geometry).Coordinates.Select(r => r.Select(p => p.ToArray()).ToArray()).ToArray(), Members = members };
                }
                return [new(HealthReviewKind.Zone, zoneId, request.ExpectedReviewVersion, decision, request.Reason, snapshot)];
            }, token);

    public Task<Result<HealthReviewOutcome>> ReviewGuidanceAsync(Guid orderId, Guid zoneId, HealthGuidanceReviewRequest request, CancellationToken token) =>
        MutateAsync(orderId, request.OperationId, request.BatchId, request.ExpectedResultVersion, "Guidance", zoneId,
            request with { EvidenceMediaIds = request.EvidenceMediaIds?.Order().ToArray()! }, (s, _) =>
            {
                if (!s.Zones.TryGetValue(zoneId, out var zone)) NotFound();
                if (zone.Version != request.ExpectedZoneVersion) Conflict("VersionConflict", "Zone version changed.");
                Evidence(s, request.Reason, request.EvidenceMediaIds);
                var zoneReview = Current(s, HealthReviewKind.Zone, zoneId);
                if (!IsAccepted(zoneReview) || zoneReview!.Id != request.ZoneReviewId || ZoneIssues(s, zone, false).Count != 0)
                    Conflict("DependencyChanged", "Review the zone and its members before deciding guidance.");
                var decision = Decision(request.Decision, HealthReviewDecision.Select, HealthReviewDecision.RejectAll, HealthReviewDecision.NeedsEvidence);
                if ((decision == HealthReviewDecision.Select) != request.GuidanceId.HasValue)
                    Invalid("InvalidDecision", "Select requires GuidanceId; other decisions must omit it.");
                var snapshot = new HealthReviewSnapshot(BoundReviewId: zoneReview!.Id, EvidenceMediaIds: request.EvidenceMediaIds!.Order().ToArray());
                if (request.GuidanceId is Guid guidanceId)
                {
                    if (!s.Guidance.TryGetValue(guidanceId, out var entry)) Invalid("GuidanceNotApplicable", "Sourced guidance was not found.");
                    var candidate = new ReviewedGuidanceSnapshot(entry.Id, entry.Code, entry.VersionNumber, entry.SourceReference,
                        entry.SuspectedClassCode, entry.PlantConditionId, entry.PlantConditionRevisionNumber, entry.EffectiveFrom, entry.EffectiveTo);
                    if (!GuidanceValid(s, zoneReview.ReadSnapshot(), candidate)) Invalid("GuidanceNotApplicable", "Guidance must be active/effective for the exact reviewed class and condition revision.");
                    snapshot = snapshot with { Guidance = candidate };
                }
                IReadOnlyList<Prepared> prepared = [new(HealthReviewKind.Guidance, zoneId, request.ExpectedReviewVersion, decision, request.Reason, snapshot)];
                return Task.FromResult(prepared);
            }, token);
}
