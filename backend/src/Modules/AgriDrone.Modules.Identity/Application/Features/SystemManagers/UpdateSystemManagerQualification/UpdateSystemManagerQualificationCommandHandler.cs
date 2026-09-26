using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class UpdateSystemManagerQualificationCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSystemManagerQualificationCommand, Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        UpdateSystemManagerQualificationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Status == FlightQualificationStatus.Qualified &&
            (!request.ExpiresAt.HasValue ||
             request.ExpiresAt.Value <= timeProvider.GetUtcNow()))
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.InvalidQualificationExpiry());
        }

        return await SystemManagerProfileMutationExecutor.ExecuteAsync(
            repository,
            unitOfWork,
            auditWriter,
            auditLogSink,
            executionContext,
            timeProvider,
            request.ProfileId,
            request.Reason,
            request.ExpectedVersion,
            "UPDATE_QUALIFICATION",
            (profile, now) => profile.UpdateQualification(
                request.Status,
                request.ExpiresAt,
                now,
                request.ExpectedVersion),
            cancellationToken);
    }
}
