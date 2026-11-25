using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser;

internal sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IPasswordService passwordService,
    IJwtTokenGenerator jwtTokenGenerator,
    ITenantSelectionTokenService tenantSelectionTokenService,
    IRefreshSessionRepository? refreshSessionRepository = null,
    IRefreshTokenService? refreshTokenService = null,
    IIdentityUnitOfWork? unitOfWork = null,
    IOptions<RefreshSessionOptions>? refreshOptions = null,
    TimeProvider? timeProvider = null)
    : IRequestHandler<LoginUserCommand, Result<AuthenticationCommandResult>>
{
    public async Task<Result<AuthenticationCommandResult>> Handle(
        LoginUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.email, cancellationToken);
        if (user is null || user.Status != UserStatus.Active || user.DeletedAt is not null)
            return Result.Failure<AuthenticationCommandResult>(AuthenticationError.InvalidCredentials());

        if (!passwordService.VerifyPassword(request.password, user.PasswordHash))
            return Result.Failure<AuthenticationCommandResult>(AuthenticationError.InvalidCredentials());

        var observedAuthVersion = user.AuthenticationVersion;
        var systemRoles = await userRepository.GetSystemRoleCodesAsync(
            user.Id,
            cancellationToken);

        if (systemRoles.Contains(SystemRoles.SystemAdmin) ||
            systemRoles.Contains(SystemRoles.SystemManager))
        {
            if (!request.issueRefreshSession)
            {
                var systemSession = AuthenticationSessionFactory.CreateSystemSession(
                    jwtTokenGenerator,
                    user,
                    systemRoles);

                return Result.Success(new AuthenticationCommandResult(
                    new LoginUserResponse(
                        user.Email,
                        user.FullName,
                        user.Phone,
                        systemSession,
                        null)));
            }

            return await IssueSystemSessionWithRefreshAsync(
                user.Id,
                observedAuthVersion,
                cancellationToken);
        }

        var memberships = await tenantMembershipRepository.GetActiveByUserIdAsync(
            user.Id,
            cancellationToken);
        if (memberships.Count == 0)
            return Result.Failure<AuthenticationCommandResult>(TenantMembershipError.UserNotInAnyTenant(user.Email));

        if (memberships.Count == 1)
        {
            var singleMembership = memberships.Single();
            if (!request.issueRefreshSession)
            {
                var session = AuthenticationSessionFactory.Create(
                    jwtTokenGenerator,
                    user,
                    singleMembership,
                    systemRoles);

                return Result.Success(new AuthenticationCommandResult(
                    new LoginUserResponse(
                        user.Email,
                        user.FullName,
                        user.Phone,
                        session,
                        null)));
            }

            return await IssueTenantSessionWithRefreshAsync(
                user.Id,
                singleMembership.TenantId,
                singleMembership.Id,
                observedAuthVersion,
                cancellationToken);
        }

        var selectionToken = tenantSelectionTokenService.Generate(user.Id, observedAuthVersion);
        var tenants = memberships
            .Select(AuthenticationSessionFactory.ToTenantOption)
            .ToArray();

        return Result.Success(new AuthenticationCommandResult(
            new LoginUserResponse(
                user.Email,
                user.FullName,
                user.Phone,
                null,
                new TenantSelectionResponse(
                    selectionToken.Token,
                    selectionToken.ExpiresAt,
                    tenants))));
    }

    private async Task<Result<AuthenticationCommandResult>> IssueSystemSessionWithRefreshAsync(
        Guid userId,
        int observedAuthVersion,
        CancellationToken cancellationToken)
    {
        EnsureRefreshDependenciesAvailable();

        return await unitOfWork!.ExecuteInTransactionAsync(async token =>
        {
            var locked = await refreshSessionRepository!.LockForSessionCreationAsync(
                userId,
                tenantId: null,
                membershipId: null,
                token);
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

            if (locked is null ||
                locked.User.DeletedAt is not null ||
                locked.User.Status != UserStatus.Active ||
                locked.User.AuthenticationVersion != observedAuthVersion ||
                (!locked.SystemRoles.Contains(SystemRoles.SystemAdmin) &&
                 !locked.SystemRoles.Contains(SystemRoles.SystemManager)))
            {
                return Result.Failure<AuthenticationCommandResult>(AuthenticationError.InvalidCredentials());
            }

            var cookie = AuthenticationSessionFactory.IssueInitialRefreshSession(
                refreshSessionRepository,
                refreshTokenService!,
                refreshOptions!.Value,
                locked.User,
                tenant: null,
                membership: null,
                now);

            await unitOfWork.SaveChangesAsync(token);

            var session = AuthenticationSessionFactory.CreateSystemSession(
                jwtTokenGenerator,
                locked.User,
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

    private async Task<Result<AuthenticationCommandResult>> IssueTenantSessionWithRefreshAsync(
        Guid userId,
        Guid tenantId,
        Guid membershipId,
        int observedAuthVersion,
        CancellationToken cancellationToken)
    {
        EnsureRefreshDependenciesAvailable();

        return await unitOfWork!.ExecuteInTransactionAsync(async token =>
        {
            var locked = await refreshSessionRepository!.LockForSessionCreationAsync(
                userId,
                tenantId,
                membershipId,
                token);
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

            if (locked is null ||
                locked.User.DeletedAt is not null ||
                locked.User.Status != UserStatus.Active ||
                locked.User.AuthenticationVersion != observedAuthVersion)
            {
                return Result.Failure<AuthenticationCommandResult>(AuthenticationError.InvalidCredentials());
            }

            if (locked.Tenant is null ||
                locked.Tenant.DeletedAt is not null ||
                locked.Tenant.Status != GeneralStatus.Active ||
                locked.Membership is null ||
                locked.Membership.UserId != locked.User.Id ||
                locked.Membership.TenantId != locked.Tenant.Id ||
                locked.Membership.Status != GeneralStatus.Active)
            {
                return Result.Failure<AuthenticationCommandResult>(TenantMembershipError.UserNotInAnyTenant(locked.User.Email));
            }

            var cookie = AuthenticationSessionFactory.IssueInitialRefreshSession(
                refreshSessionRepository,
                refreshTokenService!,
                refreshOptions!.Value,
                locked.User,
                locked.Tenant,
                locked.Membership,
                now);

            await unitOfWork.SaveChangesAsync(token);

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

    private void EnsureRefreshDependenciesAvailable()
    {
        if (refreshSessionRepository is null ||
            refreshTokenService is null ||
            unitOfWork is null ||
            refreshOptions is null)
        {
            throw new InvalidOperationException("Refresh session persistence services are required when issuing a refresh session.");
        }
    }
}
