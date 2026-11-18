using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureUploadManifestEvidence
{
    public Guid TenantId { get; }
    public Guid FarmId { get; }
    public Guid OrderId { get; }
    public Guid AttemptId { get; }
    public Guid MissionId { get; }
    public string ManifestId { get; }
    public string ManifestFingerprint { get; }

    public CaptureUploadManifestEvidence(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        Guid attemptId,
        Guid missionId,
        string manifestId,
        string manifestFingerprint)
    {
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(orderId, nameof(orderId));
        DomainGuard.NotEmpty(attemptId, nameof(attemptId));
        DomainGuard.NotEmpty(missionId, nameof(missionId));

        ManifestId = PolicyValidationGuard.NormalizeRequired(manifestId, 200, nameof(manifestId));
        ManifestFingerprint = PolicyValidationGuard.NormalizeRequired(manifestFingerprint, 128, nameof(manifestFingerprint));

        TenantId = tenantId;
        FarmId = farmId;
        OrderId = orderId;
        AttemptId = attemptId;
        MissionId = missionId;
    }
}
