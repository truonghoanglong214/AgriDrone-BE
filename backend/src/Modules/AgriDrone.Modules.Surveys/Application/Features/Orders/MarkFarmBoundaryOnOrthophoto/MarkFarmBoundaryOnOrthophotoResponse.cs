using System.Text.Json.Serialization;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;

public sealed record MarkFarmBoundaryOnOrthophotoResponse(
    Guid SurveyOrderId,
    Guid FarmBoundaryId,
    int BoundaryVersionNumber,
    uint OrderVersion,
    DateTimeOffset MarkedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? MissionVersion = null);
