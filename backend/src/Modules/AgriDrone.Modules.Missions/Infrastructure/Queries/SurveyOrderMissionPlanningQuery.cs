using System.Data.Common;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class SurveyOrderMissionPlanningQuery(
    ISurveyOrderOperationalContextV3Query operationalContextQuery)
    : ISurveyOrderMissionPlanningQuery
{
    public async Task<SurveyOrderMissionPlanningContext?> GetAsync(
        Guid surveyOrderId, CancellationToken cancellationToken = default)
    {
        // Read the selected service first, then derive planning and flight gates
        // from the purpose-specific authoritative order context.
        var source = await ReadAsync(surveyOrderId, nameof(MissionPurpose.PlantHealth),
            cancellationToken);
        if (source is null) return null;
        var servicePurpose = source.ServiceCode switch
        {
            "PLANT_HEALTH" => MissionPurpose.PlantHealth,
            "HARVEST_READINESS" => MissionPurpose.HarvestReadiness,
            _ => throw new SurveyOrderMissionPlanningUnavailableException()
        };
        if (servicePurpose != MissionPurpose.PlantHealth)
            source = await ReadAsync(surveyOrderId, servicePurpose.ToString(), cancellationToken);
        if (source is null) return null;

        if (source.RequiresBaselineMapping && source.FarmBaseMapVersionId is null)
            source = await ReadAsync(surveyOrderId, nameof(MissionPurpose.BaselineMapping),
                cancellationToken);
        return source is null ? null : Map(source);
    }

    public async Task<SurveyOrderMissionPlanningContext?> GetForPurposeAsync(
        Guid surveyOrderId, MissionPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var source = await ReadAsync(surveyOrderId, purpose.ToString(), cancellationToken);
        return source is null ? null : Map(source);
    }

    private async Task<SurveyOrderOperationalContextV3?> ReadAsync(
        Guid surveyOrderId, string purpose, CancellationToken cancellationToken)
    {
        try
        {
            return await operationalContextQuery.GetAsync(
                surveyOrderId, purpose, cancellationToken);
        }
        catch (SurveyOrderOperationalContextUnavailableException)
        {
            throw new SurveyOrderMissionPlanningUnavailableException();
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            throw new SurveyOrderMissionPlanningUnavailableException();
        }
    }

    private static SurveyOrderMissionPlanningContext Map(
        SurveyOrderOperationalContextV3 source)
    {
        var selectedService = source.ServiceCode switch
        {
            "PLANT_HEALTH" => SurveyServiceType.PlantHealth,
            "HARVEST_READINESS" => SurveyServiceType.HarvestReadiness,
            _ => throw new SurveyOrderMissionPlanningUnavailableException()
        };
        var start = source.AppointmentStartAt ?? DateTimeOffset.UnixEpoch;
        var end = source.AppointmentEndAt ?? start.AddSeconds(1);
        var purposeMatches = source.RequestedMissionPurpose == nameof(MissionPurpose.BaselineMapping) ||
            source.RequestedMissionPurpose == selectedService.ToString();
        var ready = purposeMatches && source.IsReady && source.AppointmentStartAt.HasValue &&
                    source.AppointmentEndAt.HasValue;
        var isBaselinePlanning = source.RequiresBaselineMapping &&
                                 source.FarmBaseMapVersionId is null;
        var planningStatusAllowed = isBaselinePlanning
            ? source.OrderStatus is "AwaitingBaselineAppointment" or "BaselineReady"
            : source.OrderStatus is "AwaitingPaidAppointment" or "AwaitingPayment" or
                "ReadyForPaidService";
        var planningFailures = source.ReadinessFailures.Where(failure =>
            failure is not ("OrderNotEligible" or "AppointmentNotConfirmed" or
                "AppointmentPurposeMismatch" or "PaymentNotConfirmed" or
                "PriceAdjustmentPending"));
        var canPlan = purposeMatches && planningStatusAllowed &&
                      source.FarmBoundaryVersionId.HasValue &&
                      source.PrimarySystemManagerId.HasValue &&
                      !planningFailures.Any();
        var canSchedule = canPlan && source.AppointmentStatus == "Confirmed" &&
                          source.AppointmentStartAt.HasValue &&
                          source.AppointmentEndAt.HasValue &&
                          source.AppointmentEndAt.Value > source.AppointmentStartAt.Value;
        return new SurveyOrderMissionPlanningContext(
            source.SurveyOrderId, source.TenantId, source.FarmId,
            selectedService,
            source.RequiresBaselineMapping && source.FarmBaseMapVersionId is null,
            source.FarmBaseMapVersionId, [], start, end, ready,
            (!purposeMatches ? "PURPOSE_MISMATCH" : null) ??
            (source.ReadinessFailures.Count > 0 ? source.ReadinessFailures[0] : null) ??
            (ready ? null : "ORDER_NOT_READY"), canPlan, canSchedule);
    }
}
