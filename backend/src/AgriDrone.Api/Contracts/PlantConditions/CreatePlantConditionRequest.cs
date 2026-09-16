using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record CreatePlantConditionRequest(
    string Code,
    string Name,
    string? ScientificName,
    PlantConditionTypeValue ConditionType,
    string? Description);

[JsonConverter(typeof(JsonStringEnumConverter<PlantConditionTypeValue>))]
public enum PlantConditionTypeValue
{
    [JsonStringEnumMemberName("DISEASE")]
    Disease,

    [JsonStringEnumMemberName("ABIOTIC_DAMAGE")]
    AbioticDamage,

    [JsonStringEnumMemberName("MECHANICAL_DAMAGE")]
    MechanicalDamage,

    [JsonStringEnumMemberName("OTHER")]
    Other
}
