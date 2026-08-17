using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenService passwordResetTokenService,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IUserRepository userRepository,
    IPasswordService passwordService,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<ResetPasswordCommand, Result<ResetPasswordResponse>>
{
    public Task<Result<ResetPasswordResponse>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = passwordResetTokenService.Hash(request.Token);

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
        var now = DateTimeOffset.UtcNow;

        if (resetToken is null || !resetToken.CanBeUsed(now))
        {
            return Result.Failure<ResetPasswordResponse>(
                PasswordResetError.InvalidOrExpiredToken());
        }

        var user = await userRepository.GetByIdAsync(
            resetToken.UserId,
            cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new ResetPasswordResponse("Password has been reset successfully."));
    }
}
