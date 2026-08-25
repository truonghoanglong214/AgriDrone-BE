using System.Security.Cryptography;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.BootstrapSystemAdmin;

internal sealed partial class BootstrapSystemAdminCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISystemAdminBootstrapLock bootstrapLock,
    IPasswordService passwordService,
    IPasswordResetTokenService passwordResetTokenService,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetEmailDelivery passwordResetEmailDelivery,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IIdentityUnitOfWork unitOfWork,
    ILogger<BootstrapSystemAdminCommandHandler> logger)
    : IRequestHandler<
        BootstrapSystemAdminCommand,
        Result<BootstrapSystemAdminResponse>>
{
    private readonly PasswordResetOptions _passwordResetOptions =
        passwordResetOptions.Value;

    public async Task<Result<BootstrapSystemAdminResponse>> Handle(
        BootstrapSystemAdminCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();

        var transactionResult = await unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => CreateInTransactionAsync(
                email,
                fullName,
                transactionCancellationToken),
            cancellationToken);

        if (transactionResult.IsFailure)
        {
            return Result.Failure<BootstrapSystemAdminResponse>(
                transactionResult.Error);
        }

        var creation = transactionResult.Value;
        if (!creation.Response.Created)
        {
            return Result.Success(creation.Response);
        }

        try
        {
            await passwordResetEmailDelivery.DeliverAsync(
                creation.User!.Email,
                creation.User.FullName,
                creation.PlainTextToken!,
                creation.ExpiresAt!.Value,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPasswordSetupEmailFailure(
                logger,
                creation.User!.Id,
                creation.User.Email,
                exception);
        }

        return Result.Success(creation.Response);
    }

    private async Task<Result<BootstrapCreation>> CreateInTransactionAsync(
        string email,
        string fullName,
        CancellationToken cancellationToken)
    {
        await bootstrapLock.AcquireAsync(cancellationToken);

        var hasSystemAdmin = await roleRepository.HasAssignedActiveUserAsync(
            SystemRoles.SystemAdmin,
            cancellationToken);
        if (hasSystemAdmin)
        {
            return Result.Success(
                BootstrapCreation.AlreadyConfigured(email));
        }

        var systemAdminRole = await roleRepository.GetByCodeAsync(
            SystemRoles.SystemAdmin,
            cancellationToken);
        if (systemAdminRole is null)
        {
            return Result.Failure<BootstrapCreation>(
                BootstrapSystemAdminError.SystemRoleMissing());
        }

        var existingUser = await userRepository.GetByEmailIncludingDeletedAsync(
            email,
            cancellationToken);
        if (existingUser is not null)
        {
            return Result.Failure<BootstrapCreation>(
                BootstrapSystemAdminError.ConfiguredEmailAlreadyExists(email));
        }

        var now = DateTimeOffset.UtcNow;
        var randomPassword = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(64));
        var passwordHash = passwordService.HashPassword(randomPassword);
        var user = User.Create(
            email,
            passwordHash,
            fullName,
            phone: null,
            UserStatus.Active,
            now);

        user.AssignSystemRole(systemAdminRole.Id, now);

        var generatedToken = passwordResetTokenService.Generate();
        var expiresAt = now.AddMinutes(
            _passwordResetOptions.ExpirationMinutes);
        var resetToken = PasswordResetToken.Create(
            user.Id,
            generatedToken.TokenHash,
            expiresAt,
            now);

        userRepository.Add(user);
        passwordResetTokenRepository.Add(resetToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            BootstrapCreation.Created(
                user,
                generatedToken.PlainTextToken,
                expiresAt));
    }

    private sealed record BootstrapCreation(
        BootstrapSystemAdminResponse Response,
        User? User,
        string? PlainTextToken,
        DateTimeOffset? ExpiresAt)
    {
        public static BootstrapCreation AlreadyConfigured(string email) =>
            new(
                new BootstrapSystemAdminResponse(false, null, email),
                null,
                null,
                null);

        public static BootstrapCreation Created(
            User user,
            string plainTextToken,
            DateTimeOffset expiresAt) =>
            new(
                new BootstrapSystemAdminResponse(true, user.Id, user.Email),
                user,
                plainTextToken,
                expiresAt);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Critical,
        Message = "The System Admin account {UserId} was created, but its password setup email could not be sent. Fix email delivery and use the forgot-password flow for {Email}.")]
    private static partial void LogPasswordSetupEmailFailure(
        ILogger logger,
        Guid userId,
        string email,
        Exception exception);
}
