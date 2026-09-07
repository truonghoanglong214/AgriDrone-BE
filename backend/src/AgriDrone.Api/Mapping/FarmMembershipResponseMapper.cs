using AgriDrone.Api.Contracts.FarmMemberships;
using AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using DomainFarmAccessScope = AgriDrone.Modules.Identity.Domain.FarmMemberships.FarmAccessScope;
using DomainFarmMemberRole = AgriDrone.Modules.Identity.Domain.FarmMemberships.FarmMemberRole;

namespace AgriDrone.Api.Mapping;

internal static class FarmMembershipResponseMapper
{
    public static AssignFarmMemberApiResponse ToResponse(
        AssignFarmMemberResponse assignment) =>
        new(
            assignment.FarmMembershipId,
            assignment.TenantId,
            assignment.FarmId,
            assignment.UserId,
            assignment.Role switch
            {
                DomainFarmMemberRole.Manager =>
                    AssignFarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker =>
                    AssignFarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    AssignFarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    AssignFarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            [],
            assignment.Status.ToString().ToUpperInvariant(),
            assignment.Version,
            assignment.JoinedAt);

    public static AssignFarmMemberApiResponse ToResponse(
        GetFarmMemberAssignmentResponse assignment) =>
        new(
            assignment.FarmMembershipId,
            assignment.TenantId,
            assignment.FarmId,
            assignment.UserId,
            assignment.Role switch
            {
                DomainFarmMemberRole.Manager =>
                    AssignFarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker =>
                    AssignFarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    AssignFarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    AssignFarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.ZoneIds,
            assignment.Status.ToString().ToUpperInvariant(),
            assignment.Version,
            assignment.JoinedAt);
}
