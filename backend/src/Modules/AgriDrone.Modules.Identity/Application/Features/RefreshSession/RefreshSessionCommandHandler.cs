using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using DomainRefreshSession = AgriDrone.Modules.Identity.Domain.RefreshSessions.RefreshSession;

namespace AgriDrone.Modules.Identity.Application.Features.RefreshSession;

internal sealed class RefreshSessionCommandHandler(
    IRefreshTokenService refreshTokenService,
    IRefreshSessionRepository refreshSessionRepository,
    IIdentityUnitOfWork unitOfWork,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<RefreshSessionOptions> refreshOptions,
    TimeProvider timeProvider)
    : IRequestHandler<RefreshSessionCommand, Result<AuthenticationCommandResult>>
{
    public async Task<Result<AuthenticationCommandResult>> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!refreshTokenService.TryHash(request.RawRefreshToken, out var tokenHash))
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        var observed = await refreshSessionRepository.FindObservedByTokenHashAsync(
            tokenHash,
            cancellationToken);
        if (observed is null)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => RotateWithinTransactionAsync(observed, token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.RefreshConflict());
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.UniqueViolation or
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.RefreshConflict());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.RefreshConflict());
        }
    }

    private async Task<Result<AuthenticationCommandResult>> RotateWithinTransactionAsync(
        ObservedRefreshTokenLookup observed,
        CancellationToken cancellationToken)
    {
        var locked = await refreshSessionRepository.LockForRefreshAsync(observed, cancellationToken);
        if (locked is null)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        var now = timeProvider.GetUtcNow();
        var session = locked.Session;
        var token = locked.Token;
        var user = locked.User;

        if (session.IsRevoked || session.IsExpired(now))
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        var userValid = user.DeletedAt is null &&
            user.Status == UserStatus.Active &&
            user.AuthenticationVersion == session.AuthenticationVersion;

        var scopeValid = session.TenantId.HasValue
            ? locked.Tenant is { DeletedAt: null, Status: GeneralStatus.Active } &&
              locked.Membership is { Status: GeneralStatus.Active } &&
              locked.Membership.UserId == user.Id &&
              locked.Membership.TenantId == locked.Tenant.Id
            : locked.SystemRoles.Contains(SystemRoles.SystemAdmin) ||
              locked.SystemRoles.Contains(SystemRoles.SystemManager);

        if (!userValid || !scopeValid)
        {
            if (session.Revoke(DomainRefreshSession.RevokeReasonAuthorizationInvalid, now))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        if (token.IsUsed)
        {
            if (observed.ObservedUsedAt.HasValue)
            {
                // Pre-lock read already observed this token as used -> replay / token reuse.
                if (session.Revoke(DomainRefreshSession.RevokeReasonTokenReuse, now))
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return Result.Failure<AuthenticationCommandResult>(
                    AuthenticationError.InvalidRefreshSession());
            }

            // Concurrent rotation completed between pre-lock read and row lock while session stayed valid.
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.RefreshConflict());
        }

        if (token.IsExpired(now))
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        var slidingExpiresAt = now.AddDays(refreshOptions.Value.IdleLifetimeDays);
        var nextExpiresAt = slidingExpiresAt < session.AbsoluteExpiresAt
            ? slidingExpiresAt
            : session.AbsoluteExpiresAt;
        if (nextExpiresAt <= now)
        {
            return Result.Failure<AuthenticationCommandResult>(
                AuthenticationError.InvalidRefreshSession());
        }

        // Flush used_at first inside the same transaction to satisfy uq_refresh_session_tokens_one_unused_per_session
        token.MarkUsed(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var nextGenerated = refreshTokenService.Generate();
        var nextToken = RefreshSessionToken.CreateRotated(
            token,
            Guid.CreateVersion7(),
            nextGenerated.TokenHash,
            now,
            nextExpiresAt);

        session.RecordRotation(now);
        refreshSessionRepository.AddRotatedToken(nextToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var authSession = session.TenantId.HasValue
            ? AuthenticationSessionFactory.Create(
                jwtTokenGenerator,
                user,
                locked.Membership!,
                locked.Tenant!,
                locked.SystemRoles)
            : AuthenticationSessionFactory.CreateSystemSession(
                jwtTokenGenerator,
                user,
                locked.SystemRoles);

        return Result.Success(new AuthenticationCommandResult(
            new LoginUserResponse(
                user.Email,
                user.FullName,
                user.Phone,
                authSession,
                null),
            new IssuedRefreshCookie(nextGenerated.RawToken, nextExpiresAt)));
    }
}
