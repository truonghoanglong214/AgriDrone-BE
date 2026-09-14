using AgriDrone.Api.Contracts.FarmMemberships;
using AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;
using AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;
using AgriDrone.SharedKernel.Application.Pagination;
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
                    FarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker =>
                    FarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    FarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    FarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.ZoneIds,
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
                    FarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker =>
                    FarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    FarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    FarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.ZoneIds,
            assignment.Status.ToString().ToUpperInvariant(),
            assignment.Version,
            assignment.JoinedAt);

    public static PagedResult<FarmMemberListItemApiResponse> ToResponse(
        PagedResult<FarmMemberListItemResponse> members) =>
        new(
            members.Items.Select(ToResponse).ToArray(),
            members.PageNumber,
            members.PageSize,
            members.TotalCount);

    public static PagedResult<MyFarmAssignmentListItemApiResponse> ToResponse(
        PagedResult<MyFarmAssignmentListItemResponse> assignments) =>
        new(
            assignments.Items.Select(ToResponse).ToArray(),
            assignments.PageNumber,
            assignments.PageSize,
            assignments.TotalCount);

    private static FarmMemberListItemApiResponse ToResponse(
        FarmMemberListItemResponse member) =>
        new(
            member.FarmMembershipId,
            member.FarmId,
            member.UserId,
            member.Email,
            member.FullName,
            member.TenantRole.ToString().ToUpperInvariant(),
            member.Role switch
            {
                DomainFarmMemberRole.Manager => FarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker => FarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(member))
            },
            member.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    FarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    FarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(member))
            },
            member.ZoneIds,
            member.Status.ToString().ToUpperInvariant(),
            member.Version,
            member.JoinedAt);

    private static MyFarmAssignmentListItemApiResponse ToResponse(
        MyFarmAssignmentListItemResponse assignment) =>
        new(
            assignment.FarmMembershipId,
            new AssignedFarmSummaryApiResponse(
                assignment.Farm.Id,
                assignment.Farm.Code,
                assignment.Farm.Name,
                assignment.Farm.Address,
                assignment.Farm.AreaHectares),
            assignment.Role switch
            {
                DomainFarmMemberRole.Manager => FarmMemberRoleValue.Manager,
                DomainFarmMemberRole.Worker => FarmMemberRoleValue.Worker,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.AccessScope switch
            {
                DomainFarmAccessScope.AllZones =>
                    FarmAccessScopeValue.AllZones,
                DomainFarmAccessScope.SelectedZones =>
                    FarmAccessScopeValue.SelectedZones,
                _ => throw new ArgumentOutOfRangeException(nameof(assignment))
            },
            assignment.Zones
                .Select(zone => new AssignedZoneSummaryApiResponse(
                    zone.Id,
                    zone.Code,
                    zone.Name,
                    zone.AreaHectares))
                .ToArray(),
            assignment.Status.ToString().ToUpperInvariant(),
            assignment.Version,
            assignment.JoinedAt);
}
