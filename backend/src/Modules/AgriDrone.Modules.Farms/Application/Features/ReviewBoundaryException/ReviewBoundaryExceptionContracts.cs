using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.ReviewBoundaryException;

public sealed record ReviewBoundaryExceptionCommand(
    Guid FarmId,
    Guid ExceptionId,
    BoundaryExceptionDecision Decision,
    Point? CorrectedPosition,
    string Reason,
    JsonDocument Evidence,
    uint ExpectedVersion);

public sealed record ReviewBoundaryExceptionResponse(
    Guid ExceptionId,
    string State,
    string Decision,
    uint Version);
