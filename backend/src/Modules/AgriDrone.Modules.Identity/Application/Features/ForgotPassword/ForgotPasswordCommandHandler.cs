using System.Text.Encodings.Web;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.ForgotPassword;

internal sealed partial class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetTokenService passwordResetTokenService,
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IIdentityUnitOfWork unitOfWork,
    ILogger<ForgotPasswordCommandHandler> logger)
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
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return CreateGenericSuccess();
        }

        var now = DateTimeOffset.UtcNow;
        var generatedToken = passwordResetTokenService.Generate();
        var expiresAt = now.AddMinutes(_passwordResetOptions.ExpirationMinutes);

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
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

                return passwordResetToken.Id;
            },
            cancellationToken);

        try
        {
            await SendPasswordResetEmailAsync(
                user,
                generatedToken.PlainTextToken,
                expiresAt,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPasswordResetEmailFailure(logger, user.Id, exception);
        }

        return CreateGenericSuccess();
    }

    private static Result<ForgotPasswordResponse> CreateGenericSuccess() =>
        Result.Success(new ForgotPasswordResponse(GenericMessage));

    private async Task SendPasswordResetEmailAsync(
        User user,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var resetUrl = BuildResetUrl(plainTextToken);
        var encodedFullName = HtmlEncoder.Default.Encode(user.FullName);
        var encodedResetUrl = HtmlEncoder.Default.Encode(resetUrl);

        var message = new EmailMessage(
            To: [new EmailRecipient(user.Email, user.FullName)],
            Subject: "Reset your AgriDrone password",
            HtmlBody: $"""
                <h2>Reset your password</h2>
                <p>Hello {encodedFullName},</p>
                <p>We received a request to reset your AgriDrone password.</p>
                <p><a href="{encodedResetUrl}">Reset password</a></p>
                <p>This link expires at {expiresAt:O}. If you did not request it, you can ignore this email.</p>
                """,
            TextBody:
                $"Hello {user.FullName},{Environment.NewLine}" +
                $"Reset your AgriDrone password: {resetUrl}{Environment.NewLine}" +
                $"This link expires at {expiresAt:O}. If you did not request it, you can ignore this email.");

        await emailSender.SendAsync(message, cancellationToken);
    }

    private string BuildResetUrl(string plainTextToken)
    {
        var separator = _passwordResetOptions.ResetUrl.Contains('?') ? '&' : '?';

        return $"{_passwordResetOptions.ResetUrl}{separator}token={Uri.EscapeDataString(plainTextToken)}";
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to send a password reset email for user {UserId}.")]
    private static partial void LogPasswordResetEmailFailure(
        ILogger logger,
        Guid userId,
        Exception exception);
}
