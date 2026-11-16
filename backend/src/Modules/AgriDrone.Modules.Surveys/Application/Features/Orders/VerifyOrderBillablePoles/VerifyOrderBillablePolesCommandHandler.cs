using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifyOrderBillablePoles;

internal sealed class VerifyOrderBillablePolesCommandHandler(
    SurveysDbContext dbContext,
    IOrderPoleEvidenceReader evidenceReader,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<VerifyOrderBillablePolesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        VerifyOrderBillablePolesCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<int>(
                AppError.Unauthorized("Authentication.ActorRequired", "Current user is required."));
        }

        var order = await dbContext.SurveyOrders
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<int>(
                AppError.NotFound("SurveyOrder.NotFound", "Survey order not found."));
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(order.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<int>(
                AppError.Forbidden("Access.Denied", "Current manager does not have access to this farm."));
        }

        var evidenceResult = await evidenceReader.ReadEvidenceForOrderAsync(order.Id, cancellationToken);
        if (evidenceResult.IsFailure)
        {
            return Result.Failure<int>(evidenceResult.Error);
        }

        var evidence = evidenceResult.Value;
        var now = timeProvider.GetUtcNow();

        var latestSnapshot = await dbContext.SurveyOrderPoleCountSnapshots
            .Where(s => s.SurveyOrderId == order.Id)
            .OrderByDescending(s => s.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextRevision = (latestSnapshot?.RevisionNumber ?? 0) + 1;

        var snapshot = SurveyOrderPoleCountSnapshot.Create(
            id: Guid.NewGuid(),
            surveyOrderId: order.Id,
            tenantId: order.TenantId,
            farmId: order.FarmId,
            missionId: evidence.MissionId,
            captureAttemptId: evidence.CaptureAttemptId,
            farmBaseMapVersionId: evidence.FarmBaseMapVersionId,
            farmBoundaryVersionId: evidence.FarmBoundaryVersionId,
            mappingCandidateBatchId: evidence.MappingCandidateBatchId,
            spatialPolicyVersionId: evidence.SpatialPolicyVersionId,
            verifiedBillablePoleCount: evidence.BillablePoleCount,
            verifiedBy: actorId,
            verifiedAt: now,
            revisionNumber: nextRevision,
            predecessorSnapshotId: latestSnapshot?.Id ?? order.VerifiedPoleCountSnapshotId);

        dbContext.SurveyOrderPoleCountSnapshots.Add(snapshot);
        order.RecordVerifiedPoleCount(snapshot, order.Version);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(snapshot.VerifiedBillablePoleCount);
    }
}
