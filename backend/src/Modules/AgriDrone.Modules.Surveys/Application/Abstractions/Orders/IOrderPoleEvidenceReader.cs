using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public sealed record OrderPoleEvidence(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    Guid? MissionId,
    Guid? CaptureAttemptId,
    Guid FarmBaseMapVersionId,
    Guid FarmBoundaryVersionId,
    Guid? MappingCandidateBatchId,
    Guid SpatialPolicyVersionId,
    int BillablePoleCount);

public interface IOrderPoleEvidenceReader
{
    Task<Result<OrderPoleEvidence>> ReadEvidenceForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
