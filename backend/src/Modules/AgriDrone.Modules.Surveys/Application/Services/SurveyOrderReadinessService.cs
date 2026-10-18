using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Services;

internal sealed class SurveyOrderReadinessService(
    ISurveyOrderReadinessStateQueries stateQueries,
    TimeProvider timeProvider)
    : ISurveyOrderReadinessService
{
    public async Task<Result<SurveyOrderReadinessEvaluation>> EvaluateAsync(
        Guid surveyOrderId,
        Guid missionId,
        SurveyOperationPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var evaluatedAt = timeProvider.GetUtcNow();
        var stateResult = await stateQueries.GetCurrentAsync(
            surveyOrderId,
            missionId,
            purpose,
            evaluatedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<SurveyOrderReadinessEvaluation>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var decision = SurveyOrderReadinessPolicy.Evaluate(state.Snapshot);
        return Result.Success(new SurveyOrderReadinessEvaluation(
            state.SurveyOrderId,
            state.MissionId,
            state.TenantId,
            state.FarmId,
            purpose,
            state.OrderVersion,
            state.AppointmentId,
            state.SystemManagerProfileId,
            state.SystemManagerUserId,
            decision.IsReady,
            decision.Failures.Select(MapFailure).ToArray(),
            evaluatedAt));
    }

    private static string MapFailure(SurveyOrderReadinessFailure failure) =>
        failure switch
        {
            SurveyOrderReadinessFailure.BaselineMappingNotRequired =>
                "BASELINE_MAPPING_NOT_REQUIRED",
            SurveyOrderReadinessFailure.BoundaryNotApproved =>
                "BOUNDARY_NOT_APPROVED",
            SurveyOrderReadinessFailure.ScopeNotConfirmed =>
                "SCOPE_NOT_CONFIRMED",
            SurveyOrderReadinessFailure.AppointmentNotConfirmed =>
                "APPOINTMENT_NOT_CONFIRMED",
            SurveyOrderReadinessFailure.AppointmentPurposeMismatch =>
                "APPOINTMENT_PURPOSE_MISMATCH",
            SurveyOrderReadinessFailure.AppointmentNotCurrent =>
                "APPOINTMENT_NOT_CURRENT",
            SurveyOrderReadinessFailure.MissionNotPreparedForPurpose =>
                "MISSION_NOT_PREPARED_FOR_PURPOSE",
            SurveyOrderReadinessFailure.MissionScheduleMismatch =>
                "MISSION_SCHEDULE_MISMATCH",
            SurveyOrderReadinessFailure.DroneNotReady => "DRONE_NOT_READY",
            SurveyOrderReadinessFailure.PreflightNotCompleted =>
                "PREFLIGHT_NOT_COMPLETED",
            SurveyOrderReadinessFailure.FarmBaseMapNotPublished =>
                "FARM_BASE_MAP_NOT_PUBLISHED",
            SurveyOrderReadinessFailure.PoleCountNotConfirmed =>
                "POLE_COUNT_NOT_CONFIRMED",
            SurveyOrderReadinessFailure.PriceNotConfirmed =>
                "PRICE_NOT_CONFIRMED",
            SurveyOrderReadinessFailure.PaymentNotConfirmed =>
                "PAYMENT_NOT_CONFIRMED",
            SurveyOrderReadinessFailure.PaymentAmountOrCurrencyMismatch =>
                "PAYMENT_AMOUNT_OR_CURRENCY_MISMATCH",
            SurveyOrderReadinessFailure.PaymentRevoked =>
                "PAYMENT_REVOKED",
            SurveyOrderReadinessFailure.PrimaryManagerNotReady =>
                "PRIMARY_MANAGER_NOT_READY",
            SurveyOrderReadinessFailure.PrimaryManagerUnavailable =>
                "PRIMARY_MANAGER_UNAVAILABLE",
            SurveyOrderReadinessFailure.PriceAdjustmentPending =>
                "PRICE_ADJUSTMENT_PENDING",
            SurveyOrderReadinessFailure.SafetyChecksNotSatisfied =>
                "SAFETY_CHECKS_NOT_SATISFIED",
            SurveyOrderReadinessFailure.OrderNotEligible =>
                "ORDER_NOT_ELIGIBLE",
            SurveyOrderReadinessFailure.ServiceDisabled =>
                "SERVICE_DISABLED",
            SurveyOrderReadinessFailure.WrittenConsentNotValid =>
                "WRITTEN_CONSENT_NOT_VALID",
            SurveyOrderReadinessFailure.FlightPermissionNotValid =>
                "FLIGHT_PERMISSION_NOT_VALID",
            SurveyOrderReadinessFailure.FinancialSettlementPending =>
                "FINANCIAL_SETTLEMENT_PENDING",
            SurveyOrderReadinessFailure.ExtraFlightNotApproved =>
                "EXTRA_FLIGHT_NOT_APPROVED",
            SurveyOrderReadinessFailure.BoundaryCaptureNotFinalized =>
                "BOUNDARY_CAPTURE_NOT_FINALIZED",
            SurveyOrderReadinessFailure.BaselineRouteNotApproved =>
                "BASELINE_ROUTE_NOT_APPROVED",
            SurveyOrderReadinessFailure.BaselineRouteStale =>
                "BASELINE_ROUTE_STALE",
            SurveyOrderReadinessFailure.BaselineRouteOutsideSafetyInset =>
                "BASELINE_ROUTE_OUTSIDE_SAFETY_INSET",
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
}
