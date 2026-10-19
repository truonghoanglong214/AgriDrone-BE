using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;

public interface IExtraFlightRequestRepository
{
    Task<ExtraFlightRequest?> GetAsync(
        Guid surveyOrderId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    void Add(ExtraFlightRequest request);
}
