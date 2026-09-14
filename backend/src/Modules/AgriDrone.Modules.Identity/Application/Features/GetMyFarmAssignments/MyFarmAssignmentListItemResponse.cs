using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;

public sealed record MyFarmAssignmentListItemResponse(
    Guid FarmMembershipId,
    AssignedFarmSummaryResponse Farm,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    IReadOnlyCollection<AssignedZoneSummaryResponse> Zones,
    GeneralStatus Status,
    long Version,
    DateTimeOffset JoinedAt);

public sealed record AssignedFarmSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    decimal? AreaHectares);

public sealed record AssignedZoneSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    decimal? AreaHectares);
