using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal static class SystemManagerProfileMutationExecutor
{
    public static async Task<Result<SystemManagerProfileResponse>> ExecuteAsync(
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

        if (profile.Version != expectedVersion)
        {
            return Result.Failure<SystemManagerProfileResponse>(
                SystemManagerError.ConcurrentUpdate());
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

        return Result.Success(SystemManagerProfileResponseMapper.ToResponse(profile));
    }

    private static JsonDocument Snapshot(
        SystemManagerProfile profile,
        string reason) =>
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
