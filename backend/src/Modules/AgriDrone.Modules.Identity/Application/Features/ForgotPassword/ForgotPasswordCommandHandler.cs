using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.ForgotPassword;

internal sealed partial class ForgotPasswordCommandHandler(
    IRefreshSessionRepository refreshSessionRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetTokenService passwordResetTokenService,
    IPasswordResetEmailDelivery passwordResetEmailDelivery,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IIdentityUnitOfWork unitOfWork,
    ILogger<ForgotPasswordCommandHandler> logger,
    TimeProvider timeProvider)
    : IRequestHandler<ForgotPasswordCommand, Result<ForgotPasswordResponse>>
{
    private const string GenericMessage =
        "If an account exists for this email, a password reset link has been sent.";

    private readonly PasswordResetOptions _passwordResetOptions =
        passwordResetOptions.Value;

    public async Task<Result<ForgotPasswordResponse>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var issued = await unitOfWork.ExecuteInTransactionAsync<(
            User Recipient, string PlainTextToken, DateTimeOffset ExpiresAt)?>(
            async transactionCancellationToken =>
            {
                // Follow the same lock order as reset: user before password reset tokens.
                var user = await refreshSessionRepository.LockUserByEmailForUpdateAsync(
                    email,
                    transactionCancellationToken);
                if (user is null || user.Status != UserStatus.Active)
                {
                    return null;
                }

                var now = timeProvider.GetUtcNow();
                var generatedToken = passwordResetTokenService.Generate();
                var expiresAt = now.AddMinutes(_passwordResetOptions.ExpirationMinutes);

                await passwordResetTokenRepository.RevokeActiveForUserAsync(
                    user.Id,
                    now,
                    transactionCancellationToken);

                var passwordResetToken = PasswordResetToken.Create(
                    user.Id,
                    generatedToken.TokenHash,
                    expiresAt,
                    now);

                passwordResetTokenRepository.Add(passwordResetToken);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                return (user, generatedToken.PlainTextToken, expiresAt);
            },
            cancellationToken);

        if (issued is not { } resetEmail)
        {
            return CreateGenericSuccess();
        }

        try
        {
            await passwordResetEmailDelivery.DeliverAsync(
                resetEmail.Recipient.Email,
                resetEmail.Recipient.FullName,
                resetEmail.PlainTextToken,
                resetEmail.ExpiresAt,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPasswordResetEmailFailure(logger, resetEmail.Recipient.Id, exception);
        }

        return CreateGenericSuccess();
    }

    private static Result<ForgotPasswordResponse> CreateGenericSuccess() =>
        Result.Success(new ForgotPasswordResponse(GenericMessage));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to send a password reset email for user {UserId}.")]
    private static partial void LogPasswordResetEmailFailure(
        ILogger logger,
        Guid userId,
        Exception exception);
}
