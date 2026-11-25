using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.SharedKernel.Application;
using MediatR;
using DomainRefreshSession = AgriDrone.Modules.Identity.Domain.RefreshSessions.RefreshSession;

namespace AgriDrone.Modules.Identity.Application.Features.LogoutSession;

internal sealed class LogoutSessionCommandHandler(
    IRefreshTokenService refreshTokenService,
    IRefreshSessionRepository refreshSessionRepository,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<LogoutSessionCommand, Result>
{
    public async Task<Result> Handle(
        LogoutSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!refreshTokenService.TryHash(request.RawRefreshToken, out var tokenHash))
        {
            return Result.Success();
        }

        var observed = await refreshSessionRepository.FindObservedByTokenHashAsync(
            tokenHash,
            cancellationToken);
        if (observed is null)
        {
            return Result.Success();
        }

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var session = await refreshSessionRepository.LockSessionForLogoutAsync(
                observed,
                token);
            if (session is not null &&
                session.Revoke(DomainRefreshSession.RevokeReasonLogout, timeProvider.GetUtcNow()))
            {
                await unitOfWork.SaveChangesAsync(token);
            }

            return Result.Success();
        }, cancellationToken);

        return Result.Success();
    }
}
