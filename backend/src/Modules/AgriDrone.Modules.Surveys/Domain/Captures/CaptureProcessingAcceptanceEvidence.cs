using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureProcessingAcceptanceEvidence
{
    public Guid TenantId { get; }
    public Guid FarmId { get; }
    public Guid OrderId { get; }
    public Guid AttemptId { get; }
    public Guid MissionId { get; }
    public string ProcessingJobId { get; }
    public string AcceptanceReceiptId { get; }

    public CaptureProcessingAcceptanceEvidence(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        Guid attemptId,
        Guid missionId,
        string processingJobId,
        string acceptanceReceiptId)
    {
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(orderId, nameof(orderId));
        DomainGuard.NotEmpty(attemptId, nameof(attemptId));
        DomainGuard.NotEmpty(missionId, nameof(missionId));

        ProcessingJobId = PolicyValidationGuard.NormalizeRequired(processingJobId, 200, nameof(processingJobId));
        AcceptanceReceiptId = PolicyValidationGuard.NormalizeRequired(acceptanceReceiptId, 200, nameof(acceptanceReceiptId));

        TenantId = tenantId;
        FarmId = farmId;
        OrderId = orderId;
        AttemptId = attemptId;
        MissionId = missionId;
    }
}
