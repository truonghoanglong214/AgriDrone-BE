namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record GetSystemAdminSurveyOrdersRequest
{
    public SurveyOrderStatusFilterValue? Status { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? FarmId { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
