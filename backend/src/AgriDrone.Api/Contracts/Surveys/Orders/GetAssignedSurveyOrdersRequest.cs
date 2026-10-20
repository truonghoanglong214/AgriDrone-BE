namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record GetAssignedSurveyOrdersRequest
{
    public SurveyOrderStatusFilterValue? Status { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

#pragma warning disable CA1707
public enum SurveyOrderStatusFilterValue
{
    PENDING_BOUNDARY_VERIFICATION,
    AWAITING_BASELINE_APPOINTMENT,
    AWAITING_BASELINE_DEPOSIT,
    AWAITING_BOUNDARY_CAPTURE,
    BASELINE_READY,
    BASELINE_IN_PROGRESS,
    AWAITING_BASELINE_REVIEW,
    AWAITING_PRICING,
    AWAITING_PAID_APPOINTMENT,
    AWAITING_PAYMENT,
    READY_FOR_PAID_SERVICE,
    IN_PROGRESS,
    PENDING_REVIEW,
    COMPLETED,
    CANCELLED
}
#pragma warning restore CA1707
