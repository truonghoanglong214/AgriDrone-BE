using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.SelectTenant;

internal sealed class SelectTenantCommandHandler(
    ITenantSelectionTokenService tenantSelectionTokenService,
    ITenantMembershipRepository tenantMembershipRepository,
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshSessionRepository refreshSessionRepository,
    IRefreshTokenService refreshTokenService,
    IIdentityUnitOfWork unitOfWork,
    IOptions<RefreshSessionOptions> refreshOptions,
    TimeProvider timeProvider)
    : IRequestHandler<SelectTenantCommand, Result<AuthenticationCommandResult>>
{
    public async Task<Result<AuthenticationCommandResult>> Handle(
        SelectTenantCommand request,
        CancellationToken cancellationToken)
    {
        var validated = tenantSelectionTokenService.Validate(request.SelectionToken);
        if (validated is null)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidTenantSelectionToken());
        }

        var candidateMembership = await tenantMembershipRepository.GetActiveByUserAndTenantIdAsync(
            validated.UserId,
            request.TenantId,
            cancellationToken);
        if (candidateMembership is null)
        {
            var candidateUser = await userRepository.GetByIdAsync(validated.UserId, cancellationToken);
            if (candidateUser is null ||
                candidateUser.DeletedAt is not null ||
                candidateUser.Status != UserStatus.Active ||
                candidateUser.AuthenticationVersion != validated.AuthenticationVersion ||
                timeProvider.GetUtcNow() >= validated.ExpiresAt)
            {
                return Result.Failure<AuthenticationCommandResult>(
                    AuthenticationError.InvalidTenantSelectionToken());
            }

            return Result.Failure<AuthenticationCommandResult>(
                TenantError.AccessDenied());
        }

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var locked = await refreshSessionRepository.LockForSessionCreationAsync(
                validated.UserId,
                request.TenantId,
                candidateMembership.Id,
                token);
            var now = timeProvider.GetUtcNow();

            if (locked is null ||
                locked.User.DeletedAt is not null ||
                locked.User.Status != UserStatus.Active ||
                locked.User.AuthenticationVersion != validated.AuthenticationVersion ||
                now >= validated.ExpiresAt)
            {
                return Result.Failure<AuthenticationCommandResult>(
                    AuthenticationError.InvalidTenantSelectionToken());
            }

            if (locked.Tenant is null ||
                locked.Tenant.DeletedAt is not null ||
                locked.Tenant.Status != GeneralStatus.Active ||
                locked.Membership is null ||
                locked.Membership.UserId != locked.User.Id ||
                locked.Membership.TenantId != locked.Tenant.Id ||
                locked.Membership.Status != GeneralStatus.Active)
            {
                return Result.Failure<AuthenticationCommandResult>(
                    TenantError.AccessDenied());
            }

            IssuedRefreshCookie? cookie = null;
            if (request.IssueRefreshSession)
            {
                cookie = AuthenticationSessionFactory.IssueInitialRefreshSession(
                    refreshSessionRepository,
                    refreshTokenService,
                    refreshOptions.Value,
                    locked.User,
                    locked.Tenant,
                    locked.Membership,
                    now);
                await unitOfWork.SaveChangesAsync(token);
            }

            var session = AuthenticationSessionFactory.Create(
                jwtTokenGenerator,
                locked.User,
                locked.Membership,
                locked.Tenant,
                locked.SystemRoles);

            return Result.Success(new AuthenticationCommandResult(
                new LoginUserResponse(
                    locked.User.Email,
                    locked.User.FullName,
                    locked.User.Phone,
                    session,
                    null),
                cookie));
        }, cancellationToken);
    }
}
