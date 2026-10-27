using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ValidateFarmBaseMapDraft;

internal sealed class ValidateFarmBaseMapDraftQueryHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    IBaselineMappingBatchValidator validator)
    : IRequestHandler<ValidateFarmBaseMapDraftQuery, Result<BaselineMappingBatchValidationResult>>
{
    public async Task<Result<BaselineMappingBatchValidationResult>> Handle(
        ValidateFarmBaseMapDraftQuery query,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid)
        {
            return Result.Failure<BaselineMappingBatchValidationResult>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            query.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<BaselineMappingBatchValidationResult>(FarmError.NotFound());
        }

        var batch = await dbContext.MappingCandidateBatches
            .AsNoTracking()
            .Include(b => b.Candidates)
            .SingleOrDefaultAsync(
                b => b.FarmBaseMapVersionId == query.DraftId && b.FarmId == query.FarmId,
                cancellationToken);

        if (batch is null)
        {
            return Result.Failure<BaselineMappingBatchValidationResult>(
                AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
        }

        var boundary = await dbContext.FarmBoundaries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                b => b.Id == batch.FarmBoundaryVersionId,
                cancellationToken);

        var validationResult = validator.Validate(batch, boundary);

        return Result.Success(validationResult);
    }
}
