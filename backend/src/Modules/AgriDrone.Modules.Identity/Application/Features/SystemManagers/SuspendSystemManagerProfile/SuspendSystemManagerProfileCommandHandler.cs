using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class SuspendSystemManagerProfileCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<SuspendSystemManagerProfileCommand, Result<SystemManagerProfileResponse>>
{
    public Task<Result<SystemManagerProfileResponse>> Handle(
        SuspendSystemManagerProfileCommand request,
        CancellationToken cancellationToken) =>
        SystemManagerProfileMutationExecutor.ExecuteAsync(
            repository,
            unitOfWork,
            auditWriter,
            auditLogSink,
            executionContext,
            timeProvider,
            request.ProfileId,
            request.Reason,
            request.ExpectedVersion,
            "SUSPEND",
            (profile, now) => profile.Suspend(now, request.ExpectedVersion),
            cancellationToken);
}
