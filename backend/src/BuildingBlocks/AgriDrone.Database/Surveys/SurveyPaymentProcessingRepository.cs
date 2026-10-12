using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyPaymentProcessingRepository(
    SurveyPaymentProcessingDbContext context)
    : ISurveyPaymentProcessingRepository
{
    public async Task<Result<SurveyPaymentOwnerState>>
        GetForOwnerInitiationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasOwnerAccessAsync(
                surveyOrderId,
                actorUserId,
                cancellationToken))
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                PaymentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var appointmentId = await ReadConfirmedPaidAppointmentAsync(
            surveyOrderId,
            evaluatedAt,
            cancellationToken);
        if (!appointmentId.HasValue)
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                PaymentError.OrderNotReady());
        }

        var activePayments = await context.SurveyPayments
            .FromSqlInterpolated($$"""
                SELECT *
                FROM survey.survey_payments
                WHERE survey_order_id = {{surveyOrderId}}
                  AND status IN (
                    'PENDING'::system.survey_payment_status,
                    'PROCESSING'::system.survey_payment_status,
                    'CONFIRMED'::system.survey_payment_status,
                    'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        return Result.Success(new SurveyPaymentOwnerState(
            order,
            appointmentId.Value,
            activePayments.SingleOrDefault()));
    }

    public async Task<Result<SurveyPaymentEventState>>
        GetForProviderEventAsync(
            string provider,
            string providerReference,
            string deduplicationKey,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        Guid paymentId;
        Guid orderId;
        await using (var command = CreateCommand(
            """
            SELECT payment.id, survey_order.id
            FROM survey.survey_payments AS payment
            JOIN survey.survey_orders AS survey_order
              ON survey_order.id = payment.survey_order_id
            WHERE payment.provider = @provider
              AND payment.provider_reference = @provider_reference
            FOR UPDATE OF payment, survey_order
            """,
            Text("provider", provider),
            Text("provider_reference", providerReference)))
        await using (var reader = await command.ExecuteReaderAsync(
            cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Result.Failure<SurveyPaymentEventState>(
                    PaymentError.NotFound(providerReference));
            }

            paymentId = reader.GetGuid(0);
            orderId = reader.GetGuid(1);
        }

        var payment = await context.SurveyPayments.SingleAsync(
            item => item.Id == paymentId,
            cancellationToken);
        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == orderId,
            cancellationToken);
        var duplicate = await context.PaymentEvents.AsNoTracking().AnyAsync(
            item => item.Provider == provider &&
                    item.DeduplicationKey == deduplicationKey,
            cancellationToken);

        return Result.Success(new SurveyPaymentEventState(
            order,
            payment,
            duplicate));
    }

    public void AddPayment(SurveyPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        context.EnsureTransaction();
        context.SurveyPayments.Add(payment);
    }

    public void AddEvent(PaymentEvent paymentEvent)
    {
        ArgumentNullException.ThrowIfNull(paymentEvent);
        context.EnsureTransaction();
        context.PaymentEvents.Add(paymentEvent);
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

    private async Task<Guid?> ReadConfirmedPaidAppointmentAsync(
        Guid surveyOrderId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT appointment.id
            FROM survey.survey_appointments AS appointment
            WHERE appointment.survey_order_id = @order_id
              AND appointment.purpose = 'PAID_SERVICE'::system.survey_appointment_purpose
              AND appointment.status = 'CONFIRMED'::system.survey_appointment_status
              AND appointment.proposed_end_at > @evaluated_at
            FOR SHARE OF appointment
            """,
            Id("order_id", surveyOrderId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

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

    private static NpgsqlParameter Text(string name, string value) =>
        new(name, value);
}
