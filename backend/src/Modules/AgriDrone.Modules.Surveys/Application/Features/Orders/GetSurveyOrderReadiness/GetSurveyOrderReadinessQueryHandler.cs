using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

internal sealed class GetSurveyOrderReadinessQueryHandler(
    ISurveyBaselineReadinessQueries readinessQueries,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetSurveyOrderReadinessQuery,
        Result<SurveyOrderReadinessResponse>>
{
    public async Task<Result<SurveyOrderReadinessResponse>> Handle(
        GetSurveyOrderReadinessQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SurveyOrderReadinessResponse>(
                SurveyAppointmentError.Forbidden());
        }

        var evaluatedAt = timeProvider.GetUtcNow();
        var stateResult = await readinessQueries.GetAsync(
            query.SurveyOrderId,
            query.MissionId,
            actorId,
            evaluatedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<SurveyOrderReadinessResponse>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var decision = SurveyOrderReadinessPolicy.Evaluate(state.Snapshot);
        return Result.Success(new SurveyOrderReadinessResponse(
            state.SurveyOrderId,
            query.MissionId,
            state.TenantId,
            state.FarmId,
            "BASELINE_MAPPING",
            state.OrderVersion,
            state.AppointmentId,
            state.SystemManagerProfileId,
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
            SurveyOrderReadinessFailure.PrimaryManagerNotReady =>
                "PRIMARY_MANAGER_NOT_READY",
            SurveyOrderReadinessFailure.PrimaryManagerUnavailable =>
                "PRIMARY_MANAGER_UNAVAILABLE",
            SurveyOrderReadinessFailure.SafetyChecksNotSatisfied =>
                "SAFETY_CHECKS_NOT_SATISFIED",
            SurveyOrderReadinessFailure.OrderNotEligible =>
                "ORDER_NOT_ELIGIBLE",
            _ => throw new InvalidOperationException(
                $"Unsupported Baseline readiness failure '{failure}'.")
        };
}
