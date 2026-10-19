using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;

public sealed record VerifyFarmBoundaryCommand(
    Guid FarmId,
    Polygon Boundary,
    string Reason,
    string? EvidenceReference,
    long ExpectedFarmVersion,
    int? ExpectedCurrentBoundaryVersionNumber)
    : IRequest<Result<VerifyFarmBoundaryResponse>>;
