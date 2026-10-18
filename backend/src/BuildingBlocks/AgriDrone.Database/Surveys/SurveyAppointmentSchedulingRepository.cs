using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyAppointmentSchedulingRepository(
    SurveyAppointmentSchedulingDbContext context)
    : ISurveyAppointmentSchedulingRepository
{
    public async Task<Result<SurveyAppointmentManagerState>>
        GetForManagerProposalAsync(
            Guid surveyOrderId,
            SurveyAppointmentPurpose purpose,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyAppointmentManagerState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var managerProfileId = await ReadManagerAccessAsync(
            surveyOrderId,
            actorUserId,
            evaluatedAt,
            cancellationToken);
        if (!managerProfileId.HasValue)
        {
            return Result.Failure<SurveyAppointmentManagerState>(
                SurveyAppointmentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var appointments = await context.SurveyAppointments
            .FromSqlInterpolated($$"""
                SELECT appointment.*, appointment.xmin
                FROM survey.survey_appointments AS appointment
                WHERE appointment.survey_order_id = {{surveyOrderId}}
                  AND appointment.purpose = {{purpose}}
                  AND appointment.status IN (
                    'PROPOSED'::system.survey_appointment_status,
                    'CONFIRMED'::system.survey_appointment_status,
                    'RESCHEDULE_REQUESTED'::system.survey_appointment_status)
                FOR UPDATE OF appointment
                """)
            .ToListAsync(cancellationToken);
        var latestAppointments = await context.SurveyAppointments
            .FromSqlInterpolated($$"""
                SELECT appointment.*, appointment.xmin
                FROM survey.survey_appointments AS appointment
                WHERE appointment.survey_order_id = {{surveyOrderId}}
                  AND appointment.purpose = {{purpose}}
                ORDER BY appointment.revision_number DESC,
                         appointment.created_at DESC,
                         appointment.id DESC
                LIMIT 1
                FOR UPDATE OF appointment
                """)
            .ToListAsync(cancellationToken);

        return Result.Success(new SurveyAppointmentManagerState(
            order,
            appointments.SingleOrDefault(),
            latestAppointments.SingleOrDefault(),
            managerProfileId.Value));
    }

    public async Task<Result<SurveyAppointmentOwnerState>>
        GetForOwnerMutationAsync(
            Guid surveyOrderId,
            Guid appointmentId,
            Guid actorUserId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyAppointmentOwnerState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasOwnerAccessAsync(
                surveyOrderId,
                actorUserId,
                cancellationToken))
        {
            return Result.Failure<SurveyAppointmentOwnerState>(
                SurveyAppointmentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var appointments = await context.SurveyAppointments
            .FromSqlInterpolated($$"""
                SELECT appointment.*, appointment.xmin
                FROM survey.survey_appointments AS appointment
                WHERE appointment.id = {{appointmentId}}
                  AND appointment.survey_order_id = {{surveyOrderId}}
                FOR UPDATE OF appointment
                """)
            .ToListAsync(cancellationToken);
        var appointment = appointments.SingleOrDefault();
        return appointment is null
            ? Result.Failure<SurveyAppointmentOwnerState>(
                SurveyAppointmentError.NotFound(appointmentId))
            : Result.Success(new SurveyAppointmentOwnerState(
                order,
                appointment,
                await HasActivePaymentAsync(
                    surveyOrderId,
                    cancellationToken)));
    }

    public void Add(SurveyAppointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        context.EnsureTransaction();
        context.SurveyAppointments.Add(appointment);
    }

    public async Task InvalidateBaselineFieldWorkflowAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE survey.boundary_capture_sessions
            SET status = 'Superseded'
            WHERE survey_order_id = {{surveyOrderId}}
              AND status IN ('InProgress', 'Finalized');

            UPDATE survey.baseline_route_plan_references
            SET status = 'Superseded'
            WHERE survey_order_id = {{surveyOrderId}}
              AND status IN ('Generated', 'Approved');
            """, cancellationToken);
    }

    private Task<bool> LockOrderAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT survey_order.id
            FROM survey.survey_orders AS survey_order
            WHERE survey_order.id = @order_id
            FOR UPDATE OF survey_order
            """,
            cancellationToken,
            Id("order_id", surveyOrderId));

    private async Task<Guid?> ReadManagerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT profile.id
            FROM survey.survey_orders AS survey_order
            JOIN farm.farms AS farm
              ON farm.id = survey_order.farm_id
             AND farm.tenant_id = survey_order.tenant_id
            JOIN identity.farm_manager_assignments AS assignment
              ON assignment.farm_id = survey_order.farm_id
             AND assignment.tenant_id = survey_order.tenant_id
             AND assignment.ended_at IS NULL
            JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
            JOIN identity.users AS app_user ON app_user.id = profile.user_id
            WHERE survey_order.id = @order_id
              AND farm.deleted_at IS NULL
              AND farm.status = 'ACTIVE'::system.general_status
              AND app_user.id = @actor_id
              AND app_user.status = 'ACTIVE'::system.user_status
              AND profile.status = 'ACTIVE'::system.system_manager_profile_status
              AND profile.availability = 'AVAILABLE'::system.system_manager_availability_status
              AND profile.qualification_status = 'QUALIFIED'::system.flight_qualification_status
              AND profile.qualification_expires_at > @evaluated_at
            FOR SHARE OF farm, assignment, profile, app_user
            """,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private Task<bool> HasOwnerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT membership.id
            FROM survey.survey_orders AS survey_order
            JOIN identity.tenant_memberships AS membership
              ON membership.tenant_id = survey_order.tenant_id
             AND membership.user_id = @actor_id
             AND membership.role = 'OWNER'::system.tenant_member_role
             AND membership.status = 'ACTIVE'::system.general_status
            JOIN identity.users AS app_user
              ON app_user.id = membership.user_id
             AND app_user.status = 'ACTIVE'::system.user_status
            WHERE survey_order.id = @order_id
            FOR SHARE OF membership, app_user
            """,
            cancellationToken,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId));

    private Task<bool> HasActivePaymentAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT payment.id
            FROM survey.survey_payments AS payment
            WHERE payment.survey_order_id = @order_id
              AND payment.status IN (
                'PENDING'::system.survey_payment_status,
                'PROCESSING'::system.survey_payment_status,
                'CONFIRMED'::system.survey_payment_status,
                'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
            FOR SHARE OF payment
            """,
            cancellationToken,
            Id("order_id", surveyOrderId));

    private async Task<bool> ExistsAsync(
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private DbCommand CreateCommand(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        context.EnsureTransaction();
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction!
            .GetDbTransaction();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static NpgsqlParameter Id(string name, Guid value) =>
        new(name, value);
}
