using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint;

public sealed record ClassifyAndRecordPointCommand(
    Guid FarmId,
    Point Point,
    BoundaryExceptionSource Source,
    string SourceReferenceId,
    Guid? SurveyOrderId = null,
    Guid? MissionId = null,
    double NearBoundaryThresholdMeters = 5.0);

public sealed record ClassifyAndRecordPointResponse(
    PointBoundaryClassification Classification,
    double DistanceMeters,
    Guid? BoundaryExceptionId,
    string? State);
