using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.UpdateUser;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using DomainRefreshSession = AgriDrone.Modules.Identity.Domain.RefreshSessions.RefreshSession;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword;

internal sealed class UpdateUserPasswordCommandHandler(
    IExecutionContext executionContext,
    IRefreshSessionRepository refreshSessionRepository,
    IIdentityUnitOfWork unitOfWork,
    IPasswordService passwordService,
    TimeProvider? timeProvider = null) : IRequestHandler<UpdateUserPasswordCommand, Result<UpdateUserPasswordResponse>>
{
    public async Task<Result<UpdateUserPasswordResponse>> Handle(
        UpdateUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid currentUserId)
            return Result.Failure<UpdateUserPasswordResponse>(AuthenticationError.CurrentUserRequired());

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var user = await refreshSessionRepository.LockUserForUpdateAsync(currentUserId, token);
            if (user is null || user.Status != UserStatus.Active || user.DeletedAt is not null)
                return Result.Failure<UpdateUserPasswordResponse>(UserError.NotFound());

            if (!passwordService.VerifyPassword(request.oldPassword, user.PasswordHash))
                return Result.Failure<UpdateUserPasswordResponse>(PasswordError.Incorrect());

            var newPassword = passwordService.HashPassword(request.newPassword);
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

            user.ChangePassword(newPassword, now);
            await refreshSessionRepository.RevokeAllActiveByUserIdAsync(
                user.Id,
                DomainRefreshSession.RevokeReasonPasswordChanged,
                now,
                token);
            await unitOfWork.SaveChangesAsync(token);

            return Result.Success(new UpdateUserPasswordResponse(user.Email, "Update Success"));
        }, cancellationToken);
    }
}
