using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record SystemManagerProfileResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    SystemManagerProfileStatus Status,
    SystemManagerAvailabilityStatus Availability,
    FlightQualificationStatus QualificationStatus,
    DateTimeOffset? QualificationExpiresAt,
    long Version);

public sealed record CreateSystemManagerProfileCommand(Guid UserId)
    : IRequest<Result<SystemManagerProfileResponse>>;

public sealed record ActivateSystemManagerProfileCommand(
    Guid ProfileId,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;

public sealed record SuspendSystemManagerProfileCommand(
    Guid ProfileId,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;

public sealed record UpdateSystemManagerAvailabilityCommand(
    Guid ProfileId,
    SystemManagerAvailabilityStatus Availability,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;

public sealed record UpdateSystemManagerQualificationCommand(
    Guid ProfileId,
    FlightQualificationStatus Status,
    DateTimeOffset? ExpiresAt,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;

internal sealed class CreateSystemManagerProfileCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISystemManagerProfileRepository profileRepository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSystemManagerProfileCommand, Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        CreateSystemManagerProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                UserError.NotFound(request.UserId));
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.UserMustBeActive());
        }

        if (await profileRepository.GetByUserIdAsync(user.Id, cancellationToken) is not null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ProfileAlreadyExists());
        }

        var role = await roleRepository.GetByCodeAsync(
            SystemRoles.SystemManager,
            cancellationToken);
        if (role is null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.RoleMissing());
        }

        var now = timeProvider.GetUtcNow();
        var profile = SystemManagerProfile.Create(user.Id, now);
        user.AssignSystemRole(role.Id, now);
        profileRepository.Add(profile);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            profile.UserId,
            Status = profile.Status.ToString(),
            Availability = profile.Availability.ToString(),
            QualificationStatus = profile.QualificationStatus.ToString()
        });
        auditWriter.AddSystemAdminAction(
            auditLogSink,
            actorId,
            executionContext.CorrelationId,
            "SystemManagerProfile",
            profile.Id,
            "CREATE",
            null,
            newData,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(profile, user));
    }

    internal static SystemManagerProfileResponse ToResponse(
        SystemManagerProfile profile,
        User? user = null) =>
        new(
            profile.Id,
            profile.UserId,
            (user ?? profile.User).Email,
            (user ?? profile.User).FullName,
            profile.Status,
            profile.Availability,
            profile.QualificationStatus,
            profile.QualificationExpiresAt,
            profile.Version);
}

internal abstract class SystemManagerProfileMutationHandler
{
    protected static AppError? Validate(
        SystemManagerProfile profile,
        long expectedVersion) =>
        profile.Version == expectedVersion
            ? null
            : SystemManagerError.ConcurrentUpdate();

    protected static JsonDocument Snapshot(SystemManagerProfile profile, string reason) =>
        JsonSerializer.SerializeToDocument(new
        {
            profile.Status,
            profile.Availability,
            profile.QualificationStatus,
            profile.QualificationExpiresAt,
            profile.Version,
            Reason = reason
        });
}

internal sealed class ActivateSystemManagerProfileCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : SystemManagerProfileMutationHandler,
      IRequestHandler<ActivateSystemManagerProfileCommand, Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        ActivateSystemManagerProfileCommand request,
        CancellationToken cancellationToken) =>
        await MutateAsync(
            repository,
            unitOfWork,
            auditWriter,
            auditLogSink,
            executionContext,
            timeProvider,
            request.ProfileId,
            request.Reason,
            request.ExpectedVersion,
            "ACTIVATE",
            (profile, now) => profile.Activate(now, request.ExpectedVersion),
            cancellationToken);

    internal static async Task<Result<SystemManagerProfileResponse>> MutateAsync(
        ISystemManagerProfileRepository repository,
        IIdentityUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IAuditLogSink auditLogSink,
        IExecutionContext executionContext,
        TimeProvider timeProvider,
        Guid profileId,
        string reason,
        long expectedVersion,
        string action,
        Action<SystemManagerProfile, DateTimeOffset> mutation,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var profile = await repository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ProfileNotFound());
        }

        var versionError = Validate(profile, expectedVersion);
        if (versionError is not null)
        {
            return Result.Failure<SystemManagerProfileResponse>(versionError);
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = Snapshot(profile, reason);
        mutation(profile, now);
        using var newData = Snapshot(profile, reason);

        auditWriter.AddSystemAdminAction(
            auditLogSink,
            actorId,
            executionContext.CorrelationId,
            "SystemManagerProfile",
            profile.Id,
            action,
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ConcurrentUpdate());
        }

        return Result.Success(CreateSystemManagerProfileCommandHandler.ToResponse(profile));
    }
}

internal sealed class SuspendSystemManagerProfileCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<SuspendSystemManagerProfileCommand, Result<SystemManagerProfileResponse>>
{
    public Task<Result<SystemManagerProfileResponse>> Handle(
        SuspendSystemManagerProfileCommand request,
        CancellationToken cancellationToken) =>
        ActivateSystemManagerProfileCommandHandler.MutateAsync(
            repository, unitOfWork, auditWriter, auditLogSink, executionContext,
            timeProvider, request.ProfileId, request.Reason, request.ExpectedVersion,
            "SUSPEND", (profile, now) => profile.Suspend(now, request.ExpectedVersion),
            cancellationToken);
}

internal sealed class UpdateSystemManagerAvailabilityCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSystemManagerAvailabilityCommand, Result<SystemManagerProfileResponse>>
{
    public Task<Result<SystemManagerProfileResponse>> Handle(
        UpdateSystemManagerAvailabilityCommand request,
        CancellationToken cancellationToken) =>
        ActivateSystemManagerProfileCommandHandler.MutateAsync(
            repository, unitOfWork, auditWriter, auditLogSink, executionContext,
            timeProvider, request.ProfileId, request.Reason, request.ExpectedVersion,
            "UPDATE_AVAILABILITY",
            (profile, now) => profile.UpdateAvailability(
                request.Availability, now, request.ExpectedVersion),
            cancellationToken);
}

internal sealed class UpdateSystemManagerQualificationCommandHandler(
    ISystemManagerProfileRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IAuditLogSink auditLogSink,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSystemManagerQualificationCommand, Result<SystemManagerProfileResponse>>
{
    public async Task<Result<SystemManagerProfileResponse>> Handle(
        UpdateSystemManagerQualificationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Status == FlightQualificationStatus.Qualified &&
            (!request.ExpiresAt.HasValue ||
             request.ExpiresAt.Value <= timeProvider.GetUtcNow()))
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.InvalidQualificationExpiry());
        }

        return await ActivateSystemManagerProfileCommandHandler.MutateAsync(
            repository, unitOfWork, auditWriter, auditLogSink, executionContext,
            timeProvider, request.ProfileId, request.Reason, request.ExpectedVersion,
            "UPDATE_QUALIFICATION",
            (profile, now) => profile.UpdateQualification(
                request.Status, request.ExpiresAt, now, request.ExpectedVersion),
            cancellationToken);
    }
}
