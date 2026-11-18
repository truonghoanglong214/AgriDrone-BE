using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class FarmBaseMapInternallyApprovedV1Handler(
    MissionsDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<FarmBaseMapInternallyApprovedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<FarmBaseMapInternallyApprovedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be2FarmBaseMapInternallyApprovedV1,
            envelope,
            (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        MissionsDbContext context,
        IntegrationEventEnvelope<FarmBaseMapInternallyApprovedV1> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        if (envelope.ActorId != payload.ApprovedBy)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.EnvelopeMismatch",
                "Approver does not match the event envelope actor.");
        }

        var sourceMission = await context.DroneMissions.SingleOrDefaultAsync(
            mission => mission.Id == payload.SourceMissionId, cancellationToken);

        if (sourceMission is null)
        {
            return InboxHandlerResult.Retry("The source mapping mission is not available yet.");
        }

        if (sourceMission.TenantId != envelope.TenantId ||
            sourceMission.FarmId != payload.FarmId ||
            sourceMission.SurveyOrderId != payload.SurveyOrderId ||
            sourceMission.CaptureAttemptId != payload.CaptureAttemptId ||
            sourceMission.FarmBoundaryVersionId != payload.FarmBoundaryVersionId)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.MissionContextMismatch",
                "FarmBaseMapInternallyApprovedV1 does not match the source mission context.");
        }

        var isPurposeValid = payload.MappingKind switch
        {
            MappingKind.FirstBaseline => sourceMission.Purpose == MissionPurpose.BaselineMapping,
            MappingKind.FollowUpSnapshot => sourceMission.Purpose is
                MissionPurpose.PlantHealth or MissionPurpose.HarvestReadiness,
            _ => false
        };

        if (!isPurposeValid)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.MappingKindMismatch",
                $"MappingKind '{payload.MappingKind}' does not match source mission purpose '{sourceMission.Purpose}'.");
        }

        var outcome = sourceMission.ApplyInternallyApprovedFarmBaseMap(
            payload.SurveyOrderId,
            payload.ApprovalId,
            payload.FarmBaseMapVersionId,
            payload.ApprovedAt);

        if (outcome == DroneMissionApprovalOutcome.Conflict)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.MissionStateConflict",
                "Source mission cannot accept internal approval in its current state.");
        }

        var updatedMissions = outcome == DroneMissionApprovalOutcome.Applied ? 1 : 0;

        if (payload.MappingKind == MappingKind.FirstBaseline)
        {
            var dependentMissions = await context.DroneMissions
                .Where(m => m.SurveyOrderId == payload.SurveyOrderId &&
                            m.TenantId == envelope.TenantId &&
                            m.FarmId == payload.FarmId &&
                            m.RequiresBaselineCompletion &&
                            m.Id != sourceMission.Id)
                .ToListAsync(cancellationToken);

            foreach (var dep in dependentMissions)
            {
                var depOutcome = dep.ApplyInternallyApprovedFarmBaseMap(
                    payload.SurveyOrderId,
                    payload.ApprovalId,
                    payload.FarmBaseMapVersionId,
                    payload.ApprovedAt);

                if (depOutcome == DroneMissionApprovalOutcome.Conflict)
                {
                    return InboxHandlerResult.PermanentFailure(
                        "FarmBaseMapInternallyApprovedV1.DependentMissionConflict",
                        $"Dependent mission '{dep.Id}' cannot accept baseline completion in its current state.");
                }

                if (depOutcome == DroneMissionApprovalOutcome.Applied)
                {
                    updatedMissions++;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
        {
            payload.ApprovalId,
            payload.SurveyOrderId,
            payload.SourceMissionId,
            payload.FarmBaseMapVersionId,
            UpdatedMissionCount = updatedMissions
        }));
    }
}
