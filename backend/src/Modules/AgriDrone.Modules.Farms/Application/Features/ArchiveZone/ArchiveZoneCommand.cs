using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveZone;

public sealed record ArchiveZoneCommand(
    Guid FarmId,
    Guid ZoneId,
    long ExpectedVersion) : IRequest<Result>;
