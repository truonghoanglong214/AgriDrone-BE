using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record CreatePlantConditionRequest(
    string Code,
    string Name,
    string? ScientificName,
    PlantConditionTypeValue ConditionType,
    string? Description,
    PlantHealthLabelKindValue? PlantHealthLabelKind = null,
    string? DatasetVersion = null,
    string? AnnotationRuleVersion = null,
    string? TeamLabelLimitation = null);

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

public enum PlantHealthLabelKindValue
{
    [JsonStringEnumMemberName("SUSPECTED_DISEASE")]
    SuspectedDisease,

    [JsonStringEnumMemberName("CONFUSING_NON_DISEASE")]
    ConfusingNonDisease
}
