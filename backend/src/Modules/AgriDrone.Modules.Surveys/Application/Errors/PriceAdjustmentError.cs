using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class PriceAdjustmentError
{
    public const string InvalidTransitionCode =
        PriceAdjustmentDomainErrorCodes.InvalidTransition;
    public const string PendingAlreadyExistsCode =
        "PriceAdjustment.PendingAlreadyExists";
}
