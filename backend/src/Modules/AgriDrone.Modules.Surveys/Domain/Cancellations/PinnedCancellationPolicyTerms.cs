using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record PinnedCancellationPolicyTerms(
    Guid PolicyId,
    int VersionNumber,
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    int AllowedFreeNoShowReschedules,
    bool OperationalPostponementRefundable,
    bool ServiceFailureRefundable)
{
    public static PinnedCancellationPolicyTerms Create(
        Guid policyId,
        int versionNumber,
        bool refundableBeforeCapture,
        bool retainDepositAfterCapture,
        int allowedFreeNoShowReschedules,
        bool operationalPostponementRefundable,
        bool serviceFailureRefundable)
    {
        DomainGuard.NotEmpty(policyId, nameof(policyId));
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(allowedFreeNoShowReschedules);

        return new PinnedCancellationPolicyTerms(
            policyId,
            versionNumber,
            refundableBeforeCapture,
            retainDepositAfterCapture,
            allowedFreeNoShowReschedules,
            operationalPostponementRefundable,
            serviceFailureRefundable);
    }
}
