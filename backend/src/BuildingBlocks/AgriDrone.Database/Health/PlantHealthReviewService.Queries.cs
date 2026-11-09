using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService
{
    public Task<Result<HealthReviewPage>> GetAsync(Guid orderId, string kind, HealthReviewQuery query, CancellationToken token) =>
        ReadAsync(orderId, query.BatchId, (s, _) =>
        {
            ValidateQuery(query);
            List<HealthReviewItemView> items;
            if (kind == "findings")
            {
                var findings = s.Findings.Values.AsEnumerable();
                if (query.ZoneId is Guid zoneId)
                {
                    if (!s.Zones.TryGetValue(zoneId, out var zone)) NotFound();
                    var reviewed = Current(s, HealthReviewKind.Zone, zoneId)?.ReadSnapshot().Members;
                    if (reviewed is not null)
                    {
                        var memberIds = reviewed.Select(m => m.FindingId).ToHashSet();
                        findings = findings.Where(f => memberIds.Contains(f.Id));
                    }
                    else findings = findings.Where(f => zone.Memberships.Any(m =>
                        m.PlantId.HasValue && f.PlantId == m.PlantId || m.MappingCandidateId.HasValue && f.MappingCandidateId == m.MappingCandidateId));
                }
                items = findings.OrderByDescending(f => f.ObservedAt).ThenBy(f => f.Id).Select(f =>
                {
                    var issues = FindingIssues(s, f.Id);
                    return new HealthReviewItemView(f.Id, f.SourceVersion, f.Version, f.ObservedAt,
                        issues.Count == 0 ? "RESOLVED" : Current(s, HealthReviewKind.Finding, f.Id) is null ? f.ResolutionState : "NEEDS_REVIEW",
                        f.Original.RootElement.Clone(), View(s, HealthReviewKind.Finding, f.Id), issues);
                }).ToList();
                if (query.ClassCode is not null)
                    items = items.Where(i => string.Equals(FindingClass(s, i.Id), query.ClassCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (kind == "exceptions")
                items = s.Exceptions.Values.OrderBy(e => e.Id).Select(e => new HealthReviewItemView(e.Id, null, e.Version, s.Batch.ProducedAt,
                    BoundaryIssues(s, e.Id).Count == 0 ? "RESOLVED" : "NEEDS_REVIEW", e.Original.RootElement.Clone(),
                    View(s, HealthReviewKind.Boundary, e.Id), BoundaryIssues(s, e.Id))).ToList();
            else if (kind == "zones")
            {
                items = s.Zones.Values.OrderByDescending(z => z.CreatedAt).ThenBy(z => z.Id).Select(z => new HealthReviewItemView(z.Id, null, z.Version,
                    z.CreatedAt, ZoneIssues(s, z).Count == 0 ? "RESOLVED" : "NEEDS_REVIEW", z.ProposalEvidence.RootElement.Clone(),
                    View(s, HealthReviewKind.Zone, z.Id), ZoneIssues(s, z), View(s, HealthReviewKind.Guidance, z.Id))).ToList();
                if (query.ClassCode is not null)
                    items = items.Where(i => string.Equals(Current(s, HealthReviewKind.Zone, i.Id)?.ReadSnapshot().ClassCode ?? s.Zones[i.Id].SuspectedClassCode, query.ClassCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else { Invalid("InvalidRequest", "Unknown review queue."); return Task.FromResult<HealthReviewPage>(null!); }
            if (query.ZoneId.HasValue && kind != "findings") Invalid("InvalidRequest", "ZoneId filter is supported only for findings.");
            if (query.Decision is not null) items = items.Where(i => (i.Review?.Decision ?? "Pending") == query.Decision).ToList();
            if (query.ResolutionState is not null) items = items.Where(i => i.ResolutionState == query.ResolutionState).ToList();
            return Task.FromResult(Page(s, query, items.Count, items.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArray()));
        }, token);

    public Task<Result<HealthReviewPage>> GetHistoryAsync(Guid orderId, Guid findingId, HealthReviewQuery query, CancellationToken token) =>
        ReadAsync(orderId, query.BatchId, async (s, ct) =>
        {
            ValidateQuery(query);
            if (query.Decision is not null || query.ClassCode is not null || query.ZoneId.HasValue || query.ResolutionState is not null)
                Invalid("InvalidRequest", "History supports only batch and pagination filters.");
            if (!s.Findings.TryGetValue(findingId, out var finding)) NotFound();
            var history = _db.HealthReviews.AsNoTracking().Where(r => r.BatchId == s.Batch.Id && r.Kind == HealthReviewKind.Finding && r.SubjectId == findingId);
            var count = await history.CountAsync(ct);
            var reviews = await history.OrderByDescending(r => r.ReviewedAt).ThenByDescending(r => r.Id)
                .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
            var head = s.Heads.GetValueOrDefault((HealthReviewKind.Finding, findingId));
            var items = reviews.Select(r => new HealthReviewItemView(finding.Id, finding.SourceVersion, finding.Version,
                finding.ObservedAt, "HISTORY", finding.Original.RootElement.Clone(),
                new(r.Id, head?.CurrentReviewId == r.Id ? head.Version : null, r.Decision.ToString(), r.ReviewedBy,
                    r.ReviewedAt, r.Reason, r.Snapshot.RootElement.Clone()), [])).ToArray();
            return Page(s, query, count, items);
        }, token);

    public Task<Result<IReadOnlyList<HealthGuidanceOption>>> GetGuidanceOptionsAsync(Guid orderId, Guid zoneId, CancellationToken token) =>
        ReadAsync<IReadOnlyList<HealthGuidanceOption>>(orderId, null, (s, _) =>
        {
            if (!s.Zones.ContainsKey(zoneId)) NotFound();
            var review = Current(s, HealthReviewKind.Zone, zoneId);
            if (!IsAccepted(review)) Conflict("DependencyChanged", "Review the zone class before selecting guidance.");
            var value = review!.ReadSnapshot();
            if (!ConditionValid(s, value)) Conflict("DependencyChanged", "The reviewed class or catalogue revision changed.");
            IReadOnlyList<HealthGuidanceOption> options = s.Guidance.Values.Where(g => g.Status == SourcedGuidanceStatus.Active &&
                    g.IsEffectiveAt(s.Now) && g.SuspectedClassCode == value.ClassCode && g.PlantConditionId == value.ConditionId && g.PlantConditionRevisionNumber == value.ConditionRevision)
                .OrderBy(g => g.Code, StringComparer.Ordinal).ThenBy(g => g.VersionNumber).Select(g => new HealthGuidanceOption(g.Id, g.Code,
                    g.VersionNumber, g.SuspectedClassCode, g.SourceTitle, g.SourceReference, g.GuidanceText, g.EffectiveFrom, g.EffectiveTo)).ToArray();
            return Task.FromResult(options);
        }, token);

    public Task<Result<HealthReviewSummary>> GetSummaryAsync(Guid orderId, CancellationToken token) =>
        ReadAsync(orderId, null, (s, _) => Task.FromResult(Summary(s)), token);

    private static HealthReviewSummary Summary(Session s, bool forPublication = false)
    {
        var findingIssues = s.Findings.Keys.ToDictionary(id => id, id => FindingIssues(s, id));
        var boundaryIssues = s.Exceptions.Keys.ToDictionary(id => id, id => BoundaryIssues(s, id));
        var zoneIssues = s.Zones.Values.ToDictionary(z => z.Id, z => ZoneIssues(s, z));
        var findingReviews = s.Findings.Keys.Select(id => Current(s, HealthReviewKind.Finding, id)).ToArray();
        var zoneReviews = s.Zones.Keys.Select(id => Current(s, HealthReviewKind.Zone, id)).ToArray();
        var ready = s.Result.ActiveImportBatchId == s.Batch.Id && s.Result.Status == (forPublication ? SurveyResultStatus.Approved : SurveyResultStatus.PendingReview) &&
            s.Order.Status == SurveyOrderStatus.PendingReview && RuleValid(s) && findingIssues.Values.All(i => i.Count == 0) &&
            boundaryIssues.Values.All(i => i.Count == 0) && zoneIssues.Values.All(i => i.Count == 0);
        return new(s.Result.Id, s.Batch.Id, s.Result.Version, s.Result.ReviewRevision,
            findingReviews.Count(r => r is null), findingReviews.Count(r => r?.Decision == HealthReviewDecision.Ambiguous),
            findingReviews.Count(IsAccepted), findingReviews.Count(r => r?.Decision == HealthReviewDecision.Reject),
            findingReviews.Count(r => r?.Decision == HealthReviewDecision.NonDisease), boundaryIssues.Values.Count(i => i.Count != 0),
            zoneReviews.Count(r => r is null || r.Decision == HealthReviewDecision.Ambiguous),
            zoneReviews.Count(r => r?.Decision == HealthReviewDecision.Reject), zoneReviews.Count(IsAccepted),
            zoneIssues.Values.Count(i => i.Contains("MissingGuidanceDecision") || i.Contains("GuidanceNotApplicable")),
            findingIssues.Values.Count(i => i.Contains("CatalogueChanged") || i.Contains("AnnotationRuleChanged") || i.Contains("PlantAssociationChanged")) +
                boundaryIssues.Values.Count(i => i.Contains("StaleBoundaryAssociation")) + zoneIssues.Values.Count(i => i.Contains("StaleMemberReview") || i.Contains("CatalogueChanged")) + (RuleValid(s) ? 0 : 1), ready);
    }

    private static HealthReviewStateView? View(Session s, HealthReviewKind kind, Guid id)
    {
        var entry = Current(s, kind, id);
        return entry is null ? null : new(entry.Id, s.Heads[(kind, id)].Version, entry.Decision.ToString(),
            entry.ReviewedBy, entry.ReviewedAt, entry.Reason, entry.Snapshot.RootElement.Clone());
    }
    private static string? FindingClass(Session s, Guid id)
    {
        var reviewed = Current(s, HealthReviewKind.Finding, id)?.ReadSnapshot().ClassCode;
        if (reviewed is not null) return reviewed;
        return s.Findings[id].Original.RootElement.TryGetProperty("SuspectedClassCode", out var original)
            ? original.GetString() : s.Findings[id].SuspectedClassCode;
    }
    private static HealthReviewPage Page(Session s, HealthReviewQuery q, int count, IReadOnlyList<HealthReviewItemView> items) =>
        new(s.Result.Id, s.Batch.Id, s.Result.Version, s.Result.ReviewRevision, s.Result.ResultNature!, s.Result.TeamLabelLimitation!,
            s.Result.ActiveImportBatchId == s.Batch.Id, q.PageNumber, q.PageSize, count, items);
    private static void ValidateQuery(HealthReviewQuery query)
    {
        if (query.PageNumber is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.BatchId == Guid.Empty || query.ZoneId == Guid.Empty || query.ClassCode?.Length > 80)
            Invalid("InvalidRequest", "Pagination or filter is invalid; page size must be between 1 and 100.");
        if (query.Decision is not null && query.Decision != "Pending" && (!Enum.TryParse<HealthReviewDecision>(query.Decision, out var decision) || decision.ToString() != query.Decision))
            Invalid("InvalidDecision", "Unknown review decision filter.");
        if (query.ResolutionState is not null and not ("PENDING_REVIEW" or "NEEDS_REVIEW" or "RESOLVED"))
            Invalid("InvalidRequest", "Unknown resolution-state filter.");
    }
}
