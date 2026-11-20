using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Maps;

public sealed record FarmBaseMapInternallyApprovedResult(
    Guid FarmBaseMapVersionId,
    int VersionNumber,
    Guid SurveyOrderId,
    Guid FarmId,
    int VerifiedBillablePoleCount,
    Guid PoleCountSnapshotId,
    string EvidenceFingerprint,
    DateTimeOffset ApprovedAt);

public interface IFarmBaseMapInternalApprovalOrchestrator
{
    Task<Result<FarmBaseMapInternallyApprovedResult>> ApproveInternalAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken cancellationToken);
}
