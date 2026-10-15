using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;

internal sealed class VerifySurveyCaptureAuthorizationCommandHandler(
    ISurveyCaptureAuthorizationRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<VerifySurveyCaptureAuthorizationCommand,
        Result<SurveyCaptureAuthorizationResponse>>
{
    public async Task<Result<SurveyCaptureAuthorizationResponse>> Handle(
        VerifySurveyCaptureAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Forbidden());
        var authorization = await repository.GetAsync(command.SurveyOrderId, command.Type, cancellationToken);
        if (authorization is null)
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.NotFound());
        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new { authorization.Status, authorization.Version });
        try
        {
            authorization.Verify(command.EvidenceReference, command.IssuedAt.ToUniversalTime(),
                command.ExpiresAt?.ToUniversalTime(), actorId, now, command.ExpectedVersion);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Conflict(exception.Message));
        }
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            authorization.Status, authorization.EvidenceReference, authorization.IssuedAt,
            authorization.VerifiedAt, authorization.ExpiresAt, authorization.VerifiedBy,
            authorization.Version
        });
        auditWriter.AddSystemAdminAction(unitOfWork, actorId, executionContext.CorrelationId,
            nameof(SurveyCaptureAuthorization), authorization.Id, "VERIFY", oldData, newData, now);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Conflict("Authorization version conflict."));
        }
        return Result.Success(Map(authorization));
    }

    internal static SurveyCaptureAuthorizationResponse Map(SurveyCaptureAuthorization item) =>
        new(item.Id, item.SurveyOrderId, item.Type, item.Status, item.VerifiedAt,
            item.ExpiresAt, item.RevokedAt, item.Version);
}
