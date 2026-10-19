using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;

internal sealed class GetAssignedFarmOperationalContextQueryHandler(
    IFarmOperationalContextQueries queries,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext)
    : IRequestHandler<GetAssignedFarmOperationalContextQuery, Result<FarmOperationalContextResponse>>
{
    public async Task<Result<FarmOperationalContextResponse>> Handle(
        GetAssignedFarmOperationalContextQuery query,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid)
        {
            return Result.Failure<FarmOperationalContextResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            query.FarmId,
            cancellationToken);

        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
        {
            return Result.Failure<FarmOperationalContextResponse>(FarmError.AccessDenied());
        }

        var dto = await queries.GetAssignedFarmOperationalContextAsync(
            tenantId,
            query.FarmId,
            cancellationToken);

        if (dto is null)
        {
            return Result.Failure<FarmOperationalContextResponse>(FarmError.NotFound());
        }

        return Result.Success(new FarmOperationalContextResponse(
            dto.FarmId,
            dto.TenantId,
            dto.Code,
            dto.Name,
            dto.Address,
            dto.Status,
            dto.AreaHectares,
            dto.Version,
            dto.ApprovedBoundary is null ? null : new ApprovedBoundarySummaryResponse(
                dto.ApprovedBoundary.Id,
                dto.ApprovedBoundary.VersionNumber,
                dto.ApprovedBoundary.Geometry,
                dto.ApprovedBoundary.Source,
                dto.ApprovedBoundary.EvidenceReference,
                dto.ApprovedBoundary.ReviewedBy,
                dto.ApprovedBoundary.ReviewedAt,
                dto.ApprovedBoundary.ReviewReason,
                dto.ApprovedBoundary.Version),
            dto.Zones.Select(z => new OperationalZoneSummaryResponse(
                z.Id,
                z.Code,
                z.Name,
                z.Status,
                z.AreaHectares,
                z.Boundary,
                z.Version)).ToArray(),
            dto.PublishedBaseMap is null ? null : new PublishedBaseMapSummaryResponse(
                dto.PublishedBaseMap.Id,
                dto.PublishedBaseMap.VersionNumber,
                dto.PublishedBaseMap.SourceSurveyOrderId,
                dto.PublishedBaseMap.PublishedAt,
                dto.PublishedBaseMap.PublishedBy,
                dto.PublishedBaseMap.Version)));
    }
}
