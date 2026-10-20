using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint;

public sealed record ClassifyAndRecordPointRequest(
    ClassifyAndRecordPointCommand Command)
    : IRequest<Result<ClassifyAndRecordPointResponse>>;

internal sealed class ClassifyAndRecordPointHandler(
    IFarmRepository farmRepository,
    IFarmBoundaryRepository boundaryRepository,
    IBoundaryExceptionRepository exceptionRepository,
    IFarmBoundaryPolicy boundaryPolicy,
    IFarmUnitOfWork unitOfWork,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<ClassifyAndRecordPointRequest, Result<ClassifyAndRecordPointResponse>>
{
    public async Task<Result<ClassifyAndRecordPointResponse>> Handle(
        ClassifyAndRecordPointRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Command;
        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
        {
            return Result.Failure<ClassifyAndRecordPointResponse>(FarmError.NotFound());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            command.FarmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure<ClassifyAndRecordPointResponse>(FarmError.NotFound());
        }

        var currentBoundary = await boundaryRepository.GetCurrentApprovedAsync(
            tenantId,
            command.FarmId,
            cancellationToken);

        if (currentBoundary is null)
        {
            return Result.Failure<ClassifyAndRecordPointResponse>(FarmError.InvalidBoundary());
        }

        var classification = boundaryPolicy.ClassifyPoint(
            command.Point,
            currentBoundary.Geometry,
            command.NearBoundaryThresholdMeters);

        var distance = boundaryPolicy.CalculateDistanceToBoundaryMeters(
            command.Point,
            currentBoundary.Geometry);

        if (classification == PointBoundaryClassification.WithinBoundary)
        {
            return Result.Success(new ClassifyAndRecordPointResponse(
                classification,
                distance,
                null,
                null));
        }

        var existingUnresolved = await exceptionRepository.GetUnresolvedBySourceAsync(
            currentBoundary.Id,
            command.Source,
            command.SourceReferenceId,
            cancellationToken);

        if (existingUnresolved is not null)
        {
            return Result.Success(new ClassifyAndRecordPointResponse(
                classification,
                distance,
                existingUnresolved.Id,
                existingUnresolved.State.ToString()));
        }

        var initialState = classification == PointBoundaryClassification.OutOfBoundary
            ? BoundaryExceptionState.OutOfBoundary
            : BoundaryExceptionState.NeedsReview;

        var now = timeProvider.GetUtcNow();

        var exception = BoundaryException.Create(
            tenantId,
            command.FarmId,
            currentBoundary.Id,
            command.Source,
            command.SourceReferenceId,
            command.SurveyOrderId,
            command.MissionId,
            command.Point,
            initialState,
            (decimal)distance,
            (decimal)command.NearBoundaryThresholdMeters,
            "1.0",
            now);

        exceptionRepository.Add(exception);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ClassifyAndRecordPointResponse(
            classification,
            distance,
            exception.Id,
            initialState.ToString()));
    }
}
