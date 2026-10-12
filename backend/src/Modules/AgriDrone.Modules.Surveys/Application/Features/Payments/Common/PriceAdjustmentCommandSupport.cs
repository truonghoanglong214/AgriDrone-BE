using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.Common;

internal static class PriceAdjustmentCommandSupport
{
    public static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code switch
        {
            PriceAdjustmentDomainErrorCodes.VersionConflict =>
                PriceAdjustmentError.VersionConflict(),
            PriceAdjustmentDomainErrorCodes.InvalidTransition =>
                PriceAdjustmentError.InvalidTransition(exception.Message),
            PriceAdjustmentDomainErrorCodes.NoChange =>
                PriceAdjustmentError.NoChange(),
            SurveyOrderDomainErrorCodes.VersionConflict =>
                SurveyOrderError.VersionConflict(),
            SurveyOrderDomainErrorCodes.InvalidTransition =>
                SurveyOrderError.InvalidTransition(exception.Message),
            _ => AppError.Conflict(exception.Code, exception.Message)
        };

    public static string MapStatus(PriceAdjustmentStatus status) => status switch
    {
        PriceAdjustmentStatus.Pending => "PENDING",
        PriceAdjustmentStatus.Approved => "APPROVED",
        PriceAdjustmentStatus.Rejected => "REJECTED",
        PriceAdjustmentStatus.Applied => "APPLIED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static SurveyPriceAdjustmentChangedV1 CreateEvent(
        SurveyOrder order,
        PriceAdjustment adjustment,
        string changeType,
        DateTimeOffset changedAt) => new(
            adjustment.Id,
            order.Id,
            order.TenantId,
            order.FarmId,
            MapStatus(adjustment.Status),
            adjustment.OldPoleCount,
            adjustment.NewPoleCount,
            adjustment.OldPricePerPole,
            adjustment.NewPricePerPole,
            adjustment.OldFinalPrice,
            adjustment.NewFinalPrice,
            adjustment.Currency,
            changeType,
            changedAt);
}
