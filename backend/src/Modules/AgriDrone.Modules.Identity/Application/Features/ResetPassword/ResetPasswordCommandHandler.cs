using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;
using DomainRefreshSession = AgriDrone.Modules.Identity.Domain.RefreshSessions.RefreshSession;

namespace AgriDrone.Modules.Identity.Application.Features.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenService passwordResetTokenService,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IRefreshSessionRepository refreshSessionRepository,
    IPasswordService passwordService,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null)
    : IRequestHandler<ResetPasswordCommand, Result<ResetPasswordResponse>>
{
    public Task<Result<ResetPasswordResponse>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedToken = request.Token.Trim();
        var tokenHash = passwordResetTokenService.Hash(normalizedToken);

        return unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => ResetAsync(
                request.NewPassword,
                tokenHash,
                transactionCancellationToken),
            cancellationToken);
    }

    private async Task<Result<ResetPasswordResponse>> ResetAsync(
        string newPassword,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var resetToken = await passwordResetTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);
        if (resetToken is null)
        {
            return Result.Failure<ResetPasswordResponse>(
                PasswordResetError.InvalidOrExpiredToken());
        }

        // Lock user row before rotating credentials and revoking active refresh sessions
        var user = await refreshSessionRepository.LockUserForUpdateAsync(
            resetToken.UserId,
            cancellationToken);

        if (user is null || user.Status != UserStatus.Active || user.DeletedAt is not null)
        {
            return Result.Failure<ResetPasswordResponse>(
                PasswordResetError.InvalidOrExpiredToken());
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (!resetToken.CanBeUsed(now))
        {
            return Result.Failure<ResetPasswordResponse>(
                PasswordResetError.InvalidOrExpiredToken());
        }

        var tokenConsumed = await passwordResetTokenRepository.TryMarkUsedAsync(
            resetToken.Id,
            now,
            cancellationToken);

        if (!tokenConsumed)
        {
            return Result.Failure<ResetPasswordResponse>(
                PasswordResetError.InvalidOrExpiredToken());
        }

        var passwordHash = passwordService.HashPassword(newPassword);
        user.ChangePassword(passwordHash, now);
        await refreshSessionRepository.RevokeAllActiveByUserIdAsync(
            user.Id,
            DomainRefreshSession.RevokeReasonPasswordReset,
            now,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new ResetPasswordResponse("Password has been reset successfully."));
    }
}
