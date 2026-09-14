using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries;

internal sealed record MyFarmAssignmentReadModel(
    Guid FarmMembershipId,
    Guid FarmId,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    IReadOnlyCollection<Guid> AssignedZoneIds,
    GeneralStatus Status,
    long Version,
    DateTimeOffset JoinedAt);
