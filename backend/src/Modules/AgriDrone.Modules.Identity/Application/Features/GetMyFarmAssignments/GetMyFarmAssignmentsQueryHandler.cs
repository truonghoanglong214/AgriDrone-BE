using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;

internal sealed class GetMyFarmAssignmentsQueryHandler(
    IFarmMembershipQueries farmMembershipQueries,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<
        GetMyFarmAssignmentsQuery,
        Result<PagedResult<MyFarmAssignmentListItemResponse>>>
{
    public async Task<Result<PagedResult<MyFarmAssignmentListItemResponse>>> Handle(
        GetMyFarmAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<PagedResult<MyFarmAssignmentListItemResponse>>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PagedResult<MyFarmAssignmentListItemResponse>>(
                AuthenticationError.CurrentUserRequired());
        }

        var hasAccess = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Member,
            cancellationToken);

        if (!hasAccess.IsAllowed)
        {
            return Result.Failure<PagedResult<MyFarmAssignmentListItemResponse>>(
                TenantError.AccessDenied());
        }

        var activeFarms = await farmReferenceQuery.GetActiveFarmsAsync(
            tenantId,
            cancellationToken);

        var activeFarmIds = activeFarms
            .Select(farm => farm.FarmId)
            .ToArray();

        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);

        var farmAssignments = await farmMembershipQueries.GetMyAssignmentsPageAsync(
            tenantId,
            actorId,
            activeFarmIds,
            request.Role,
            pageRequest,
            cancellationToken);

        var pageFarmIds = farmAssignments.Items
            .Select(assignment => assignment.FarmId)
            .Distinct()
            .ToArray();

        var activeZones = await farmReferenceQuery.GetActiveZonesAsync(
            tenantId,
            pageFarmIds,
            cancellationToken);

        var farmsById = activeFarms.ToDictionary(farm => farm.FarmId);
        var zonesByFarmId = activeZones
            .GroupBy(zone => zone.FarmId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var items = farmAssignments.Items
            .Select(assignment =>
            {
                var farm = farmsById[assignment.FarmId];
                var farmZones = zonesByFarmId.TryGetValue(
                    assignment.FarmId,
                    out var zones)
                    ? zones
                    : [];

                var assignedZoneIds = assignment.AssignedZoneIds.ToHashSet();
                var visibleZones = farmZones
                    .Where(zone =>
                        assignment.AccessScope == FarmAccessScope.AllZones ||
                        assignedZoneIds.Contains(zone.ZoneId))
                    .Select(zone => new AssignedZoneSummaryResponse(
                        zone.ZoneId,
                        zone.Code,
                        zone.Name,
                        zone.AreaHectares))
                    .ToArray();

                return new MyFarmAssignmentListItemResponse(
                    assignment.FarmMembershipId,
                    new AssignedFarmSummaryResponse(
                        farm.FarmId,
                        farm.Code,
                        farm.Name,
                        farm.Address,
                        farm.AreaHectares),
                    assignment.Role,
                    assignment.AccessScope,
                    visibleZones,
                    assignment.Status,
                    assignment.Version,
                    assignment.JoinedAt);
            })
            .ToArray();

        return Result.Success(new PagedResult<MyFarmAssignmentListItemResponse>(
            items,
            farmAssignments.PageNumber,
            farmAssignments.PageSize,
            farmAssignments.TotalCount));
    }
}
