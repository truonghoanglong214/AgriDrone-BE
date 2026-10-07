using System.Text.Json;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public sealed record DiseaseZoneProposal
{
    public required Guid TenantId { get; init; }
    public required Guid FarmId { get; init; }
    public required Guid SurveyOrderId { get; init; }
    public required Guid SurveyResultId { get; init; }
    public required Guid FarmBoundaryVersionId { get; init; }
    public required Guid FarmBaseMapVersionId { get; init; }
    public required Guid PlantConditionId { get; init; }
    public required Guid HealthLevelId { get; init; }
    public required Guid SourceHandoffId { get; init; }
    public Guid? SourceJobId { get; init; }
    public required string SourceProposalId { get; init; }
    public required Polygon Geometry { get; init; }
    public required IReadOnlyCollection<DiseaseZoneMembershipCandidate> Membership { get; init; }
    public required JsonDocument RecommendationCandidates { get; init; }
    public required JsonDocument Evidence { get; init; }
    public required DateTimeOffset ProposedAt { get; init; }

    internal void Validate()
    {
        DomainGuard.NotEmpty(TenantId);
        DomainGuard.NotEmpty(FarmId);
        DomainGuard.NotEmpty(SurveyOrderId);
        DomainGuard.NotEmpty(SurveyResultId);
        DomainGuard.NotEmpty(FarmBoundaryVersionId);
        DomainGuard.NotEmpty(FarmBaseMapVersionId);
        DomainGuard.NotEmpty(PlantConditionId);
        DomainGuard.NotEmpty(HealthLevelId);
        DomainGuard.NotEmpty(SourceHandoffId);
        DomainGuard.Utc(ProposedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceProposalId);
        ArgumentNullException.ThrowIfNull(Geometry);
        ArgumentNullException.ThrowIfNull(Membership);
        ArgumentNullException.ThrowIfNull(RecommendationCandidates);
        ArgumentNullException.ThrowIfNull(Evidence);

        if (SourceJobId == Guid.Empty)
        {
            throw new ArgumentException("Source job identifier cannot be empty.", nameof(SourceJobId));
        }

        if (SourceProposalId.Trim().Length > 200)
        {
            throw new ArgumentException("Source proposal identifier cannot exceed 200 characters.", nameof(SourceProposalId));
        }
    }
}
