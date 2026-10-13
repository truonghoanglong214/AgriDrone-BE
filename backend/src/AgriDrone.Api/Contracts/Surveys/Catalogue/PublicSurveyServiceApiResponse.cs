using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record PublicSurveyServiceApiResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    PublicSurveyServiceTypeValue ServiceType,
    bool IsExperimental,
    PublicIndicativePriceApiResponse IndicativePrice);

public sealed record PublicIndicativePriceApiResponse(
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

[JsonConverter(typeof(JsonStringEnumConverter<PublicSurveyServiceTypeValue>))]
public enum PublicSurveyServiceTypeValue
{
    [JsonStringEnumMemberName("PLANT_HEALTH")]
    PlantHealth,

    [JsonStringEnumMemberName("HARVEST_READINESS")]
    HarvestReadiness
}
