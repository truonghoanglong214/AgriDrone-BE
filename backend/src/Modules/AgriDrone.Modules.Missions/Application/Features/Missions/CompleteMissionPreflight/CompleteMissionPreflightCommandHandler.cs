using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

internal sealed class CompleteMissionPreflightCommandHandler(
    ISystemManagerAccessService managerAccessService,
    IDroneMissionRepository missionRepository,
    IPreflightChecklistRepository checklistRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        CompleteMissionPreflightCommand,
        Result<CompleteMissionPreflightResult>>
{
    public async Task<Result<CompleteMissionPreflightResult>> Handle(
        CompleteMissionPreflightCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId,
            cancellationToken);
        if (!access.IsAllowed ||
            access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.FarmAccessDenied());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            tenantId,
            request.FarmId,
            cancellationToken);
        if (mission is null)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.SurveyOrderId is null)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.MissionNotOrderBound());
        }

        if (mission.PreflightOperationId == request.OperationId)
        {
            var savedChecklist = await checklistRepository.GetByOperationIdAsync(
                mission.Id,
                request.OperationId,
                cancellationToken);
            return mission.MatchesPreflightOperation(
                request.OperationId,
                request.ChecklistVersion,
                request.Answers,
                request.SuitableForFlight,
                request.Notes ?? request.UnsuitableConditionNotes ?? request.FailsafeNotes)
                && savedChecklist is not null && savedChecklist.MatchesPayload(
                    request.ChecklistDefinitionId,
                    request.Answers,
                    request.UnsuitableConditionNotes ??
                        (request.SuitableForFlight ? null : request.Notes),
                    request.FailsafeNotes,
                    actorId,
                    request.DeviceCompletedAt,
                    request.FlightAuthorizationEvidence)
                ? Result.Success(new CompleteMissionPreflightResult(
                    MissionResponseMapper.Map(mission),
                    true))
                : Result.Failure<CompleteMissionPreflightResult>(
                    CompleteMissionPreflightError.OperationPayloadConflict(
                        request.OperationId));
        }

        if (mission.Status != MissionStatus.Scheduled)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.InvalidStatus(mission.Status));
        }

        if (mission.Version != request.ExpectedVersion)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                MissionError.VersionConflict(
                    request.ExpectedVersion,
                    mission.Version));
        }

        var definition = await checklistRepository.GetActiveDefinitionByIdAsync(
            request.ChecklistDefinitionId,
            "DRONE_PRE_FLIGHT",
            cancellationToken);
        if (definition is null ||
            !string.Equals(
                request.ChecklistVersion,
                $"v{definition.VersionNumber}",
                StringComparison.Ordinal))
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.StaleChecklistDefinition());
        }

        if (mission.PreflightOperationId.HasValue &&
            string.Equals(mission.PreflightChecklistVersion,
                request.ChecklistVersion, StringComparison.Ordinal))
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.AlreadyCompleted());
        }

        if (await checklistRepository.GetByOperationIdAsync(
                mission.Id, request.OperationId, cancellationToken) is not null)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.OperationPayloadConflict(request.OperationId));
        }

        var answerError = PreflightChecklistPolicy.ValidateResponses(
            definition.Items.RootElement,
            request.Answers.RootElement);
        if (answerError is not null)
        {
            return Result.Failure<CompleteMissionPreflightResult>(answerError);
        }

        if (request.SuitableForFlight &&
            (request.Answers.RootElement.EnumerateObject().Any(answer =>
                 answer.Value.ValueKind == JsonValueKind.False) ||
             string.IsNullOrWhiteSpace(request.FlightAuthorizationEvidence) ||
             string.IsNullOrWhiteSpace(request.FailsafeNotes)))
            return Result.Failure<CompleteMissionPreflightResult>(
                CompleteMissionPreflightError.SafetyEvidenceRequired());

        var now = timeProvider.GetUtcNow();
        if (mission.PreflightOperationId.HasValue)
        {
            var previousChecklist = await checklistRepository.GetByOperationIdAsync(
                mission.Id, mission.PreflightOperationId.Value, cancellationToken);
            previousChecklist?.Supersede();
            mission.ReplaceStalePreflight(now);
        }
        mission.CompletePreflight(
            request.OperationId,
            request.ChecklistVersion,
            request.Answers,
            request.SuitableForFlight,
            request.Notes ?? request.UnsuitableConditionNotes ?? request.FailsafeNotes,
            actorId,
            now);

        var checklist = MissionPreflightChecklist.Complete(
            mission.Id,
            definition,
            request.OperationId,
            request.Answers,
            request.UnsuitableConditionNotes ??
                (request.SuitableForFlight ? null : request.Notes),
            request.FailsafeNotes,
            actorId,
            request.DeviceCompletedAt ?? now,
            now,
            request.FlightAuthorizationEvidence);
        checklistRepository.AddCompleted(checklist);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            mission.SurveyOrderId,
            mission.PreflightOperationId,
            mission.PreflightChecklistVersion,
            mission.PreflightSuitableForFlight,
            mission.PreflightNotes,
            mission.PreflightConfirmedAt,
            ChecklistDefinitionId = definition.Id,
            ChecklistVersion = definition.VersionNumber,
            request.FlightAuthorizationEvidence
        });
        auditWriter.AddUserAction(
            unitOfWork,
            mission.TenantId,
            mission.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(DroneMission),
            mission.Id,
            "COMPLETE_PREFLIGHT",
            null,
            auditData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                MissionError.ConcurrentUpdate());
        }
        catch (PreflightChecklistConflictException)
        {
            return Result.Failure<CompleteMissionPreflightResult>(
                AppError.Conflict(
                    "MissionPreflight.ConcurrentCompletion",
                    "A preflight checklist completion already exists. Retry with the original operation ID."));
        }

        return Result.Success(new CompleteMissionPreflightResult(
            MissionResponseMapper.Map(mission),
            false));
    }
}
