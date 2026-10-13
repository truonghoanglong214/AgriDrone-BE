using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class CreateSystemManagerProfileCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISystemManagerProfileRepository profileRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSystemManagerProfileCommand, Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        CreateSystemManagerProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                UserError.NotFound(request.UserId));
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.UserMustBeActive());
        }

        if (await profileRepository.GetByUserIdAsync(user.Id, cancellationToken) is not null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ProfileAlreadyExists());
        }

        var role = await roleRepository.GetByCodeAsync(
            SystemRoles.SystemManager,
            cancellationToken);
        if (role is null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.RoleMissing());
        }

        var now = timeProvider.GetUtcNow();
        var profile = SystemManagerProfile.Create(user.Id, now);
        user.AssignSystemRole(role.Id, now);
        profileRepository.Add(profile);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            profile.UserId,
            Status = profile.Status.ToString(),
            Availability = profile.Availability.ToString(),
            QualificationStatus = profile.QualificationStatus.ToString()
        });
        auditWriter.AddSystemAdminAction(
            auditLogSink,
            actorId,
            executionContext.CorrelationId,
            "SystemManagerProfile",
            profile.Id,
            "CREATE",
            null,
            newData,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(SystemManagerProfileResponseMapper.ToResponse(profile, user));
    }
}
