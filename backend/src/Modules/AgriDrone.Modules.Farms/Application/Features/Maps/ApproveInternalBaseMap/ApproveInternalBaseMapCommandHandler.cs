using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ApproveInternalBaseMap;

internal sealed class ApproveInternalBaseMapCommandHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveInternalBaseMapCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ApproveInternalBaseMapCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<Guid>(
                AppError.Unauthorized("Authentication.ActorRequired", "Current user is required."));
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<Guid>(
                AppError.Forbidden("Farm.AccessDenied", "User does not have management access to this farm."));
        }

        var mapVersion = await dbContext.FarmBaseMapVersions
            .SingleOrDefaultAsync(m => m.Id == command.BaseMapVersionId && m.FarmId == command.FarmId, cancellationToken);

        if (mapVersion is null)
        {
            return Result.Failure<Guid>(
                AppError.NotFound("FarmBaseMapVersion.NotFound", "Farm base map version not found."));
        }

        if (mapVersion.Status != FarmBaseMapStatus.Draft)
        {
            return Result.Failure<Guid>(
                AppError.Conflict("FarmBaseMapVersion.InvalidStatus", $"Only draft base maps can be approved internally; current status is '{mapVersion.Status}'."));
        }

        var now = timeProvider.GetUtcNow();

        // Check if there is an existing baseline map for this farm
        var existingBaselineMap = await dbContext.FarmBaseMapVersions
            .Where(m => m.FarmId == command.FarmId && m.IsBaseline && m.Id != mapVersion.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingBaselineMap is null)
        {
            // First Order establishes the baseline
            mapVersion.SetLineage(isBaseline: true, predecessorVersionId: null, baselineMapVersionId: mapVersion.Id);
        }
        else
        {
            // Follow-up order links to the previous version and the existing baseline
            var latestPublished = await dbContext.FarmBaseMapVersions
                .Where(m => m.FarmId == command.FarmId && m.Status == FarmBaseMapStatus.Published && m.Id != mapVersion.Id)
                .OrderByDescending(m => m.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            mapVersion.SetLineage(
                isBaseline: false,
                predecessorVersionId: latestPublished?.Id ?? existingBaselineMap.Id,
                baselineMapVersionId: existingBaselineMap.Id);
        }

        mapVersion.ApproveInternal(actorId, now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(mapVersion.Id);
    }
}
