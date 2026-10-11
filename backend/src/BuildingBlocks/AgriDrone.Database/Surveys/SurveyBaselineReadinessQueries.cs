using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyBaselineReadinessQueries(NpgsqlDataSource dataSource)
    : ISurveyBaselineReadinessQueries
{
    public async Task<Result<SurveyBaselineReadinessState>> GetAsync(
        Guid surveyOrderId,
        Guid missionId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
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
                boundary.id AS approved_boundary_id,
                appointment.id AS appointment_id,
                appointment.status::text AS appointment_status,
                appointment.proposed_start_at,
                appointment.proposed_end_at,
                profile.id AS manager_profile_id,
                profile.user_id AS manager_user_id,
                profile.status::text AS manager_status,
                profile.availability::text AS manager_availability,
                profile.qualification_status::text AS qualification_status,
                profile.qualification_expires_at,
                mission.id AS mission_id,
                mission.mission_purpose::text AS mission_purpose,
                mission.status::text AS mission_status,
                mission.scheduled_at,
                mission.scheduled_end_at,
                mission.pilot_user_id,
                drone.status::text AS drone_status,
                preflight.id AS preflight_id,
                preflight.unsuitable_condition_notes,
                preflight.failsafe_notes
            FROM survey.survey_orders AS survey_order
            LEFT JOIN farm.farm_boundaries AS boundary
              ON boundary.id = survey_order.farm_boundary_version_id
             AND boundary.tenant_id = survey_order.tenant_id
             AND boundary.farm_id = survey_order.farm_id
             AND boundary.status = 'APPROVED'::system.farm_boundary_status
            LEFT JOIN LATERAL (
                SELECT item.*
                FROM survey.survey_appointments AS item
                WHERE item.survey_order_id = survey_order.id
                  AND item.purpose = 'BASELINE_MAPPING'::system.survey_appointment_purpose
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
                  AND checklist.status = 'COMPLETED'::system.mission_preflight_checklist_status
                LIMIT 1
            ) AS preflight ON TRUE
            WHERE survey_order.id = @order_id
            """;
        command.Parameters.AddWithValue("order_id", surveyOrderId);
        command.Parameters.AddWithValue("mission_id", missionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Result.Failure<SurveyBaselineReadinessState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var managerUserId = ReadNullableGuid(reader, "manager_user_id");
        if (managerUserId != actorUserId)
        {
            return Result.Failure<SurveyBaselineReadinessState>(
                SurveyAppointmentError.Forbidden());
        }

        var tenantId = reader.GetGuid(reader.GetOrdinal("tenant_id"));
        var farmId = reader.GetGuid(reader.GetOrdinal("farm_id"));
        var orderStatus = ParseOrderStatus(
            reader.GetString(reader.GetOrdinal("status")));
        var orderVersion = checked((uint)reader.GetInt64(
            reader.GetOrdinal("xmin")));
        var requiresBaseline = reader.GetBoolean(
            reader.GetOrdinal("requires_baseline_mapping"));
        var appointmentId = ReadNullableGuid(reader, "appointment_id");
        SurveyAppointmentStatus? appointmentStatus = appointmentId.HasValue
            ? ParseAppointmentStatus(
                ReadNullableString(reader, "appointment_status")!)
            : null;
        var appointmentStart = ReadNullableTimestamp(
            reader,
            "proposed_start_at");
        var appointmentEnd = ReadNullableTimestamp(
            reader,
            "proposed_end_at");
        var missionPurpose = ReadNullableString(reader, "mission_purpose");
        var missionStatus = ReadNullableString(reader, "mission_status");
        var scheduledAt = ReadNullableTimestamp(reader, "scheduled_at");
        var scheduledEndAt = ReadNullableTimestamp(
            reader,
            "scheduled_end_at");
        var managerReady =
            ReadNullableString(reader, "manager_status") == "ACTIVE" &&
            ReadNullableString(reader, "qualification_status") ==
                "QUALIFIED" &&
            ReadNullableTimestamp(reader, "qualification_expires_at") >
                evaluatedAt;
        var managerAvailable = ReadNullableString(
            reader,
            "manager_availability") == "AVAILABLE";
        var missionPrepared =
            ReadNullableGuid(reader, "mission_id").HasValue &&
            missionPurpose == "BASELINE_MAPPING" &&
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

        var snapshot = new SurveyOrderReadinessSnapshot(
            orderStatus,
            SurveyOperationPurpose.BaselineMapping,
            requiresBaseline,
            ReadNullableGuid(reader, "approved_boundary_id").HasValue,
            ReadNullableGuid(reader, "scope_confirmed_by").HasValue,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: false,
            HasPriceSnapshot: false,
            appointmentId.HasValue
                ? SurveyAppointmentPurpose.BaselineMapping
                : null,
            appointmentStatus,
            PaymentStatus: null,
            managerReady,
            managerAvailable,
            appointmentStatus == SurveyAppointmentStatus.Confirmed &&
                appointmentEnd > evaluatedAt,
            missionPrepared,
            scheduleMatches,
            ReadNullableString(reader, "drone_status") == "AVAILABLE",
            preflightCompleted,
            HasPendingPriceAdjustment: false,
            safetySatisfied);

        return Result.Success(new SurveyBaselineReadinessState(
            surveyOrderId,
            tenantId,
            farmId,
            orderVersion,
            appointmentId,
            ReadNullableGuid(reader, "manager_profile_id"),
            snapshot));
    }

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

    private static SurveyOrderStatus ParseOrderStatus(string value) =>
        value switch
        {
            "BASELINE_READY" => SurveyOrderStatus.BaselineReady,
            "AWAITING_BASELINE_APPOINTMENT" =>
                SurveyOrderStatus.AwaitingBaselineAppointment,
            _ => Enum.Parse<SurveyOrderStatus>(
                string.Concat(value.Split('_').Select(static part =>
                    char.ToUpperInvariant(part[0]) +
                    part[1..].ToLowerInvariant())))
        };

    private static SurveyAppointmentStatus ParseAppointmentStatus(
        string value) => value switch
        {
            "PROPOSED" => SurveyAppointmentStatus.Proposed,
            "CONFIRMED" => SurveyAppointmentStatus.Confirmed,
            "RESCHEDULE_REQUESTED" =>
                SurveyAppointmentStatus.RescheduleRequested,
            "CANCELLED" => SurveyAppointmentStatus.Cancelled,
            "SUPERSEDED" => SurveyAppointmentStatus.Superseded,
            _ => throw new InvalidOperationException(
                $"Unknown appointment status '{value}'.")
        };
}
