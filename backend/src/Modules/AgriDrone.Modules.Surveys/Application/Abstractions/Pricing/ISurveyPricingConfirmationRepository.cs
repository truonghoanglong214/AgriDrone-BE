using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;

public interface ISurveyPricingConfirmationRepository
{
    Task<Result<SurveyPricingConfirmationState>> GetForConfirmationAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyPricingConfirmationState(
    SurveyOrder Order,
    Guid SystemManagerProfileId,
    Guid CurrentFarmBaseMapVersionId,
    Guid BaseMapSourceSurveyOrderId,
    int AuthoritativeActivePoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency);
