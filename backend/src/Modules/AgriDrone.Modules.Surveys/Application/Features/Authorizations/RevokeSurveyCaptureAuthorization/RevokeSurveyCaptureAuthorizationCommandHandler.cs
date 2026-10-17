using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.RevokeSurveyCaptureAuthorization;

internal sealed class RevokeSurveyCaptureAuthorizationCommandHandler(
    ISurveyCaptureAuthorizationRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RevokeSurveyCaptureAuthorizationCommand,
        Result<SurveyCaptureAuthorizationResponse>>
{
    public async Task<Result<SurveyCaptureAuthorizationResponse>> Handle(
        RevokeSurveyCaptureAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Forbidden());
        var authorization = await repository.GetAsync(command.SurveyOrderId, command.Type, cancellationToken);
        if (authorization is null)
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.NotFound());
        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new { authorization.Status, authorization.Version });
        try { authorization.Revoke(command.Reason, actorId, now, command.ExpectedVersion); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Conflict(exception.Message));
        }
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            authorization.Status, authorization.RevokedAt,
            authorization.RevokedBy, authorization.RevocationReason, authorization.Version
        });
        auditWriter.AddSystemAdminAction(unitOfWork, actorId, executionContext.CorrelationId,
            nameof(SurveyCaptureAuthorization), authorization.Id, "REVOKE", oldData, newData, now);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SurveyCaptureAuthorizationResponse>(SurveyCaptureAuthorizationError.Conflict("Authorization version conflict."));
        }
        return Result.Success(VerifySurveyCaptureAuthorizationCommandHandler.Map(authorization));
    }
}
