using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderReadinessStateQueries(NpgsqlDataSource dataSource)
    : ISurveyOrderReadinessStateQueries
{
    public async Task<Result<SurveyOrderReadinessState>> GetCurrentAsync(
        Guid surveyOrderId,
        Guid missionId,
        SurveyOperationPurpose purpose,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var appointmentPurpose = purpose == SurveyOperationPurpose.BaselineMapping
            ? "BASELINE_MAPPING"
            : "PAID_SERVICE";
        var missionPurpose = MapPurpose(purpose);

        await using var connection = await dataSource.OpenConnectionAsync(
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                survey_order.tenant_id,
                survey_order.farm_id,
                survey_order.status::text,
                survey_order.xmin::text::bigint,
                survey_order.requires_baseline_mapping,
                survey_order.farm_boundary_version_id,
                survey_order.scope_confirmed_by,
                survey_order.confirmed_survey_pole_count,
                survey_order.price_per_pole_snapshot,
                survey_order.final_price,
                survey_order.remaining_amount,
                survey_order.currency,
                survey_order.deposit_payment_status::text AS deposit_payment_status,
                service.service_type::text AS service_type,
                boundary.id AS approved_boundary_id,
                base_map.id AS published_base_map_id,
                appointment.id AS appointment_id,
                appointment.purpose::text AS appointment_purpose,
                appointment.status::text AS appointment_status,
                appointment.proposed_start_at,
                appointment.proposed_end_at,
                profile.id AS manager_profile_id,
                profile.user_id AS manager_user_id,
                profile.status::text AS manager_status,
                profile.availability::text AS manager_availability,
                mission.id AS mission_id,
                mission.mission_purpose::text AS mission_purpose,
                mission.status::text AS mission_status,
                mission.scheduled_at,
                mission.scheduled_end_at,
                mission.pilot_user_id,
                drone.status::text AS drone_status,
                preflight.id AS preflight_id,
                preflight.unsuitable_condition_notes,
                preflight.failsafe_notes,
                payment.status::text AS payment_status,
                payment.amount AS payment_amount,
                payment.currency AS payment_currency,
                EXISTS (
                    SELECT 1
                    FROM survey.price_adjustments AS adjustment
                    WHERE adjustment.survey_order_id = survey_order.id
                      AND adjustment.status IN (
                        'PENDING'::system.price_adjustment_status,
                        'APPROVED'::system.price_adjustment_status)
                ) AS has_open_adjustment
                , EXISTS (
                    SELECT 1
                    FROM survey.adjustment_settlements AS settlement
                    JOIN survey.price_adjustments AS settlement_adjustment
                      ON settlement_adjustment.id = settlement.price_adjustment_id
                    WHERE settlement_adjustment.survey_order_id = survey_order.id
                      AND settlement.status IN ('Pending', 'Processing', 'Failed')
                ) AS has_pending_settlement
                , EXISTS (
                    SELECT 1
                    FROM survey.extra_flight_requests AS extra_flight
                    WHERE extra_flight.survey_order_id = survey_order.id
                      AND extra_flight.status = 'Pending'
                ) AS has_unapproved_extra_flight
                , EXISTS (
                    SELECT 1
                    FROM survey.boundary_capture_sessions AS capture
                    WHERE capture.id = survey_order.boundary_capture_session_id
                      AND capture.survey_order_id = survey_order.id
                      AND capture.farm_boundary_version_id =
                          survey_order.farm_boundary_version_id
                      AND capture.status = 'Finalized'
                ) AS has_finalized_boundary_capture
                , EXISTS (
                    SELECT 1
                    FROM survey.baseline_route_plan_references AS route
                    WHERE route.route_plan_id = survey_order.baseline_route_plan_id
                      AND route.survey_order_id = survey_order.id
                      AND route.status = 'Approved'
                ) AS has_approved_baseline_route
                , EXISTS (
                    SELECT 1
                    FROM survey.baseline_route_plan_references AS route
                    WHERE route.route_plan_id = survey_order.baseline_route_plan_id
                      AND route.survey_order_id = survey_order.id
                      AND route.boundary_capture_session_id =
                          survey_order.boundary_capture_session_id
                      AND route.farm_boundary_version_id =
                          survey_order.farm_boundary_version_id
                ) AS is_baseline_route_current
                , EXISTS (
                    SELECT 1
                    FROM survey.baseline_route_plan_references AS route
                    WHERE route.route_plan_id = survey_order.baseline_route_plan_id
                      AND route.survey_order_id = survey_order.id
                      AND route.is_within_safety_inset
                ) AS is_baseline_route_within_safety_inset
            FROM survey.survey_orders AS survey_order
            JOIN survey.survey_services AS service
              ON service.id = survey_order.survey_service_id
            LEFT JOIN farm.farm_boundaries AS boundary
              ON boundary.id = survey_order.farm_boundary_version_id
             AND boundary.tenant_id = survey_order.tenant_id
             AND boundary.farm_id = survey_order.farm_id
             AND boundary.status = 'APPROVED'::system.farm_boundary_status
            LEFT JOIN farm.farm_base_map_versions AS base_map
              ON base_map.id = survey_order.farm_base_map_version_id
             AND base_map.tenant_id = survey_order.tenant_id
             AND base_map.farm_id = survey_order.farm_id
             AND base_map.status = 'PUBLISHED'::system.farm_base_map_status
            LEFT JOIN LATERAL (
                SELECT item.*
                FROM survey.survey_appointments AS item
                WHERE item.survey_order_id = survey_order.id
                  AND item.purpose = CAST(
                    @appointment_purpose AS system.survey_appointment_purpose)
                  AND item.status IN (
                    'PROPOSED'::system.survey_appointment_status,
                    'CONFIRMED'::system.survey_appointment_status,
                    'RESCHEDULE_REQUESTED'::system.survey_appointment_status)
                ORDER BY item.revision_number DESC
                LIMIT 1
            ) AS appointment ON TRUE
            LEFT JOIN identity.farm_manager_assignments AS assignment
              ON assignment.tenant_id = survey_order.tenant_id
             AND assignment.farm_id = survey_order.farm_id
             AND assignment.ended_at IS NULL
            LEFT JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
            LEFT JOIN mission.drone_missions AS mission
              ON mission.id = @mission_id
             AND mission.survey_order_id = survey_order.id
             AND mission.tenant_id = survey_order.tenant_id
             AND mission.farm_id = survey_order.farm_id
            LEFT JOIN mission.drones AS drone ON drone.id = mission.drone_id
            LEFT JOIN LATERAL (
                SELECT checklist.id,
                       checklist.unsuitable_condition_notes,
                       checklist.failsafe_notes
                FROM mission.mission_preflight_checklists AS checklist
                WHERE checklist.mission_id = mission.id
                  AND checklist.status =
                    'COMPLETED'::system.mission_preflight_checklist_status
                ORDER BY checklist.completed_at DESC
                LIMIT 1
            ) AS preflight ON TRUE
            LEFT JOIN LATERAL (
                SELECT item.status, item.amount, item.currency
                FROM survey.survey_payments AS item
                WHERE item.survey_order_id = survey_order.id
                  AND item.purpose = CASE
                      WHEN @appointment_purpose = 'BASELINE_MAPPING' THEN 'BaselineDeposit'
                      ELSE 'FinalBalance'
                  END
                ORDER BY item.created_at DESC
                LIMIT 1
            ) AS payment ON TRUE
            WHERE survey_order.id = @order_id
            """;
        command.Parameters.AddWithValue("order_id", surveyOrderId);
        command.Parameters.AddWithValue("mission_id", missionId);
        command.Parameters.AddWithValue(
            "appointment_purpose",
            appointmentPurpose);
        command.Parameters.AddWithValue("evaluated_at", evaluatedAt);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Result.Failure<SurveyOrderReadinessState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var appointmentId = ReadNullableGuid(reader, "appointment_id");
        var appointmentStatus = ParseAppointmentStatus(
            ReadNullableString(reader, "appointment_status"));
        var appointmentStart = ReadNullableTimestamp(
            reader,
            "proposed_start_at");
        var appointmentEnd = ReadNullableTimestamp(
            reader,
            "proposed_end_at");
        var managerUserId = ReadNullableGuid(reader, "manager_user_id");
        var missionStatus = ReadNullableString(reader, "mission_status");
        var actualMissionPurpose = ReadNullableString(
            reader,
            "mission_purpose");
        var scheduledAt = ReadNullableTimestamp(reader, "scheduled_at");
        var scheduledEndAt = ReadNullableTimestamp(
            reader,
            "scheduled_end_at");
        var serviceTypeMatches = purpose switch
        {
            SurveyOperationPurpose.BaselineMapping => true,
            SurveyOperationPurpose.PlantHealth =>
                reader.GetString(reader.GetOrdinal("service_type")) ==
                    "PLANT_HEALTH",
            SurveyOperationPurpose.HarvestReadiness =>
                reader.GetString(reader.GetOrdinal("service_type")) ==
                    "HARVEST_READINESS",
            _ => false
        };
        var missionPrepared = serviceTypeMatches &&
            ReadNullableGuid(reader, "mission_id").HasValue &&
            actualMissionPurpose == missionPurpose &&
            missionStatus == "SCHEDULED" &&
            ReadNullableGuid(reader, "pilot_user_id") == managerUserId;
        var scheduleMatches = missionPrepared &&
            appointmentStart.HasValue &&
            appointmentEnd.HasValue &&
            scheduledAt >= appointmentStart &&
            scheduledEndAt <= appointmentEnd;
        var preflightCompleted = ReadNullableGuid(
            reader,
            "preflight_id").HasValue;
        var safetySatisfied = preflightCompleted &&
            ReadNullableString(reader, "unsuitable_condition_notes") is null &&
            ReadNullableString(reader, "failsafe_notes") is null;
        var paymentStatus = ParsePaymentStatus(
            ReadNullableString(reader, "payment_status"));
        var expectedAmount = ReadNullableDecimal(reader, "remaining_amount") ?? ReadNullableDecimal(reader, "final_price");
        var paymentAmount = ReadNullableDecimal(reader, "payment_amount");
        var expectedCurrency = ReadNullableString(reader, "currency")?.Trim();
        var paymentCurrency = ReadNullableString(
            reader,
            "payment_currency")?.Trim();
        var isPaidPurpose = purpose is SurveyOperationPurpose.PlantHealth or
            SurveyOperationPurpose.HarvestReadiness;
        var paymentMatches = !isPaidPurpose ||
            (expectedAmount.HasValue &&
             paymentAmount == expectedAmount &&
             string.Equals(
                 paymentCurrency,
                 expectedCurrency,
                 StringComparison.Ordinal));

        var depositPaymentStatus = ParsePaymentStatus(
            ReadNullableString(reader, "deposit_payment_status"));

        var snapshot = new SurveyOrderReadinessSnapshot(
            ParseOrderStatus(reader.GetString(reader.GetOrdinal("status"))),
            purpose,
            reader.GetBoolean(reader.GetOrdinal("requires_baseline_mapping")),
            ReadNullableGuid(reader, "approved_boundary_id").HasValue,
            ReadNullableGuid(reader, "scope_confirmed_by").HasValue,
            ReadNullableGuid(reader, "published_base_map_id").HasValue,
            ReadNullableInt32(reader, "confirmed_survey_pole_count") is > 0,
            ReadNullableDecimal(reader, "price_per_pole_snapshot") is > 0 &&
                expectedAmount is > 0 &&
                !string.IsNullOrWhiteSpace(expectedCurrency),
            appointmentId.HasValue
                ? purpose == SurveyOperationPurpose.BaselineMapping
                    ? SurveyAppointmentPurpose.BaselineMapping
                    : SurveyAppointmentPurpose.PaidService
                : null,
            appointmentStatus,
            paymentStatus,
            ReadNullableString(reader, "manager_status") == "ACTIVE",
            ReadNullableString(reader, "manager_availability") == "AVAILABLE",
            appointmentStatus == SurveyAppointmentStatus.Confirmed &&
                appointmentEnd > evaluatedAt,
            missionPrepared,
            scheduleMatches,
            ReadNullableString(reader, "drone_status") == "AVAILABLE",
            preflightCompleted,
            reader.GetBoolean(reader.GetOrdinal("has_open_adjustment")),
            paymentMatches,
            safetySatisfied,
            reader.GetBoolean(reader.GetOrdinal("has_pending_settlement")),
            reader.GetBoolean(reader.GetOrdinal(
                "has_unapproved_extra_flight")),
            reader.GetBoolean(reader.GetOrdinal(
                "has_finalized_boundary_capture")),
            reader.GetBoolean(reader.GetOrdinal(
                "has_approved_baseline_route")),
            reader.GetBoolean(reader.GetOrdinal(
                "is_baseline_route_current")),
            reader.GetBoolean(reader.GetOrdinal(
                "is_baseline_route_within_safety_inset")),
            depositPaymentStatus);

        return Result.Success(new SurveyOrderReadinessState(
            surveyOrderId,
            missionId,
            reader.GetGuid(reader.GetOrdinal("tenant_id")),
            reader.GetGuid(reader.GetOrdinal("farm_id")),
            checked((uint)reader.GetInt64(reader.GetOrdinal("xmin"))),
            appointmentId,
            ReadNullableGuid(reader, "manager_profile_id"),
            managerUserId,
            snapshot));
    }

    private static string MapPurpose(SurveyOperationPurpose purpose) =>
        purpose switch
        {
            SurveyOperationPurpose.BaselineMapping => "BASELINE_MAPPING",
            SurveyOperationPurpose.PlantHealth => "PLANT_HEALTH",
            SurveyOperationPurpose.HarvestReadiness => "HARVEST_READINESS",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

    private static SurveyOrderStatus ParseOrderStatus(string value) =>
        Enum.Parse<SurveyOrderStatus>(ToPascalCase(value));

    private static SurveyAppointmentStatus? ParseAppointmentStatus(
        string? value) => value is null
            ? null
            : Enum.Parse<SurveyAppointmentStatus>(ToPascalCase(value));

    private static SurveyPaymentStatus? ParsePaymentStatus(string? value) =>
        value is null
            ? null
            : Enum.Parse<SurveyPaymentStatus>(ToPascalCase(value));

    private static string ToPascalCase(string value) => string.Concat(
        value.Split('_').Select(static part =>
            char.ToUpperInvariant(part[0]) +
            part[1..].ToLowerInvariant()));

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? ReadNullableString(
        NpgsqlDataReader reader,
        string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? ReadNullableTimestamp(
        NpgsqlDataReader reader,
        string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static decimal? ReadNullableDecimal(
        NpgsqlDataReader reader,
        string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static int? ReadNullableInt32(
        NpgsqlDataReader reader,
        string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
