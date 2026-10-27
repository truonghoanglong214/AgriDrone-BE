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
    public async Task<Result<IReadOnlyList<SurveyPayment>>>
        GetOwnerPaymentHistoryAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasOwnerAccessAsync(
                surveyOrderId,
                actorUserId,
                cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                PaymentError.Forbidden());
        }

        IReadOnlyList<SurveyPayment> payments =
            await ReadPaymentHistoryAsync(surveyOrderId, cancellationToken);
        return Result.Success(payments);
    }

    public async Task<Result<IReadOnlyList<SurveyPayment>>>
        GetSystemAdminPaymentHistoryAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        IReadOnlyList<SurveyPayment> payments =
            await ReadPaymentHistoryAsync(surveyOrderId, cancellationToken);
        return Result.Success(payments);
    }

    private Task<List<SurveyPayment>> ReadPaymentHistoryAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => context.SurveyPayments
        .AsNoTracking()
        .Include(payment => payment.Events)
        .Where(payment => payment.SurveyOrderId == surveyOrderId)
        .OrderByDescending(payment => payment.CreatedAt)
        .ToListAsync(cancellationToken);

    public async Task<Result<SurveyPaymentOwnerState>>
        GetForOwnerInitiationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            PaymentPurpose purpose = PaymentPurpose.FinalBalance,
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
        var appointmentId = await ReadConfirmedAppointmentForPurposeAsync(
            surveyOrderId,
            purpose,
            evaluatedAt,
            cancellationToken);
        if (!appointmentId.HasValue)
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                PaymentError.OrderNotReady());
        }

        var purposeString = purpose.ToString();
        var activePayments = await context.SurveyPayments
            .FromSqlInterpolated($$"""
                SELECT payment.*, payment.xmin
                FROM survey.survey_payments AS payment
                WHERE payment.survey_order_id = {{surveyOrderId}}
                  AND payment.purpose = {{purposeString}}
                  AND payment.status IN (
                    'PENDING'::system.survey_payment_status,
                    'PROCESSING'::system.survey_payment_status,
                    'PAID'::system.survey_payment_status,
                    'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                FOR UPDATE OF payment
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

    public async Task<Result<SurveyPriceAdjustmentRequestState>>
        GetForPriceAdjustmentRequestAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasAssignedManagerAccessAsync(
                surveyOrderId,
                actorUserId,
                evaluatedAt,
                cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                PriceAdjustmentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var openAdjustments = await context.PriceAdjustments
            .FromSqlInterpolated($$"""
                SELECT adjustment.*, adjustment.xmin
                FROM survey.price_adjustments AS adjustment
                WHERE adjustment.survey_order_id = {{surveyOrderId}}
                  AND adjustment.status IN (
                    'PENDING'::system.price_adjustment_status,
                    'APPROVED'::system.price_adjustment_status)
                FOR UPDATE OF adjustment
                """)
            .ToListAsync(cancellationToken);
        var effectivePrices = await ReadEffectivePricesAsync(
            order.SurveyServiceId,
            evaluatedAt,
            cancellationToken);
        if (effectivePrices.Count != 1)
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                PriceAdjustmentError.EffectivePriceUnavailable());
        }

        var activePayment = await ReadLatestPaymentAsync(
            surveyOrderId,
            PaymentPurpose.FinalBalance,
            cancellationToken);
        return Result.Success(new SurveyPriceAdjustmentRequestState(
            order,
            effectivePrices[0],
            openAdjustments.SingleOrDefault(),
            activePayment));
    }

    public async Task<Result<SurveyPriceAdjustmentReviewState>>
        GetForPriceAdjustmentReviewAsync(
            Guid surveyOrderId,
            Guid adjustmentId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentReviewState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await ExistsAsync(
                """
                SELECT adjustment.id
                FROM survey.price_adjustments AS adjustment
                WHERE adjustment.id = @adjustment_id
                  AND adjustment.survey_order_id = @order_id
                FOR UPDATE OF adjustment
                """,
                cancellationToken,
                Id("adjustment_id", adjustmentId),
                Id("order_id", surveyOrderId)))
        {
            return Result.Failure<SurveyPriceAdjustmentReviewState>(
                PriceAdjustmentError.NotFound(adjustmentId));
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var adjustment = await context.PriceAdjustments.SingleAsync(
            item => item.Id == adjustmentId,
            cancellationToken);
        var activePayment = await ReadLatestPaymentAsync(
            surveyOrderId,
            PaymentPurpose.FinalBalance,
            cancellationToken);
        return Result.Success(new SurveyPriceAdjustmentReviewState(
            order,
            adjustment,
            activePayment));
    }

    public async Task<Result<SurveyPaymentReconciliationState>>
        GetForReconciliationAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        Guid orderId;
        await using (var command = CreateCommand(
            """
            SELECT payment.survey_order_id
            FROM survey.survey_payments AS payment
            JOIN survey.survey_orders AS survey_order
              ON survey_order.id = payment.survey_order_id
            WHERE payment.id = @payment_id
            FOR UPDATE OF payment, survey_order
            """,
            Id("payment_id", paymentId)))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid id)
            {
                return Result.Failure<SurveyPaymentReconciliationState>(
                    PaymentError.NotFound(paymentId.ToString("D")));
            }

            orderId = id;
        }

        var payment = await context.SurveyPayments.SingleAsync(
            item => item.Id == paymentId,
            cancellationToken);
        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == orderId,
            cancellationToken);
        return Result.Success(new SurveyPaymentReconciliationState(
            order,
            payment));
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

    public void AddPriceAdjustment(PriceAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        context.EnsureTransaction();
        context.PriceAdjustments.Add(adjustment);
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

    private Task<bool> HasAssignedManagerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT profile.id
            FROM survey.survey_orders AS survey_order
            JOIN identity.farm_manager_assignments AS assignment
              ON assignment.tenant_id = survey_order.tenant_id
             AND assignment.farm_id = survey_order.farm_id
             AND assignment.ended_at IS NULL
            JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
             AND profile.user_id = @actor_id
             AND profile.status = 'ACTIVE'::system.system_manager_profile_status
             AND profile.qualification_status = 'QUALIFIED'::system.flight_qualification_status
             AND profile.qualification_expires_at > @evaluated_at
            WHERE survey_order.id = @order_id
            FOR SHARE OF assignment, profile
            """,
            cancellationToken,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));

    private async Task<List<EffectiveSurveyPrice>> ReadEffectivePricesAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var prices = new List<EffectiveSurveyPrice>();
        await using var command = CreateCommand(
            """
            SELECT price.id, price.price_per_pole, price.currency
            FROM survey.survey_service_prices AS price
            WHERE price.survey_service_id = @service_id
              AND price.price_per_pole IS NOT NULL
              AND price.currency = 'VND'
              AND price.effective_from <= @evaluated_at
              AND (price.effective_to IS NULL OR price.effective_to > @evaluated_at)
            ORDER BY price.effective_from DESC
            LIMIT 2
            FOR SHARE OF price
            """,
            Id("service_id", surveyServiceId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            prices.Add(new EffectiveSurveyPrice(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                reader.GetString(2).Trim()));
        }

        return prices;
    }

    private async Task<SurveyPayment?> ReadLatestPaymentAsync(
        Guid surveyOrderId,
        PaymentPurpose purpose,
        CancellationToken cancellationToken)
    {
        var paymentIds = new List<Guid>();
        await using (var command = CreateCommand(
            """
            SELECT payment.id
            FROM survey.survey_payments AS payment
            WHERE payment.survey_order_id = @order_id
              AND payment.purpose = @purpose
            ORDER BY payment.created_at DESC
            LIMIT 1
            FOR UPDATE OF payment
            """,
            Id("order_id", surveyOrderId),
            Text("purpose", purpose.ToString())))
        await using (var reader = await command.ExecuteReaderAsync(
            cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                paymentIds.Add(reader.GetGuid(0));
            }
        }

        if (paymentIds.Count == 0)
        {
            return null;
        }

        var paymentId = paymentIds[0];
        return await context.SurveyPayments.SingleAsync(
            item => item.Id == paymentId,
            cancellationToken);
    }

    private async Task<Guid?> ReadConfirmedAppointmentForPurposeAsync(
        Guid surveyOrderId,
        PaymentPurpose purpose,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var purposeSql = purpose == PaymentPurpose.BaselineDeposit
            ? "'BASELINE_MAPPING'::system.survey_appointment_purpose"
            : "'PAID_SERVICE'::system.survey_appointment_purpose";

        await using var command = CreateCommand(
            $"""
            SELECT appointment.id
            FROM survey.survey_appointments AS appointment
            WHERE appointment.survey_order_id = @order_id
              AND appointment.purpose = {purposeSql}
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
