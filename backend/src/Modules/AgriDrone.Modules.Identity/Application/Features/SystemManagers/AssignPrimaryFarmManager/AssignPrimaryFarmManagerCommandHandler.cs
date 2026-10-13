using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class AssignPrimaryFarmManagerCommandHandler(
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    ISystemManagerProfileRepository profileRepository,
    IFarmManagerAssignmentRepository assignmentRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<AssignPrimaryFarmManagerCommand, Result<FarmManagerAssignmentResponse>>
{
    public async Task<Result<FarmManagerAssignmentResponse>> Handle(
        AssignPrimaryFarmManagerCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<FarmManagerAssignmentResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var farm = await farmReferenceQuery.GetActiveFarmAsync(
            request.FarmId,
            cancellationToken);
        if (farm is null)
        {
            return Result.Failure<FarmManagerAssignmentResponse>(
                SystemManagerError.FarmNotFound());
        }

        var profile = await profileRepository.GetByIdAsync(
            request.SystemManagerProfileId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (profile is null ||
            profile.User.Status != UserStatus.Active ||
            !profile.CanBeAssigned(now))
        {
            return Result.Failure<FarmManagerAssignmentResponse>(
                SystemManagerError.NotAssignable());
        }

        var current = await assignmentRepository.GetActiveByFarmIdAsync(
            request.FarmId,
            cancellationToken);
        if (current is not null)
        {
            if (!request.ExpectedCurrentAssignmentVersion.HasValue ||
                current.Version != request.ExpectedCurrentAssignmentVersion.Value)
            {
                return Result.Failure<FarmManagerAssignmentResponse>(
                    SystemManagerError.ConcurrentUpdate());
            }

            if (current.SystemManagerProfileId == profile.Id)
            {
                return Result.Success(ToResponse(current, profile.UserId));
            }
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    JsonDocument? oldData = null;
                    try
                    {
                        if (current is not null)
                        {
                            oldData = Snapshot(current);
                            current.End(
                                actorId,
                                $"Reassigned: {request.Reason}",
                                now,
                                request.ExpectedCurrentAssignmentVersion!.Value);
                            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                        }

                        var assignment = FarmManagerAssignment.Create(
                            farm.TenantId,
                            farm.FarmId,
                            profile.Id,
                            actorId,
                            request.Reason,
                            now);
                        assignmentRepository.Add(assignment);

                        using var newData = JsonSerializer.SerializeToDocument(new
                        {
                            assignment.TenantId,
                            assignment.FarmId,
                            assignment.SystemManagerProfileId,
                            profile.UserId,
                            assignment.AssignmentReason,
                            assignment.AssignedAt
                        });
                        auditWriter.AddSystemAdminAction(
                            auditLogSink,
                            actorId,
                            executionContext.CorrelationId,
                            "FarmManagerAssignment",
                            assignment.Id,
                            current is null ? "ASSIGN" : "REASSIGN",
                            oldData,
                            newData,
                            now);

                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                        return Result.Success(ToResponse(assignment, profile.UserId));
                    }
                    finally
                    {
                        oldData?.Dispose();
                    }
                },
                cancellationToken);
        }
        catch (ActiveFarmManagerAssignmentConflictException)
        {
            return Result.Failure<FarmManagerAssignmentResponse>(
                SystemManagerError.ActiveAssignmentConflict());
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<FarmManagerAssignmentResponse>(
                SystemManagerError.ConcurrentUpdate());
        }
    }

    private static JsonDocument Snapshot(FarmManagerAssignment assignment) =>
        JsonSerializer.SerializeToDocument(new
        {
            assignment.Id,
            assignment.TenantId,
            assignment.FarmId,
            assignment.SystemManagerProfileId,
            assignment.AssignmentReason,
            assignment.AssignedAt,
            assignment.Version
        });

    private static FarmManagerAssignmentResponse ToResponse(
        FarmManagerAssignment assignment,
        Guid userId) =>
        new(
            assignment.Id,
            assignment.TenantId,
            assignment.FarmId,
            assignment.SystemManagerProfileId,
            userId,
            assignment.AssignedAt,
            assignment.Version);
}
