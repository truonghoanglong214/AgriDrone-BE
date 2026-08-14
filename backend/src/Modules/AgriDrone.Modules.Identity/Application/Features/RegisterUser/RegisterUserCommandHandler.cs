using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser
{
    internal sealed class RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITenantRepository tenantRepository,
        ITenantMembershipRepository tenantMembershipRepository,
        IIdentityUnitOfWork unitOfWork) : IRequestHandler<RegisterUserCommand, Result<RegisterUserResponse>>
    {
        public async Task<Result<RegisterUserResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            var existingUser = await userRepository.GetByEmailAsync(request.email, cancellationToken);
            if(existingUser is not null) 
                return Result.Failure<RegisterUserResponse>(UserError.EmailAlreadyExists(request.email));

            var existingTenant = await tenantRepository.GetByCodeAsync(request.tenantCode, cancellationToken);
            if(existingTenant is not null)
                return Result.Failure<RegisterUserResponse>(UserError.TenantAlreadyExist(request.tenantCode));
            
            string passwordHash = passwordService.HashPassword(request.password);
            var now = DateTimeOffset.UtcNow;

            var newUser = User.Create(request.email, passwordHash, request.fullName, request.phone, UserStatus.Active, now);

            var newTenant = Tenant.Create(request.tenantCode, request.tenantName, GeneralStatus.Active, now);

            var newTenantMembership = TenantMembership.Create(newTenant.Id, newUser.Id, TenantMemberRole.Owner, GeneralStatus.Active, now, now);

            userRepository.Add(newUser);
            tenantRepository.Add(newTenant);
            tenantMembershipRepository.Add(newTenantMembership);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new RegisterUserResponse(
                    newUser.Id,
                    newUser.Email,
                    newUser.FullName,
                    newUser.Phone,
                    newTenant.Code,
                    newTenant.Name,
                    newUser.CreatedAt));
        }
    }
}
