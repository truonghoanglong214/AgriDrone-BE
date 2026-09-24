using System.Security.Cryptography;
using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed partial class InviteSystemManagerCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISystemManagerProfileRepository profileRepository,
    IPasswordService passwordService,
    IPasswordResetTokenService tokenService,
    IPasswordResetTokenRepository tokenRepository,
    ISystemManagerInvitationEmailDelivery emailDelivery,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<InviteSystemManagerCommandHandler> logger)
    : IRequestHandler<
        InviteSystemManagerCommand,
        Result<InviteSystemManagerResponse>>
{
    private const string UserEmailUniqueConstraint = "uq_users_email";

    private readonly PasswordResetOptions _options = passwordResetOptions.Value;

    public async Task<Result<InviteSystemManagerResponse>> Handle(
        InviteSystemManagerCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<InviteSystemManagerResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var email = request.Email.Trim().ToLowerInvariant();
        Result<InvitationCreation> creationResult;

        try
        {
            creationResult = await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => CreateInvitationAsync(
                    actorId,
                    email,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(UserEmailUniqueConstraint))
        {
            return Result.Failure<InviteSystemManagerResponse>(
                UserError.EmailAlreadyExists(email));
        }

        if (creationResult.IsFailure)
        {
            return Result.Failure<InviteSystemManagerResponse>(
                creationResult.Error);
        }

        var creation = creationResult.Value;
        var emailSent = true;

        try
        {
            await emailDelivery.DeliverAsync(
                creation.Email,
                creation.PlainTextToken,
                creation.Response.ExpiresAt,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            emailSent = false;
            LogInvitationEmailFailure(
                logger,
                creation.Response.UserId,
                creation.Email,
                exception);
        }

        return Result.Success(
            creation.Response with { EmailSent = emailSent });
    }

    private async Task<Result<InvitationCreation>> CreateInvitationAsync(
        Guid actorId,
        string email,
        CancellationToken cancellationToken)
    {
        var existingUser = await userRepository.GetByEmailIncludingDeletedAsync(
            email,
            cancellationToken);

        if (existingUser is not null)
        {
            return Result.Failure<InvitationCreation>(
                UserError.EmailAlreadyExists(email));
        }

        var systemManagerRole = await roleRepository.GetByCodeAsync(
            SystemRoles.SystemManager,
            cancellationToken);

        if (systemManagerRole is null)
        {
            return Result.Failure<InvitationCreation>(
                SystemManagerError.RoleMissing());
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);
        var randomPassword = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(64));

        var user = User.Create(
            email,
            passwordService.HashPassword(randomPassword),
            email,
            phone: null,
            UserStatus.Active,
            now);

        user.AssignSystemRole(systemManagerRole.Id, now);

        var profile = SystemManagerProfile.Create(user.Id, now);
        var generatedToken = tokenService.Generate();
        var passwordToken = PasswordResetToken.Create(
            user.Id,
            generatedToken.TokenHash,
            expiresAt,
            now);

        userRepository.Add(user);
        profileRepository.Add(profile);
        tokenRepository.Add(passwordToken);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            UserId = user.Id,
            user.Email,
            ProfileId = profile.Id,
            Role = SystemRoles.SystemManager,
            ProfileStatus = profile.Status.ToString(),
            Availability = profile.Availability.ToString(),
            QualificationStatus = profile.QualificationStatus.ToString(),
            InvitationExpiresAt = expiresAt
        });

        auditWriter.AddSystemAdminAction(
            auditLogSink,
            actorId,
            executionContext.CorrelationId,
            "SystemManagerProfile",
            profile.Id,
            "INVITE",
            oldData: null,
            newData,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new InviteSystemManagerResponse(
            user.Id,
            profile.Id,
            user.Email,
            expiresAt,
            EmailSent: false);

        return Result.Success(
            new InvitationCreation(
                response,
                user.Email,
                generatedToken.PlainTextToken));
    }

    private sealed record InvitationCreation(
        InviteSystemManagerResponse Response,
        string Email,
        string PlainTextToken);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "SystemManager account {UserId} was created, but the invitation email to {Email} failed.")]
    private static partial void LogInvitationEmailFailure(
        ILogger logger,
        Guid userId,
        string email,
        Exception exception);
}
