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
            sourceMission.SurveyOrderId != payload.SurveyOrderId)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.MissionContextMismatch",
                "FarmBaseMapInternallyApprovedV1 does not match the source mission context.");
        }

        var updatedMissions = 0;
        try
        {
            if (sourceMission.ApplyInternallyApprovedFarmBaseMap(
                    payload.SurveyOrderId,
                    payload.ApprovalId,
                    payload.FarmBaseMapVersionId,
                    payload.ApprovedAt))
            {
                updatedMissions++;
            }

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
                    if (dep.ApplyInternallyApprovedFarmBaseMap(
                            payload.SurveyOrderId,
                            payload.ApprovalId,
                            payload.FarmBaseMapVersionId,
                            payload.ApprovedAt))
                    {
                        updatedMissions++;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapInternallyApprovedV1.MissionStateInvalid",
                exception.Message);
        }

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
