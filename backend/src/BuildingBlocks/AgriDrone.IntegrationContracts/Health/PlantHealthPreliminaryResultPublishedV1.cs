using System.Text.Json.Serialization;

namespace AgriDrone.IntegrationContracts.Health;

/// <summary>Only reviewed, published preliminary values. No pending AI payload or media URLs.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantHealthPreliminaryResultPublishedV1(
    Guid CausationId, Guid PublicationId, Guid HandoffId, Guid JobId, Guid SurveyOrderId,
    Guid MissionId, Guid FarmId, Guid SurveyResultId, Guid BatchId, int SourceVersion,
    Guid FarmBoundaryVersionId, Guid FarmBaseMapVersionId, long PublicationVersion,
    long ApprovedReviewRevision, string ApprovalFingerprint, string Outcome,
    string ResultNature, string TeamLabelLimitation, string Conclusion,
    PlantHealthProvenanceV4 Provenance, Guid ApprovedBy, DateTimeOffset ApprovedAt,
    Guid PublishedBy, DateTimeOffset PublishedAt,
    IReadOnlyList<PublishedPlantHealthFindingV1> Findings,
    IReadOnlyList<PublishedPlantHealthZoneV1> DiseaseZones,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlantHealthPublicationComparisonV1? Comparison = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantHealthPublicationComparisonV1(string PolicyVersion, string Reason,
    Guid? PreviousOrderId, Guid? PreviousResultId, Guid? PreviousPublicationId,
    long? PreviousPublicationVersion, DateTimeOffset? PreviousObservedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishedPlantHealthFindingV1(
    Guid FindingId, Guid SourceFindingId, int SourceVersion, Guid PlantId, Guid ReviewId,
    string Decision, string SuspectedClassCode, Guid PlantConditionId, int ConditionRevision,
    double[] Position, DateTimeOffset ObservedAt, decimal Confidence, string Uncertainty,
    IReadOnlyList<string> Limitations, Guid ReviewedBy, DateTimeOffset ReviewedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishedPlantHealthZoneMemberV1(Guid FindingId, Guid ReviewId, Guid PlantId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishedPlantHealthGuidanceV1(
    Guid Id, string Code, int VersionNumber, string SuspectedClassCode,
    string SourceTitle, string SourceReference, string GuidanceText,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishedPlantHealthZoneV1(
    Guid ZoneId, Guid ReviewId, string SuspectedClassCode, double[][][] Geometry,
    int MembershipVersion, IReadOnlyList<PublishedPlantHealthZoneMemberV1> Members,
    Guid GuidanceReviewId, string GuidanceDecision, PublishedPlantHealthGuidanceV1? Guidance);

/// <summary>Post-commit notification intent; recipient ownership is resolved by its consumer.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantHealthPublicationNotificationRequestedV1(
    Guid CausationId, Guid PublicationId, Guid SurveyOrderId, Guid SurveyResultId,
    Guid FarmId, long PublicationVersion, string ResultNature, string TeamLabelLimitation,
    DateTimeOffset PublishedAt);
