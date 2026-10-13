using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
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
    ISystemManagerProfileRepository profileRepository,
    ISystemManagerInvitationRepository invitationRepository,
    IInvitationTokenService invitationTokenService,
    ISystemManagerInvitationEmailDelivery emailDelivery,
    IOptions<SystemManagerInvitationOptions> invitationOptions,
    IIdentityUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<InviteSystemManagerCommandHandler> logger)
    : IRequestHandler<
        InviteSystemManagerCommand,
        Result<InviteSystemManagerResponse>>
{
    private const string PendingInvitationConstraint =
        "uq_system_manager_invitations_pending_email";

    private readonly SystemManagerInvitationOptions _options =
        invitationOptions.Value;

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
            when (exception.IsUniqueConstraintViolation(
                PendingInvitationConstraint))
        {
            return Result.Failure<InviteSystemManagerResponse>(
                SystemManagerInvitationError.AlreadyPending());
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
                creation.Response.Email,
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
                creation.Response.InvitationId,
                creation.Response.Email,
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
        var existingUser =
            await userRepository.GetByEmailIncludingDeletedAsync(
            email,
            cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.DeletedAt is not null ||
                existingUser.Status != UserStatus.Active)
            {
                return Result.Failure<InvitationCreation>(
                    SystemManagerInvitationError.UserInactive());
            }

            var existingProfile =
                await profileRepository.GetByUserIdAsync(
                    existingUser.Id,
                    cancellationToken);

            if (existingProfile is not null)
            {
                return Result.Failure<InvitationCreation>(
                    SystemManagerInvitationError.AlreadySystemManager());
            }
        }

        var now = timeProvider.GetUtcNow();

        var pendingInvitation =
            await invitationRepository.GetPendingByEmailAsync(
                email,
                cancellationToken);

        if (pendingInvitation is not null)
        {
            if (pendingInvitation.CanBeAccepted(now))
            {
                return Result.Failure<InvitationCreation>(
                    SystemManagerInvitationError.AlreadyPending());
            }

            pendingInvitation.MarkExpired(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var generatedToken = invitationTokenService.Generate();
        var expiresAt = now.AddHours(_options.ExpirationHours);

        var invitation = SystemManagerInvitation.Create(
            email,
            generatedToken.TokenHash,
            actorId,
            expiresAt,
            now);

        invitationRepository.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new InvitationCreation(
                new InviteSystemManagerResponse(
                    invitation.Id,
                    invitation.Email,
                    invitation.ExpiresAt,
                    EmailSent: false),
                generatedToken.PlainTextToken));
    }

    private sealed record InvitationCreation(
        InviteSystemManagerResponse Response,
        string PlainTextToken);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "System Manager invitation {InvitationId} email to {Email} failed.")]
    private static partial void LogInvitationEmailFailure(
        ILogger logger,
        Guid invitationId,
        string email,
        Exception exception);
}
