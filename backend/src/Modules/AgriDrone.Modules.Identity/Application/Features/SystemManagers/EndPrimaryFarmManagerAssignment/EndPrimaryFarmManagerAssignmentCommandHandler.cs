using System.Text.Json;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class EndPrimaryFarmManagerAssignmentCommandHandler(
    IFarmManagerAssignmentRepository assignmentRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IActiveCaptureAssignmentGuard? activeCaptureGuard = null)
    : IRequestHandler<EndPrimaryFarmManagerAssignmentCommand, Result>
{
    public async Task<Result> Handle(
        EndPrimaryFarmManagerAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(AuthenticationError.CurrentUserRequired());
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            token => HandleInTransactionAsync(request, actorId, token),
            cancellationToken);
    }

    private async Task<Result> HandleInTransactionAsync(
        EndPrimaryFarmManagerAssignmentCommand request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetActiveByFarmIdForUpdateAsync(
            request.FarmId,
            cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(SystemManagerError.AssignmentNotFound());
        }

        if (assignment.Version != request.ExpectedVersion)
        {
            return Result.Failure(SystemManagerError.ConcurrentUpdate());
        }

        if (activeCaptureGuard is not null &&
            await activeCaptureGuard.HasInFlightCaptureAsync(
                assignment.TenantId,
                assignment.FarmId,
                cancellationToken))
        {
            return Result.Failure(AppError.Conflict(
                "ACTIVE_CAPTURE_IN_FLIGHT",
                "Cannot end assignment while a drone capture is in flight."));
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            assignment.SystemManagerProfileId,
            assignment.AssignedAt,
            assignment.Version
        });
        assignment.End(actorId, request.Reason, now, request.ExpectedVersion);
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            assignment.EndedBy,
            assignment.EndReason,
            assignment.EndedAt,
            assignment.Version
        });
        auditWriter.AddSystemAdminAction(
            auditLogSink,
            actorId,
            executionContext.CorrelationId,
            "FarmManagerAssignment",
            assignment.Id,
            "END",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(SystemManagerError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
