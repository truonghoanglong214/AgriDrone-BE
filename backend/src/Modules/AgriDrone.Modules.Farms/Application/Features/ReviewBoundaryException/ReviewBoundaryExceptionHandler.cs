using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.ReviewBoundaryException;

public sealed record ReviewBoundaryExceptionRequest(
    ReviewBoundaryExceptionCommand Command)
    : IRequest<Result<ReviewBoundaryExceptionResponse>>;

internal sealed class ReviewBoundaryExceptionHandler(
    IBoundaryExceptionRepository exceptionRepository,
    IFarmUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewBoundaryExceptionRequest, Result<ReviewBoundaryExceptionResponse>>
{
    public async Task<Result<ReviewBoundaryExceptionResponse>> Handle(
        ReviewBoundaryExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var command = request.Command;
        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(FarmError.NotFound());
        }

        var exception = await exceptionRepository.GetByIdAsync(
            tenantId,
            command.ExceptionId,
            cancellationToken);

        if (exception is null || exception.FarmId != command.FarmId)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(FarmError.BoundaryExceptionNotFound());
        }

        if (exception.State == BoundaryExceptionState.Resolved)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(FarmError.BoundaryExceptionAlreadyResolved());
        }

        try
        {
            exception.EnsureVersion(command.ExpectedVersion);
        }
        catch (FarmBoundaryDomainException)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(FarmError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();

        try
        {
            exception.Resolve(
                command.Decision,
                command.CorrectedPosition,
                actorId,
                now,
                command.Reason,
                command.Evidence);
        }
        catch (FarmBoundaryDomainException ex)
        {
            return Result.Failure<ReviewBoundaryExceptionResponse>(
                AppError.Validation(ex.Code, ex.Message));
        }

        exceptionRepository.Update(exception);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            farmId = command.FarmId,
            decision = command.Decision.ToString(),
            reason = command.Reason,
            version = exception.Version
        });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: command.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(BoundaryException),
            entityId: exception.Id,
            action: "REVIEW_BOUNDARY_EXCEPTION",
            oldData: null,
            newData: auditData,
            createdAt: now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReviewBoundaryExceptionResponse(
            exception.Id,
            exception.State.ToString(),
            exception.Decision.ToString()!,
            exception.Version));
    }
}
