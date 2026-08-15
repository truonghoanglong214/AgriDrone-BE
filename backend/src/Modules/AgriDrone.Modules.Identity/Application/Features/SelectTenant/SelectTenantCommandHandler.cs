using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SelectTenant;

internal sealed class SelectTenantCommandHandler(
    ITenantSelectionTokenService tenantSelectionTokenService,
    ITenantMembershipRepository tenantMembershipRepository,
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator)
    : IRequestHandler<SelectTenantCommand, Result<LoginUserResponse>>
{
    public async Task<Result<LoginUserResponse>> Handle(
        SelectTenantCommand request,
        CancellationToken cancellationToken)
    {
        var userId = tenantSelectionTokenService.Validate(
            request.SelectionToken);
        if (userId is not Guid id)
        {
            return Result.Failure<LoginUserResponse>(
                UserError.InvalidTenantSelectionToken());
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<LoginUserResponse>(
                UserError.InvalidTenantSelectionToken());
        }

        var membership = await tenantMembershipRepository
            .GetActiveByUserAndTenantIdAsync(
                user.Id,
                request.TenantId,
                cancellationToken);
        if (membership is null)
        {
            return Result.Failure<LoginUserResponse>(
                UserError.TenantAccessDenied());
        }

        var systemRoles = await userRepository.GetSystemRoleCodesAsync(
            user.Id,
            cancellationToken);
        var session = AuthenticationSessionFactory.Create(
            jwtTokenGenerator,
            user,
            membership,
            systemRoles);

        return Result.Success(
            new LoginUserResponse(
                user.Email,
                user.FullName,
                user.Phone,
                session,
                null));
    }
}
