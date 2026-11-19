namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface IQualityRefundNoticeQuery
{
    Task<DateTimeOffset?> GetCreatedAtAsync(Guid tenantId, Guid orderId, Guid resultId, CancellationToken cancellationToken);
}
