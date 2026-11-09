using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.IntegrationContracts.Messaging.Validation;

public static class PlantHealthPublicationValidators
{
    public static IReadOnlyList<string> Validate(PlantHealthPreliminaryResultPublishedV1? p)
    {
        List<string> errors = [];
        if (p is null) return ["Published Plant Health payload is required."];
        if (new[] { p.CausationId, p.PublicationId, p.HandoffId, p.JobId, p.SurveyOrderId, p.MissionId, p.FarmId,
                p.SurveyResultId, p.BatchId, p.FarmBoundaryVersionId, p.FarmBaseMapVersionId, p.ApprovedBy, p.PublishedBy }.Contains(Guid.Empty))
            errors.Add("Publication/source/capture/actor IDs are required.");
        if (p.PublicationVersion <= 0 || p.SourceVersion <= 0 || p.ApprovedReviewRevision < 0 || !Hash(p.ApprovalFingerprint))
            errors.Add("Publication/source/review versions and approval fingerprint are invalid.");
        if (p.Outcome != "PUBLISHED" || p.ResultNature != PlantHealthV4Values.ResultNature ||
            string.IsNullOrWhiteSpace(p.TeamLabelLimitation) || p.TeamLabelLimitation.Length > 2000)
            errors.Add("Only published preliminary suspected results with a team-label limitation are accepted.");
        if (!Utc(p.ApprovedAt) || !Utc(p.PublishedAt) || p.ApprovedAt > p.PublishedAt)
            errors.Add("Approval/publication timestamps are invalid.");
        if (p.Provenance is null || p.Provenance.ModelVersionId == Guid.Empty ||
            new[] { p.Provenance.DatasetId, p.Provenance.DatasetVersion, p.Provenance.AnnotationRuleVersion,
                p.Provenance.ModelVersion, p.Provenance.BoundaryPolicyVersion }.Any(string.IsNullOrWhiteSpace))
            errors.Add("Dataset/annotation/model/boundary provenance is required.");
        if (p.Findings is null || p.DiseaseZones is null || p.Findings.Any(f => f is null) || p.DiseaseZones.Any(z => z is null))
            return errors.Append("Published finding and zone collections are required.").ToArray();
        if (p.Conclusion != (p.Findings.Count == 0 ? "NO_ACCEPTED_SUSPECTED_FINDINGS" : "HAS_REVIEWED_SUSPECTED_FINDINGS"))
            errors.Add("The preliminary conclusion must agree with the published finding set.");
        if (p.Findings.Select(f => f.FindingId).Distinct().Count() != p.Findings.Count ||
            p.DiseaseZones.Select(z => z.ZoneId).Distinct().Count() != p.DiseaseZones.Count)
            errors.Add("Published item IDs must be unique.");
        foreach (var f in p.Findings)
            if (new[] { f.FindingId, f.SourceFindingId, f.PlantId, f.ReviewId, f.PlantConditionId, f.ReviewedBy }.Contains(Guid.Empty) ||
                f.SourceVersion <= 0 || f.ConditionRevision <= 0 || f.Decision is not ("Approve" or "Correct") ||
                string.IsNullOrWhiteSpace(f.SuspectedClassCode) || !Point(f.Position) || f.Confidence is < 0 or > 1 ||
                string.IsNullOrWhiteSpace(f.Uncertainty) || f.Limitations is null || f.Limitations.Any(string.IsNullOrWhiteSpace) ||
                !Utc(f.ObservedAt) || f.ObservedAt > f.ReviewedAt || !Utc(f.ReviewedAt) || f.ReviewedAt > p.ApprovedAt)
                errors.Add("Published findings require accepted reviewed values, source uncertainty and valid timestamps.");
        var findings = p.Findings.GroupBy(f => f.FindingId).ToDictionary(g => g.Key, g => g.First());
        foreach (var z in p.DiseaseZones)
        {
            if (z.ZoneId == Guid.Empty || z.ReviewId == Guid.Empty || z.GuidanceReviewId == Guid.Empty || z.MembershipVersion <= 1 ||
                string.IsNullOrWhiteSpace(z.SuspectedClassCode) || !Polygon(z.Geometry) || z.Members is not { Count: > 0 } ||
                z.Members.Any(m => m is null) || z.GuidanceDecision is not ("Select" or "RejectAll"))
            { errors.Add("Published zone geometry, members and guidance decision are invalid."); continue; }
            if (z.Members.Select(m => m.PlantId).Distinct().Count() != z.Members.Count || z.Members.Any(m =>
                    !findings.TryGetValue(m.FindingId, out var f) || f.ReviewId != m.ReviewId || f.PlantId != m.PlantId || f.SuspectedClassCode != z.SuspectedClassCode))
                errors.Add("Zone memberships must bind unique Plants and exact accepted finding reviews/classes.");
            var g = z.Guidance;
            if (z.GuidanceDecision == "RejectAll" ? g is not null : g is null || g.Id == Guid.Empty || g.VersionNumber <= 0 ||
                g.SuspectedClassCode != z.SuspectedClassCode || new[] { g.Code, g.SourceTitle, g.SourceReference, g.GuidanceText }.Any(string.IsNullOrWhiteSpace) ||
                !Utc(g.EffectiveFrom) || g.EffectiveFrom > p.PublishedAt || g.EffectiveTo.HasValue && (!Utc(g.EffectiveTo.Value) || g.EffectiveTo <= p.PublishedAt))
                errors.Add("Selected guidance must retain its effective cited version; RejectAll carries no guidance.");
        }
        if (p.Comparison is { } c && (c.PolicyVersion != "plant-health-publication-comparison.v1" || string.IsNullOrWhiteSpace(c.Reason) ||
            (c.Reason == "COMPATIBLE" ? c.PreviousOrderId is null || c.PreviousOrderId == Guid.Empty || c.PreviousOrderId == p.SurveyOrderId ||
                c.PreviousResultId is null || c.PreviousResultId == Guid.Empty || c.PreviousResultId == p.SurveyResultId ||
                c.PreviousPublicationId is null || c.PreviousPublicationId == Guid.Empty || c.PreviousPublicationId == p.PublicationId ||
                c.PreviousPublicationVersion is null or <= 0 || c.PreviousPublicationVersion >= p.PublicationVersion ||
                c.PreviousObservedAt is null || !Utc(c.PreviousObservedAt.Value) || p.Findings.Count == 0 || c.PreviousObservedAt >= p.Findings.Min(f => f.ObservedAt)
                : c.PreviousOrderId.HasValue || c.PreviousResultId.HasValue || c.PreviousPublicationId.HasValue || c.PreviousPublicationVersion.HasValue || c.PreviousObservedAt.HasValue)))
            errors.Add("Comparison must identify an earlier compatible publication or explicitly record no comparison.");
        return errors;
    }

    public static IReadOnlyList<string> Validate(PlantHealthPublicationNotificationRequestedV1? p) =>
        p is null || new[] { p.CausationId, p.PublicationId, p.SurveyOrderId, p.SurveyResultId, p.FarmId }.Contains(Guid.Empty) ||
        p.PublicationVersion <= 0 || p.ResultNature != PlantHealthV4Values.ResultNature ||
        string.IsNullOrWhiteSpace(p.TeamLabelLimitation) || p.TeamLabelLimitation.Length > 2000 || !Utc(p.PublishedAt)
            ? ["A published preliminary notification reference with valid IDs, version, timestamp and limitation is required."] : [];

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Utc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
    private static bool Point(double[]? point) => point is { Length: 2 } && double.IsFinite(point[0]) && double.IsFinite(point[1]) &&
        point[0] is >= -180 and <= 180 && point[1] is >= -90 and <= 90;
    private static bool Polygon(double[][][]? rings) => rings is { Length: > 0 and <= 128 } && rings.All(r =>
        r is { Length: >= 4 and <= 10000 } && r.All(Point) && r[0].SequenceEqual(r[^1]));
}
