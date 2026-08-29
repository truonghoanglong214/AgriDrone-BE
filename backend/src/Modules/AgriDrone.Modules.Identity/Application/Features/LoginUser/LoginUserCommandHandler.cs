using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser
{
    internal sealed class LoginUserCommandHandler(
        IUserRepository userRepository,
        ITenantMembershipRepository tenantMembershipRepository,
        IPasswordService passwordService,
        IJwtTokenGenerator jwtTokenGenerator,
        ITenantSelectionTokenService tenantSelectionTokenService) : IRequestHandler<LoginUserCommand, Result<LoginUserResponse>>
    {
        public async Task<Result<LoginUserResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(request.email, cancellationToken);
            if (user is null || user.Status != UserStatus.Active)
                return Result.Failure<LoginUserResponse>(AuthenticationError.InvalidCredentials());

            if (!passwordService.VerifyPassword(request.password, user.PasswordHash))
                return Result.Failure<LoginUserResponse>(AuthenticationError.InvalidCredentials());

            var systemRoles = await userRepository.GetSystemRoleCodesAsync(
                user.Id,
                cancellationToken);

            if (systemRoles.Contains(SystemRoles.SystemAdmin))
            {
                var systemSession = AuthenticationSessionFactory.CreateSystemSession(
                    jwtTokenGenerator,
                    user,
                    systemRoles);

                return Result.Success(
                    new LoginUserResponse(
                        user.Email,
                        user.FullName,
                        user.Phone,
                        systemSession,
                        null));
            }

            var memberships = await tenantMembershipRepository.GetActiveByUserIdAsync(
                user.Id,
                cancellationToken);
            if (memberships.Count == 0)
                return Result.Failure<LoginUserResponse>(TenantMembershipError.UserNotInAnyTenant(user.Email));

            if (memberships.Count == 1)
            {
                var session = AuthenticationSessionFactory.Create(
                    jwtTokenGenerator,
                    user,
                    memberships.Single(),
                    systemRoles);

                return Result.Success(
                    new LoginUserResponse(
                        user.Email,
                        user.FullName,
                        user.Phone,
                        session,
                        null));
            }

            var selectionToken = tenantSelectionTokenService.Generate(user.Id);
            var tenants = memberships
                .Select(AuthenticationSessionFactory.ToTenantOption)
                .ToArray();

            return Result.Success(
                new LoginUserResponse(
                    user.Email,
                    user.FullName,
                    user.Phone,
                    null,
                    new TenantSelectionResponse(
                        selectionToken.Token,
                        selectionToken.ExpiresAt,
                        tenants)));
        }
    }
}
