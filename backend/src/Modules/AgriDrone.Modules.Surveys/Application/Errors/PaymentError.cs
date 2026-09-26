using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class PaymentError
{
    public const string NotFoundCode = "Payment.NotFound";
    public const string InvalidTransitionCode = PaymentDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = PaymentDomainErrorCodes.VersionConflict;
    public const string DuplicateEventCode = "Payment.DuplicateEvent";
    public const string InvalidEvidenceCode = "Payment.InvalidEvidence";
}
