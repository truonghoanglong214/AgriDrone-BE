using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderOperationalContextV3Query(
    AgriDroneSchemaDbContext db,
    ISystemManagerAccessService managerAccess,
    TimeProvider clock) : ISurveyOrderOperationalContextV3Query
{
    public async Task<SurveyOrderOperationalContextV3?> GetAsync(
        Guid surveyOrderId, string requestedMissionPurpose,
        CancellationToken cancellationToken = default)
    {
        var purpose = requestedMissionPurpose switch
        {
            nameof(SurveyOperationPurpose.BaselineMapping) => SurveyOperationPurpose.BaselineMapping,
            nameof(SurveyOperationPurpose.PlantHealth) => SurveyOperationPurpose.PlantHealth,
            nameof(SurveyOperationPurpose.HarvestReadiness) => SurveyOperationPurpose.HarvestReadiness,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedMissionPurpose))
        };

        var order = await db.Set<SurveyOrder>().AsNoTracking()
            .Include(value => value.SurveyService)
            .Include(value => value.Appointments)
            .Include(value => value.Payments)
            .SingleOrDefaultAsync(value => value.Id == surveyOrderId, cancellationToken);
        if (order is null) return null;

        var appointmentPurpose = purpose == SurveyOperationPurpose.BaselineMapping
            ? SurveyAppointmentPurpose.BaselineMapping
            : SurveyAppointmentPurpose.PaidService;
        var appointment = order.Appointments
            .Where(value => value.Purpose == appointmentPurpose)
            .OrderByDescending(value => value.UpdatedAt)
            .FirstOrDefault();
        var payment = order.Payments
            .OrderByDescending(value => value.UpdatedAt)
            .FirstOrDefault();
        var access = await managerAccess.ResolveFarmAccessAsync(order.FarmId, cancellationToken);
        var managerReady = access.IsAllowed && access.TenantId == order.TenantId;
        var boundaryApproved = order.FarmBoundaryVersionId is Guid boundaryId &&
            await db.Set<FarmBoundary>().AsNoTracking().AnyAsync(value =>
                value.Id == boundaryId && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.Status == FarmBoundaryStatus.Approved,
                cancellationToken);
        var mapPublished = order.FarmBaseMapVersionId is Guid mapId &&
            await db.Set<FarmBaseMapVersion>().AsNoTracking().AnyAsync(value =>
                value.Id == mapId && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.Status == FarmBaseMapStatus.Published,
                cancellationToken);
        var pendingAdjustment = await db.Set<PriceAdjustment>().AsNoTracking().AnyAsync(value =>
            value.SurveyOrderId == order.Id &&
            (value.Status == PriceAdjustmentStatus.Pending ||
             value.Status == PriceAdjustmentStatus.Approved), cancellationToken);

        var snapshot = new SurveyOrderReadinessSnapshot(
            order.Status, purpose, order.RequiresBaselineMapping,
            boundaryApproved, order.ScopeConfirmedAt.HasValue,
            mapPublished, order.ConfirmedSurveyPoleCount.HasValue,
            order.PricePerPoleSnapshot.HasValue && order.SurveyServicePriceId.HasValue,
            appointment?.Purpose, appointment?.Status, payment?.Status,
            managerReady, pendingAdjustment,
            boundaryApproved && managerReady);
        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        return new SurveyOrderOperationalContextV3(
            order.Id, order.TenantId, order.FarmId, order.SurveyService.Code,
            order.SurveyService.ServiceType.ToString(), purpose.ToString(),
            order.Status.ToString(), order.Version, order.FarmBoundaryVersionId,
            order.FarmBaseMapVersionId, order.RequiresBaselineMapping,
            order.ConfirmedSurveyPoleCount?.Value, order.SurveyServicePriceId,
            order.PricePerPoleSnapshot?.Amount, order.Currency, order.FinalPrice,
            appointment?.Id, appointment?.Purpose.ToString(),
            appointment?.ProposedStartAt, appointment?.ProposedEndAt,
            appointment?.Status.ToString(), payment?.Id, payment?.Status.ToString(),
            managerReady ? access.ManagerProfileId : null,
            order.PreviousCompatibleOrderId,
            decision.IsReady, decision.Failures.Select(value => value.ToString()).ToArray(),
            clock.GetUtcNow());
    }
}
