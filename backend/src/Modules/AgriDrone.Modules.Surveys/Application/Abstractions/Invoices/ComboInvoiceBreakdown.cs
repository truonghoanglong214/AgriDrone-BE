namespace AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;

public sealed record ComboInvoiceBreakdown(
    int MappingPoleCount,
    decimal MappingPricePerPole,
    decimal MappingSubtotal,
    int PlantHealthPoleCount,
    decimal PlantHealthPricePerPole,
    decimal PlantHealthSubtotal);
