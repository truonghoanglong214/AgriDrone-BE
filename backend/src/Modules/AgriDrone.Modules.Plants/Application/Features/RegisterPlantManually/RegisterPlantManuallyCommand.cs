using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;

public sealed record RegisterPlantManuallyCommand(
    Guid FarmId,
    Guid ZoneId,
    string PlantCode,
    Point Location,
    Guid? MapVersionId,
    int? RowIndex,
    int? ColumnIndex,
    decimal? LocationAccuracyM,
    string Reason) : IRequest<Result<RegisterPlantManuallyResponse>>;
