using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;

internal sealed class GetSurveyOrderBoundaryScopeQueryHandler(
    ISurveyOrderBoundaryQueries queries,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetSurveyOrderBoundaryScopeQuery,
        Result<GetSurveyOrderBoundaryScopeResponse>>
{
    public async Task<Result<GetSurveyOrderBoundaryScopeResponse>> Handle(
        GetSurveyOrderBoundaryScopeQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await queries.GetAsync(
            query.SurveyOrderId,
            cancellationToken);
        if (snapshot is null)
        {
            return Result.Failure<GetSurveyOrderBoundaryScopeResponse>(
                SurveyOrderError.NotFound(query.SurveyOrderId));
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            snapshot.FarmId,
            cancellationToken);
        if (!access.IsAllowed ||
            access.TenantId != snapshot.TenantId ||
            access.FarmId != snapshot.FarmId)
        {
            return Result.Failure<GetSurveyOrderBoundaryScopeResponse>(
                SurveyOrderError.Forbidden());
        }

        return Result.Success(new GetSurveyOrderBoundaryScopeResponse(
            snapshot.SurveyOrderId,
            snapshot.OrderNumber,
            snapshot.TenantId,
            snapshot.FarmId,
            snapshot.OrderStatus,
            snapshot.OrderVersion,
            snapshot.RequiresBaselineMapping,
            snapshot.CurrentApprovedBoundary is null
                ? null
                : new SurveyOrderBoundaryResponse(
                    snapshot.CurrentApprovedBoundary.FarmBoundaryId,
                    snapshot.CurrentApprovedBoundary.VersionNumber,
                    snapshot.CurrentApprovedBoundary.Version,
                    snapshot.CurrentApprovedBoundary.Geometry,
                    snapshot.CurrentApprovedBoundary.Source,
                    snapshot.CurrentApprovedBoundary.EvidenceReference,
                    snapshot.CurrentApprovedBoundary.ReviewedBy,
                    snapshot.CurrentApprovedBoundary.ReviewedAt),
            snapshot.Zones.Select(zone =>
                    new SurveyOrderZoneScopeResponse(
                        zone.ZoneId,
                        zone.Code,
                        zone.Name,
                        zone.Boundary,
                        zone.Status,
                        zone.Version))
                .ToArray()));
    }
}
