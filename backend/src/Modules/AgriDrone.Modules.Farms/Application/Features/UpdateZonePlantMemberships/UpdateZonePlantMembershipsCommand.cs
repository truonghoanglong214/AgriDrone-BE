using AgriDrone.Modules.Farms.Application.Abstractions.Zones;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateZonePlantMemberships;

public sealed record UpdateZonePlantMembershipsCommand(
    Guid FarmId,
    Guid ZoneId,
    long ExpectedZoneVersion,
    string Reason,
    IReadOnlyList<ZonePlantMembershipOperation> Operations)
    : IRequest<Result<UpdateZonePlantMembershipsResponse>>;
