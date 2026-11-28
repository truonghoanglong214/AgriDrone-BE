using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record ComboInvoicePricing(
    int PlantHealthPoleCount,
    decimal MappingPricePerPole,
    decimal PlantHealthPricePerPole,
    Polygon? PlantHealthSurveyArea = null);
