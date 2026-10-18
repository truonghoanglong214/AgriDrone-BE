using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class ScopeAdjustmentError
{
    public static AppError NotFound(Guid adjustmentId) => AppError.NotFound(
        "ScopeAdjustment.NotFound",
        $"Scope adjustment '{adjustmentId}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        "ScopeAdjustment.Forbidden",
        "The actor is not authorized to manage scope adjustments for this order.");

    public static AppError Conflict(string message) => AppError.Conflict(
        "ScopeAdjustment.Conflict",
        message);

    public static AppError OpenAdjustmentExists() => AppError.Conflict(
        "ScopeAdjustment.OpenAdjustmentExists",
        "A pending or approved scope adjustment already exists for this order.");

    public static AppError OrderNotFound(Guid orderId) => AppError.NotFound(
        "ScopeAdjustment.OrderNotFound",
        $"Survey order '{orderId}' was not found.");
}

public static class AdjustmentSettlementError
{
    public static AppError NotFound(Guid settlementId) => AppError.NotFound(
        "AdjustmentSettlement.NotFound",
        $"Adjustment settlement '{settlementId}' was not found.");

    public static AppError Conflict(string message) => AppError.Conflict(
        "AdjustmentSettlement.Conflict",
        message);

    public static AppError IdempotencyConflict(string key) => AppError.Conflict(
        "AdjustmentSettlement.IdempotencyConflict",
        $"A settlement with idempotency key '{key}' already exists with different parameters.");
}

public static class ExtraFlightRequestError
{
    public static AppError NotFound(Guid requestId) => AppError.NotFound(
        "ExtraFlightRequest.NotFound",
        $"Extra flight request '{requestId}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        "ExtraFlightRequest.Forbidden",
        "The actor is not authorized to manage extra flight requests for this order.");

    public static AppError Conflict(string message) => AppError.Conflict(
        "ExtraFlightRequest.Conflict",
        message);

    public static AppError OrderNotFound(Guid orderId) => AppError.NotFound(
        "ExtraFlightRequest.OrderNotFound",
        $"Survey order '{orderId}' was not found.");
}
