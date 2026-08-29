using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Features.UpdateUser;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword
{
    internal sealed class UpdateUserPasswordCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IIdentityUnitOfWork unitOfWork,
        IPasswordService passwordService) : IRequestHandler<UpdateUserPasswordCommand, Result<UpdateUserPasswordResponse>>
    {
        public async Task<Result<UpdateUserPasswordResponse>> Handle(UpdateUserPasswordCommand request, CancellationToken cancellationToken)
        {
            if (currentUser.UserId is not Guid currentUserId)
                return Result.Failure<UpdateUserPasswordResponse>(AuthenticationError.CurrentUserRequired());

            var user = await userRepository.GetByIdAsync(currentUserId, cancellationToken);
            if (user is null)
                return Result.Failure<UpdateUserPasswordResponse>(UserError.NotFound());

            if (!passwordService.VerifyPassword(request.oldPassword, user.PasswordHash))
                return Result.Failure<UpdateUserPasswordResponse>(PasswordError.Incorrect());

            var newPassword = passwordService.HashPassword(request.newPassword);
            var now = DateTimeOffset.UtcNow;

            user.ChangePassword(newPassword, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdateUserPasswordResponse
                (user.Email, "Update Success"));
        }
    }
}
