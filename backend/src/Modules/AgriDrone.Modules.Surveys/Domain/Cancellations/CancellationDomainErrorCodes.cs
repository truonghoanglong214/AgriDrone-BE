namespace AgriDrone.Modules.Surveys.Domain;

public static class CancellationDomainErrorCodes
{
    public const string ReceiptsLessThanRefundsAndReservations = "Cancellation.ReceiptsLessThanRefundsAndReservations";
    public const string RetentionRequiresAcceptableCapture = "Cancellation.RetentionRequiresAcceptableCapture";
    public const string RetentionExceedsAllowedCap = "Cancellation.RetentionExceedsAllowedCap";
    public const string InvalidRefundAmount = "Cancellation.InvalidRefundAmount";
    public const string RefundObligationAlreadyFinal = "Cancellation.RefundObligationAlreadyFinal";
}
