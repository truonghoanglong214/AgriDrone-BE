using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyPaymentHandlerRegressionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReconcileDepositSupportsBothBaselineAndFollowUp(bool baseline)
    {
        var order = NewOrder(baseline);
        if (baseline) order.MarkBaselineAppointmentConfirmed(Start.AddHours(1));
        var payment = NewPayment(order, 500000m, PaymentPurpose.BaselineDeposit);
        var result = await Reconcile(order, payment, 500000m);
        Assert.True(result.IsSuccess);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        Assert.Equal(payment.Id, order.DepositPaymentId);
        Assert.Equal(baseline ? SurveyOrderStatus.BaselineReady : SurveyOrderStatus.PendingBoundaryVerification,
            order.Status);
    }

    [Theory]
    [InlineData(1500000, true)]
    [InlineData(2000000, false)]
    public async Task ReconcileFinalBalanceUsesOutstandingBalance(decimal settled, bool accepted)
    {
        var order = PricedOrder(1000, true);
        var payment = NewPayment(order, 1500000m, PaymentPurpose.FinalBalance);
        var result = await Reconcile(order, payment, settled);
        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted ? SurveyOrderStatus.ReadyForPaidService : SurveyOrderStatus.AwaitingPayment,
            order.Status);
        Assert.Equal(accepted ? SurveyPaymentStatus.Paid : SurveyPaymentStatus.Processing, payment.Status);
    }

    [Fact]
    public async Task ZeroBalanceSettlesWithoutCallingCheckout()
    {
        var order = PricedOrder(2, true);
        var handler = NewInitiationHandler(new Repository(order, null));
        var result = await handler.Handle(new InitiateSurveyPaymentCommand(order.Id, order.Version), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Amount);
        Assert.True(order.IsZeroBalanceSettled());
    }

    [Fact]
    public async Task FinalBalanceCannotBeInitiatedBeforeRequiredDepositIsPaid()
    {
        var order = PricedOrder(1000, false);
        var handler = NewInitiationHandler(new Repository(order, null));
        var result = await handler.Handle(new InitiateSurveyPaymentCommand(order.Id, order.Version), default);
        Assert.True(result.IsFailure);
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public async Task ReconciliationCannotBypassAnUnpaidRequiredDeposit()
    {
        var order = PricedOrder(1000, false);
        var payment = NewPayment(order, 2000000m, PaymentPurpose.FinalBalance);
        var result = await Reconcile(order, payment, 2000000m);
        Assert.True(result.IsFailure);
        Assert.Equal(SurveyPaymentStatus.Processing, payment.Status);
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public async Task CallbackConfirmsOutstandingBalanceAndIgnoresRepeat()
    {
        var order = PricedOrder(1000, true);
        var payment = NewPayment(order, 1500000m, PaymentPurpose.FinalBalance);
        var repository = new Repository(order, payment);
        var handler = new ProcessSurveyPaymentEventCommandHandler(
            new UnitOfWork(), repository, new Outbox(), new Context());
        using var payload = JsonDocument.Parse("{}");
        var command = new ProcessSurveyPaymentEventCommand("sepay", "REFERENCE", "EVENT-1",
            "payment.succeeded", 1500000m, "VND", Start.AddHours(9), Start.AddHours(10), payload);
        var first = await handler.Handle(command, default);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value.TransitionApplied);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
        var repeat = await handler.Handle(command, default);
        Assert.True(repeat.IsSuccess);
        Assert.True(repeat.Value.Duplicate);
    }

    [Fact]
    public async Task CancelledOrderCannotBeReactivatedByLateDepositCallback()
    {
        var order = NewOrder(false);
        order.Cancel(Start.AddHours(4));
        var payment = NewPayment(order, 500000m, PaymentPurpose.BaselineDeposit);
        var handler = new ProcessSurveyPaymentEventCommandHandler(
            new UnitOfWork(), new Repository(order, payment), new Outbox(), new Context());
        using var payload = JsonDocument.Parse("{}");
        var result = await handler.Handle(new ProcessSurveyPaymentEventCommand("sepay", "REFERENCE",
            "EVENT-LATE", "payment.succeeded", 500000m, "VND", Start.AddHours(9), Start.AddHours(10), payload), default);
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.TransitionApplied);
        Assert.Equal(SurveyOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task RepaidDepositRestoresAnAlreadyPaidFinalBalanceWithoutNewCheckout()
    {
        var order = PricedOrder(1000, true);
        var payment = NewPayment(order, 1500000m, PaymentPurpose.FinalBalance);
        payment.Confirm("REFERENCE", Start.AddHours(6));
        order.MarkPaymentConfirmed(Start.AddHours(6));
        order.RevokeBaselineDeposit(Start.AddHours(7));
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 500000m, Start.AddHours(8));
        var result = await NewInitiationHandler(new Repository(order, payment)).Handle(
            new InitiateSurveyPaymentCommand(order.Id, order.Version), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value.SurveyPaymentId);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
    }

    [Theory]
    [InlineData("CALLBACK", 0, 15000, false)]
    [InlineData("CALLBACK", 20, 11000, false)]
    [InlineData("RECONCILE", 0, 15000, false)]
    [InlineData("RECONCILE", 20, 11000, false)]
    [InlineData("ZERO", 0, 0, false)]
    [InlineData("ZERO", 20, 0, false)]
    [InlineData("CALLBACK", 20, 20600, true)]
    [InlineData("RECONCILE", 20, 20600, true)]
    [InlineData("ZERO", 20, 0, true)]
    public async Task PostFlightSettlementCompletesAndStagesUnlockOnlyOnce(string route, decimal discount, decimal balance, bool components)
    {
        var order = SurveyOrder.Create("POST-FLIGHT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            false, null, "v2.6", "TEST", Start, 5000m, discount);
        if (components)
            order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 2500m, 1000m, 1500m);
        else
            order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 1000m);
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 5000m, Start.AddHours(1));
        var map = Guid.NewGuid();
        var count = ConfirmedSurveyPoleCount.Create(route == "ZERO" ? 5 : 20);
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), map, count, Actor, Start.AddHours(2));
        order.MarkPaidAppointmentConfirmed(Start.AddHours(2));
        order.StartPaidService(Start.AddHours(3));
        order.MarkAnalysisReady(Start.AddHours(3));
        order.RecordReviewedServicePublication(Start.AddHours(4));
        if (components)
            order.ConfirmComboComponentPricing(map, route == "ZERO" ? 3 : 20, route == "ZERO" ? 1 : 8,
                SurveyPoleCountSource.CurrentPublishedInventory, "independent map and Health counts", Actor, Start.AddHours(4));
        else order.ConfirmPoleCountAndPricing(map, count, SurveyPoleCountSource.CurrentPublishedInventory,
            "Reviewed after flight", order.BookedServicePriceId!.Value, PricePerPole.Create(1000m), CurrencyCode.Vnd, Actor, Start.AddHours(4));
        Assert.Equal(discount, order.ComboDiscountPercentage);
        Assert.Equal(balance, order.RemainingAmount);
        var payment = route == "ZERO" ? null : NewPayment(order, balance, PaymentPurpose.FinalBalance);
        var repository = new Repository(order, payment);
        var outbox = new Outbox();
        if (route == "ZERO")
        {
            var result = await new InitiateSurveyPaymentCommandHandler(new UnitOfWork(), repository, outbox,
                new UnusedGateway(), new Audit(), new Context(), new Clock()).Handle(
                    new InitiateSurveyPaymentCommand(order.Id, order.Version), default);
            Assert.True(result.IsSuccess);
        }
        else if (route == "RECONCILE")
        {
            var result = await new ReconcileSurveyPaymentCommandHandler(new UnitOfWork(), repository, outbox,
                new Audit(), new Context(), new Clock()).Handle(new ReconcileSurveyPaymentCommand(payment!.Id,
                    "REFERENCE", balance, "VND", "Verified transfer", "BANK-EVIDENCE", payment.Version, order.Version), default);
            Assert.True(result.IsSuccess);
        }
        else
        {
            var handler = new ProcessSurveyPaymentEventCommandHandler(new UnitOfWork(), repository, outbox, new Context());
            using var payload = JsonDocument.Parse("{}");
            var command = new ProcessSurveyPaymentEventCommand("sepay", "REFERENCE", "POST-FLIGHT-EVENT",
                "payment.succeeded", balance, "VND", Start.AddHours(9), Start.AddHours(10), payload);
            Assert.True((await handler.Handle(command, default)).IsSuccess);
            Assert.True((await handler.Handle(command, default)).Value.Duplicate);
        }
        Assert.Equal(SurveyOrderStatus.Completed, order.Status);
        Assert.Equal(1, outbox.UnlockCalls);
    }

    private static InitiateSurveyPaymentCommandHandler NewInitiationHandler(Repository repository) =>
        new(new UnitOfWork(), repository, new Outbox(), new UnusedGateway(), new Audit(), new Context(), new Clock());

    private static Task<Result<ReconcileSurveyPaymentResponse>> Reconcile(
        SurveyOrder order, SurveyPayment payment, decimal amount) =>
        new ReconcileSurveyPaymentCommandHandler(new UnitOfWork(), new Repository(order, payment),
            new Outbox(), new Audit(), new Context(), new Clock()).Handle(
                new ReconcileSurveyPaymentCommand(payment.Id, "REFERENCE", amount, "VND",
                    "Verified transfer", "BANK-EVIDENCE", payment.Version, order.Version), default);

    private static SurveyOrder NewOrder(bool baseline) => SurveyOrder.Create(
        "REGRESSION", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        baseline, null, "v1", "Test", Start, 500000m, 20m);

    private static SurveyOrder PricedOrder(int poles, bool payDeposit)
    {
        var order = NewOrder(false);
        if (payDeposit) order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 500000m, Start.AddHours(1));
        var map = Guid.NewGuid();
        var count = ConfirmedSurveyPoleCount.Create(poles);
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), map, count, Actor, Start.AddHours(2));
        order.ConfirmPoleCountAndPricing(map, count, SurveyPoleCountSource.CurrentPublishedInventory,
            "Verified", Guid.NewGuid(), PricePerPole.Create(2500m), CurrencyCode.Vnd, Actor, Start.AddHours(3));
        order.MarkPaidAppointmentConfirmed(Start.AddHours(4));
        return order;
    }

    private static SurveyPayment NewPayment(SurveyOrder order, decimal amount, PaymentPurpose purpose)
    {
        var payment = SurveyPayment.Create(order.Id, Money.Create(amount, CurrencyCode.Vnd), "sepay",
            Start.AddHours(5), purpose);
        payment.StartProcessing("REFERENCE", Start.AddHours(5));
        return payment;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Start.AddHours(10);
    }

    private sealed class Context : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => Actor;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => default;
    }

    private sealed class UnitOfWork : ISurveyPaymentProcessingUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void AddAuditLog(AuditLog auditLog) { }
    }

    private sealed class Outbox : ISurveyPaymentProcessingOutbox
    {
        public int UnlockCalls { get; private set; }
        public Task AddPostFlightUnlockAsync(SurveyOrder order, Guid correlationId, CancellationToken cancellationToken = default)
        {
            if (order.PostFlightSettlement && order.Status == SurveyOrderStatus.Completed) UnlockCalls++;
            return Task.CompletedTask;
        }
        public void Add<T>(IntegrationEventEnvelope<T> envelope, string? partitionKey = null) { }
    }

    private sealed class UnusedGateway : ISurveyPaymentGateway
    {
        public string Provider => "sepay";
        public Task<Result<PaymentSessionResult>> CreateSessionAsync(CreatePaymentSession request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Checkout must not be called in this scenario.");
    }

    private sealed class Audit : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) { }
        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId, Guid correlationId,
            string entityType, Guid entityId, string action, JsonDocument? oldData,
            JsonDocument? newData, DateTimeOffset createdAt) { }
    }

    private sealed class Repository(SurveyOrder order, SurveyPayment? payment) : ISurveyPaymentProcessingRepository
    {
        public Task<Result<SurveyPaymentReconciliationState>> GetForReconciliationAsync(Guid paymentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(
                new SurveyPaymentReconciliationState(order, payment!)));
        public Task<Result<SurveyPaymentOwnerState>> GetForOwnerInitiationAsync(Guid surveyOrderId,
            Guid actorUserId, DateTimeOffset evaluatedAt, PaymentPurpose purpose = PaymentPurpose.FinalBalance,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(
                new SurveyPaymentOwnerState(order, Guid.NewGuid(), payment)));
        public Task<Result<IReadOnlyList<SurveyPayment>>> GetOwnerPaymentHistoryAsync(Guid surveyOrderId,
            Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<SurveyPayment>>> GetSystemAdminPaymentHistoryAsync(Guid surveyOrderId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SurveyPaymentEventState>> GetForProviderEventAsync(string provider,
            string providerReference, string deduplicationKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SurveyPaymentEventState(order, payment!, false)));
        public Task<Result<SurveyPriceAdjustmentRequestState>> GetForPriceAdjustmentRequestAsync(Guid surveyOrderId,
            Guid actorUserId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Result<SurveyPriceAdjustmentReviewState>> GetForPriceAdjustmentReviewAsync(Guid surveyOrderId,
            Guid adjustmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddPayment(SurveyPayment value) => throw new NotSupportedException();
        public void AddEvent(PaymentEvent value) { }
        public void AddPriceAdjustment(PriceAdjustment value) => throw new NotSupportedException();
    }
}
