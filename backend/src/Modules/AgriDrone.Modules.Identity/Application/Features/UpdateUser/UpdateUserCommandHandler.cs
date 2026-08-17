using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUser
{
    internal sealed class UpdateUserCommandHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        IIdentityUnitOfWork unitOfWork) : IRequestHandler<UpdateUserCommand, Result<UpdateUserResponse>>
    {
        public async Task<Result<UpdateUserResponse>>  Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            if (currentUser.UserId is not Guid currentUserId)
                return Result.Failure<UpdateUserResponse>(UserError.CurrentUserIsRequired());

            var user = await userRepository.GetByIdAsync(currentUserId, cancellationToken);
            if (user is null)
                return Result.Failure<UpdateUserResponse>(UserError.NotFound("User", currentUserId));

            var now = DateTimeOffset.UtcNow;
            user.UpdateProfile(request.fullName, request.phone, now);
            userRepository.Update(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new UpdateUserResponse(
                    user.FullName,
                    user.Phone,
                    now));
        }
    }
}
