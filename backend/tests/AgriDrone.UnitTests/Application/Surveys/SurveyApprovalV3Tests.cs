using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyApprovalV3Tests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewCustomerV3ApprovalProvisionsTenantFarmOrderCommercialSnapshotAndStateHistory()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var response = result.Value;

        Assert.Equal(request.Id, response.SurveyRequestId);
        Assert.Equal(SurveyRequestStatus.Approved, response.RequestStatus);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, response.OrderStatus);
        Assert.NotNull(response.PrimaryManagerAssignmentId);
        Assert.NotNull(response.OwnerInvitationId);

        // Verify Order
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.V3, order.Scope);
        Assert.Equal(harness.Snapshot.BusinessPolicyVersionId, order.BusinessPolicyVersionId);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
        Assert.Equal(harness.Snapshot.DepositAmount, order.DepositAmount);

        // Verify Commercial Snapshot
        var commercialSnapshot = harness.OrderRepository.SavedCommercialSnapshot;
        Assert.NotNull(commercialSnapshot);
        Assert.Equal(order.Id, commercialSnapshot.SurveyOrderId);
        Assert.Equal(harness.Snapshot.PolicyFingerprint, commercialSnapshot.PolicyFingerprint);
        Assert.Equal(harness.Snapshot.PricePerPole, commercialSnapshot.PricePerPole);
        Assert.Equal(harness.Snapshot.DepositAmount, commercialSnapshot.DepositAmount);
        Assert.Equal(harness.Snapshot.ClaimWindowDays, commercialSnapshot.ClaimWindowDays);
        Assert.Equal(harness.Snapshot.AllowedFreeNoShowReschedules, commercialSnapshot.AllowedFreeNoShowReschedules);

        // Verify Order State History
        var history = harness.OrderRepository.SavedStateHistory;
        Assert.NotNull(history);
        Assert.Equal(order.Id, history.SurveyOrderId);
        Assert.Null(history.FromStatus);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, history.ToStatus);
        Assert.Equal(harness.AdminActorId, history.ActorId);
        Assert.Equal("APPROVAL", history.Source);

        // Verify Staging
        Assert.NotNull(harness.ProvisioningPort.StagedTenant);
        Assert.NotNull(harness.ProvisioningPort.StagedFarm);
        Assert.NotNull(harness.ProvisioningPort.StagedAssignment);
        Assert.NotNull(harness.ProvisioningPort.StagedInvitation);

        // Verify Outbox & Audit
        Assert.Single(harness.Outbox.Envelopes);
        Assert.True(harness.UnitOfWork.Flushed);
    }

    [Fact]
    public async Task ExistingTenantNewFarmV3ApprovalProvisionsFarmAndOrderWithoutDuplicateTenantOrInvitation()
    {
        var harness = new ApprovalTestHarness();
        var tenantId = Guid.NewGuid();
        var requestedByUserId = Guid.NewGuid();
        var request = harness.CreateExistingTenantNewFarmRequest(ContractScope.V3, tenantId, requestedByUserId);
        harness.RequestRepository.Request = request;

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var response = result.Value;

        Assert.Equal(tenantId, response.TenantId);
        Assert.Null(response.OwnerInvitationId);

        // Verify Staging: No duplicate tenant or invitation
        Assert.Null(harness.ProvisioningPort.StagedTenant);
        Assert.NotNull(harness.ProvisioningPort.StagedFarm);
        Assert.NotNull(harness.ProvisioningPort.StagedAssignment);
        Assert.Null(harness.ProvisioningPort.StagedInvitation);

        // Verify Order & Commercial Snapshot
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.V3, order.Scope);
        Assert.NotNull(harness.OrderRepository.SavedCommercialSnapshot);
        Assert.NotNull(harness.OrderRepository.SavedStateHistory);
    }

    [Fact]
    public async Task ExistingFarmFollowUpV3ApprovalCreatesOrderWithoutNewTenantOrFarm()
    {
        var harness = new ApprovalTestHarness();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestedByUserId = Guid.NewGuid();
        var request = harness.CreateExistingFarmFollowUpRequest(ContractScope.V3, tenantId, farmId, requestedByUserId);
        harness.RequestRepository.Request = request;

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var response = result.Value;

        Assert.Equal(tenantId, response.TenantId);
        Assert.Equal(farmId, response.FarmId);
        Assert.Null(response.OwnerInvitationId);

        // Staging checks: No tenant or farm staged!
        Assert.Null(harness.ProvisioningPort.StagedTenant);
        Assert.Null(harness.ProvisioningPort.StagedFarm);
        Assert.Null(harness.ProvisioningPort.StagedInvitation);

        // Order checks: Unpaid initial deposit, not artificially paid
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.V3, order.Scope);
        Assert.False(order.RequiresBaselineMapping);
        Assert.NotNull(order.FarmBaseMapVersionId);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
        Assert.Equal(harness.Snapshot.DepositAmount, order.DepositAmount);

        Assert.NotNull(harness.OrderRepository.SavedCommercialSnapshot);
        Assert.NotNull(harness.OrderRepository.SavedStateHistory);
    }

    [Fact]
    public async Task ExistingFarmLegacyApprovalWithoutBaselineSucceedsAndRequiresBaselineMapping()
    {
        var harness = new ApprovalTestHarness();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestedByUserId = Guid.NewGuid();
        var request = harness.CreateExistingFarmFollowUpRequest(ContractScope.Legacy, tenantId, farmId, requestedByUserId);
        harness.RequestRepository.Request = request;

        harness.PrerequisiteResolver.BaselineToReturn = new SurveyApprovalFarmBaselineReference(
            tenantId,
            farmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.Legacy, order.Scope);
        Assert.True(order.RequiresBaselineMapping);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
    }

    [Fact]
    public async Task ExistingFarmV3ApprovalWithoutBaselineFailsWithRequestContextInvalid()
    {
        var harness = new ApprovalTestHarness();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestedByUserId = Guid.NewGuid();
        var request = harness.CreateExistingFarmFollowUpRequest(ContractScope.V3, tenantId, farmId, requestedByUserId);
        harness.RequestRepository.Request = request;

        harness.PrerequisiteResolver.BaselineToReturn = new SurveyApprovalFarmBaselineReference(
            tenantId,
            farmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyApprovalError.RequestContextInvalidCode, result.Error.Code);
        Assert.Null(harness.OrderRepository.SavedOrder);
    }

    [Fact]
    public async Task V3ApprovalWhenCommercialSnapshotResolutionFailsReturnsErrorWithoutProvisioning()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        harness.SnapshotResolver.ResultToReturn = Result.Failure<ResolvedSurveyCommercialSnapshot>(
            SurveyApprovalError.ServiceUnavailable());

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyApprovalError.ServiceUnavailableCode, result.Error.Code);

        Assert.Null(harness.OrderRepository.SavedOrder);
        Assert.Null(harness.OrderRepository.SavedCommercialSnapshot);
        Assert.Null(harness.OrderRepository.SavedStateHistory);
        Assert.Null(harness.ProvisioningPort.StagedTenant);
        Assert.Null(harness.ProvisioningPort.StagedFarm);
    }

    [Fact]
    public async Task V3ApprovalWhenMissingAdminContextFailsWithUnauthorized()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        harness.ExecutionContext.ActorId = null;

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyApprovalError.CurrentSystemAdminRequiredCode, result.Error.Code);
    }

    [Fact]
    public async Task V3ApprovalWhenChecklistIncompleteFailsWithValidation()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        var incompleteChecklist = new SurveyRequestReviewChecklistInput(
            SurveyRequestReviewChecklistDefinition.Version,
            new Dictionary<string, bool>
            {
                [SurveyRequestReviewChecklistDefinition.ContactValidCode] = false, // Incomplete!
                [SurveyRequestReviewChecklistDefinition.DragonFruitFarmConfirmedCode] = true,
                [SurveyRequestReviewChecklistDefinition.ServiceAreaSupportedCode] = true,
                [SurveyRequestReviewChecklistDefinition.CandidateLocationSufficientCode] = true
            },
            Notes: null);

        var command = new ApproveSurveyRequestCommand(
            request.Id,
            Guid.NewGuid(),
            "Approval reason notes",
            incompleteChecklist,
            request.Version,
            "idemp_test_incomplete");

        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyApprovalError.InvalidChecklistCode, result.Error.Code);
    }

    [Fact]
    public async Task V3ApprovalIdempotentReplayReturnsSavedResponseWhenSamePayload()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        var command = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var firstResult = await harness.Handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Replay with exact same command
        var secondResult = await harness.Handler.Handle(command, CancellationToken.None);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Value.SurveyOrderId, secondResult.Value.SurveyOrderId);
        Assert.Equal(firstResult.Value.OrderNumber, secondResult.Value.OrderNumber);
    }

    [Fact]
    public async Task V3ApprovalIdempotencyConflictWhenPayloadDiffersForSameKey()
    {
        var harness = new ApprovalTestHarness();
        var request = harness.CreateNewCustomerRequest(ContractScope.V3);
        harness.RequestRepository.Request = request;

        var command1 = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version, idempotencyKey: "shared_idemp_key", reason: "Reason A");
        var firstResult = await harness.Handler.Handle(command1, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Try same idempotency key with different reason (different fingerprint)
        var command2 = ApprovalTestHarness.CreateValidCommand(request.Id, request.Version, idempotencyKey: "shared_idemp_key", reason: "Reason B (different)");
        var secondResult = await harness.Handler.Handle(command2, CancellationToken.None);

        Assert.True(secondResult.IsFailure);
        Assert.Equal(SurveyApprovalError.IdempotencyConflictCode, secondResult.Error.Code);
    }

    private sealed class ApprovalTestHarness
    {
        public Guid AdminActorId { get; } = Guid.NewGuid();
        public TestSurveyApprovalUnitOfWork UnitOfWork { get; } = new();
        public TestSurveyApprovalRequestRepository RequestRepository { get; } = new();
        public TestSurveyApprovalIdempotencyRepository IdempotencyRepository { get; } = new();
        public TestSurveyApprovalPrerequisiteResolver PrerequisiteResolver { get; } = new();
        public TestSurveyCommercialSnapshotResolver SnapshotResolver { get; } = new();
        public TestSurveyOrderRepository OrderRepository { get; } = new();
        public TestSurveyApprovalProvisioningPort ProvisioningPort { get; } = new();
        public TestSurveyApprovalOutbox Outbox { get; } = new();
        public TestAuditWriter AuditWriter { get; } = new();
        public TestExecutionContext ExecutionContext { get; } = new();
        public TestTimeProvider TimeProvider { get; } = new(T0);
        public TestNumberGenerator NumberGenerator { get; } = new();
        public ResolvedSurveyCommercialSnapshot Snapshot { get; }
        public ApproveSurveyRequestCommandHandler Handler { get; }

        public ApprovalTestHarness()
        {
            ExecutionContext.ActorId = AdminActorId;
            ExecutionContext.CorrelationId = Guid.NewGuid();

            Snapshot = new ResolvedSurveyCommercialSnapshot(
                BusinessPolicyVersionId: Guid.NewGuid(),
                PolicyFingerprint: "fingerprint_1234567890abcdef",
                SurveyServiceRevisionId: Guid.NewGuid(),
                SurveyServiceRevisionVersion: 1,
                ServiceCode: "MAP",
                MapsPoles: true,
                ScreensDisease: false,
                PriceVersionId: Guid.NewGuid(),
                PriceVersionNumber: 1,
                PricePerPole: 5000m,
                Currency: "VND",
                DepositPolicyId: Guid.NewGuid(),
                DepositVersionNumber: 1,
                DepositAmount: 100000m,
                ClaimPolicyId: Guid.NewGuid(),
                ClaimPolicyVersionNumber: 1,
                ClaimWindowDays: 7,
                ClaimTimeZoneId: "Asia/Ho_Chi_Minh",
                ExcludeNoticeDate: true,
                CancellationPolicyId: Guid.NewGuid(),
                CancellationPolicyVersionNumber: 1,
                RefundableBeforeCapture: true,
                RetainDepositAfterCapture: true,
                AllowedFreeNoShowReschedules: 1,
                EvaluatedAt: T0);

            SnapshotResolver.Snapshot = Snapshot;

            var orchestrators = new ISurveyRequestKindApprovalOrchestrator[]
            {
                new NewCustomerApprovalOrchestrator(ProvisioningPort, NumberGenerator, OrderRepository),
                new ExistingTenantNewFarmApprovalOrchestrator(ProvisioningPort, NumberGenerator, OrderRepository),
                new ExistingFarmSurveyApprovalOrchestrator(NumberGenerator, OrderRepository)
            };

            Handler = new ApproveSurveyRequestCommandHandler(
                UnitOfWork,
                RequestRepository,
                IdempotencyRepository,
                PrerequisiteResolver,
                SnapshotResolver,
                orchestrators,
                Outbox,
                AuditWriter,
                ExecutionContext,
                TimeProvider,
                NullLogger<ApproveSurveyRequestCommandHandler>.Instance);
        }

        public SurveyRequest CreateNewCustomerRequest(ContractScope scope)
        {
            var now = TimeProvider.GetUtcNow();
            var createdAt = now.AddHours(-2);
            var service = scope == ContractScope.V3
                ? SurveyService.CreateV3("MAP", "Mapping Baseline", "Description", createdAt)
                : SurveyService.Create("PLANT_HEALTH", "Plant Health", "Description", SurveyServiceType.PlantHealth, createdAt);

            var point = new Point(106.660172, 10.762622) { SRID = 4326 };
            var idempotency = RequestIdempotency.Create("test-scope", "idemp_new_customer");
            var request = SurveyRequest.CreateNewCustomer(
                "REQ-2026-0001",
                service,
                idempotency,
                "AGRI",
                "Agri Tenant",
                "John Doe",
                "john@example.com",
                "0901234567",
                "Green Farm",
                "123 Agri Road",
                15.5m,
                point,
                100,
                null,
                null,
                null,
                createdAt);

            request.StartReview(now.AddHours(-1));
            return request;
        }

        public SurveyRequest CreateExistingTenantNewFarmRequest(ContractScope scope, Guid tenantId, Guid requestedByUserId)
        {
            var now = TimeProvider.GetUtcNow();
            var createdAt = now.AddHours(-2);
            var service = scope == ContractScope.V3
                ? SurveyService.CreateV3("MAP", "Mapping Baseline", "Description", createdAt)
                : SurveyService.Create("PLANT_HEALTH", "Plant Health", "Description", SurveyServiceType.PlantHealth, createdAt);

            var point = new Point(106.660172, 10.762622) { SRID = 4326 };
            var idempotency = RequestIdempotency.Create("test-scope", "idemp_existing_tenant");
            var request = SurveyRequest.CreateExistingTenantNewFarm(
                tenantId,
                requestedByUserId,
                "REQ-2026-0002",
                service,
                idempotency,
                "John Doe",
                "john@example.com",
                "0901234567",
                "New Farm Name",
                "456 New Road",
                20m,
                point,
                150,
                null,
                null,
                null,
                createdAt);

            request.StartReview(now.AddHours(-1));
            return request;
        }

        public SurveyRequest CreateExistingFarmFollowUpRequest(ContractScope scope, Guid tenantId, Guid farmId, Guid requestedByUserId)
        {
            var now = TimeProvider.GetUtcNow();
            var createdAt = now.AddHours(-2);
            var service = scope == ContractScope.V3
                ? SurveyService.CreateV3("MAP", "Mapping Baseline", "Description", createdAt)
                : SurveyService.Create("PLANT_HEALTH", "Plant Health", "Description", SurveyServiceType.PlantHealth, createdAt);

            var point = new Point(106.660172, 10.762622) { SRID = 4326 };
            var idempotency = RequestIdempotency.Create("test-scope", "idemp_existing_farm");
            var request = SurveyRequest.CreateExistingFarmSurvey(
                tenantId,
                farmId,
                requestedByUserId,
                "REQ-2026-0003",
                service,
                idempotency,
                "John Doe",
                "john@example.com",
                "0901234567",
                "Existing Farm Name",
                "789 Existing Road",
                25m,
                point,
                120,
                null,
                null,
                null,
                createdAt);

            request.StartReview(now.AddHours(-1));
            return request;
        }

        public static ApproveSurveyRequestCommand CreateValidCommand(
            Guid requestId,
            uint version,
            string? idempotencyKey = null,
            string? reason = null)
        {
            var checklist = new SurveyRequestReviewChecklistInput(
                SurveyRequestReviewChecklistDefinition.Version,
                new Dictionary<string, bool>
                {
                    [SurveyRequestReviewChecklistDefinition.ContactValidCode] = true,
                    [SurveyRequestReviewChecklistDefinition.DragonFruitFarmConfirmedCode] = true,
                    [SurveyRequestReviewChecklistDefinition.ServiceAreaSupportedCode] = true,
                    [SurveyRequestReviewChecklistDefinition.CandidateLocationSufficientCode] = true
                },
                Notes: null);

            return new ApproveSurveyRequestCommand(
                requestId,
                Guid.NewGuid(),
                reason ?? "Eligible farm location and verified applicant documents.",
                checklist,
                version,
                idempotencyKey ?? $"idemp_{Guid.NewGuid():N}"[..20]);
        }
    }

    private sealed class TestSurveyApprovalUnitOfWork : ISurveyApprovalUnitOfWork
    {
        public bool Flushed { get; private set; }
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            Flushed = true;
            return Task.CompletedTask;
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public void AddAuditLog(AuditLog auditLog) => AuditLogs.Add(auditLog);
    }

    private sealed class TestSurveyOrderRepository : ISurveyOrderRepository
    {
        public SurveyOrder? SavedOrder { get; private set; }
        public SurveyOrderCommercialSnapshot? SavedCommercialSnapshot { get; private set; }
        public OrderStateHistory? SavedStateHistory { get; private set; }

        public Task<SurveyOrder?> GetBySurveyRequestIdAsync(
            Guid surveyRequestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedOrder?.SurveyRequestId == surveyRequestId ? SavedOrder : null);

        public void Add(SurveyOrder surveyOrder) => SavedOrder = surveyOrder;

        public void AddCommercialSnapshot(SurveyOrderCommercialSnapshot snapshot) =>
            SavedCommercialSnapshot = snapshot;

        public void AddStateHistory(OrderStateHistory history) =>
            SavedStateHistory = history;
    }

    private sealed class TestSurveyApprovalProvisioningPort : ISurveyApprovalProvisioningPort
    {
        public StageSurveyApprovalTenant? StagedTenant { get; private set; }
        public StageSurveyApprovalFarm? StagedFarm { get; private set; }
        public StageSurveyApprovalPrimaryAssignment? StagedAssignment { get; private set; }
        public StageSurveyApprovalOwnerInvitation? StagedInvitation { get; private set; }

        public Guid TenantId { get; set; } = Guid.NewGuid();
        public Guid FarmId { get; set; } = Guid.NewGuid();
        public Guid AssignmentId { get; set; } = Guid.NewGuid();
        public Guid InvitationId { get; set; } = Guid.NewGuid();

        public Task<Result<SurveyApprovalProvisionedTenant>> StageTenantAsync(
            StageSurveyApprovalTenant request,
            CancellationToken cancellationToken = default)
        {
            StagedTenant = request;
            return Task.FromResult(Result.Success(new SurveyApprovalProvisionedTenant(TenantId, request.Code, request.Name)));
        }

        public Task<Result<SurveyApprovalProvisionedFarm>> StageFarmAsync(
            StageSurveyApprovalFarm request,
            CancellationToken cancellationToken = default)
        {
            StagedFarm = request;
            return Task.FromResult(Result.Success(new SurveyApprovalProvisionedFarm(FarmId, request.TenantId, request.Code, request.Name)));
        }

        public Task<Result<SurveyApprovalPrimaryAssignment>> StagePrimaryManagerAssignmentAsync(
            StageSurveyApprovalPrimaryAssignment request,
            CancellationToken cancellationToken = default)
        {
            StagedAssignment = request;
            return Task.FromResult(Result.Success(new SurveyApprovalPrimaryAssignment(AssignmentId, request.SystemManagerProfileId)));
        }

        public Task<Result<SurveyApprovalOwnerInvitation>> StageOwnerInvitationAsync(
            StageSurveyApprovalOwnerInvitation request,
            CancellationToken cancellationToken = default)
        {
            StagedInvitation = request;
            return Task.FromResult(Result.Success(new SurveyApprovalOwnerInvitation(InvitationId, request.Email, request.CreatedAt.AddDays(7))));
        }
    }

    private sealed class TestSurveyApprovalIdempotencyRepository : ISurveyApprovalIdempotencyRepository
    {
        public SurveyApprovalIdempotencyRecord? Claimed { get; private set; }
        public Dictionary<string, SurveyApprovalIdempotencyRecord> Records { get; } = new();

        public Task<SurveyApprovalIdempotencyRecord?> GetByActorAndKeyAsync(
            Guid actorId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            Records.TryGetValue(idempotencyKey, out var record);
            return Task.FromResult(record);
        }

        public Task<SurveyApprovalIdempotencyRecord?> GetByRequestIdAsync(
            Guid surveyRequestId,
            CancellationToken cancellationToken = default)
        {
            var record = Records.Values.FirstOrDefault(r => r.SurveyRequestId == surveyRequestId);
            return Task.FromResult(record);
        }

        public Task AddClaimAsync(
            SurveyApprovalIdempotencyRecord record,
            CancellationToken cancellationToken = default)
        {
            Claimed = record;
            Records[record.IdempotencyKey] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class TestSurveyCommercialSnapshotResolver : ISurveyCommercialSnapshotResolver
    {
        public Result<ResolvedSurveyCommercialSnapshot>? ResultToReturn { get; set; }
        public ResolvedSurveyCommercialSnapshot? Snapshot { get; set; }

        public Task<Result<ResolvedSurveyCommercialSnapshot>> ResolveForApprovalAsync(
            Guid surveyServiceId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
        {
            if (ResultToReturn is not null)
            {
                return Task.FromResult(ResultToReturn);
            }

            if (Snapshot is not null)
            {
                return Task.FromResult(Result.Success(Snapshot));
            }

            return Task.FromResult(Result.Success(new ResolvedSurveyCommercialSnapshot(
                BusinessPolicyVersionId: Guid.NewGuid(),
                PolicyFingerprint: "fingerprint_1234567890abcdef",
                SurveyServiceRevisionId: Guid.NewGuid(),
                SurveyServiceRevisionVersion: 1,
                ServiceCode: "MAP",
                MapsPoles: true,
                ScreensDisease: false,
                PriceVersionId: Guid.NewGuid(),
                PriceVersionNumber: 1,
                PricePerPole: 5000m,
                Currency: "VND",
                DepositPolicyId: Guid.NewGuid(),
                DepositVersionNumber: 1,
                DepositAmount: 100000m,
                ClaimPolicyId: Guid.NewGuid(),
                ClaimPolicyVersionNumber: 1,
                ClaimWindowDays: 7,
                ClaimTimeZoneId: "Asia/Ho_Chi_Minh",
                ExcludeNoticeDate: true,
                CancellationPolicyId: Guid.NewGuid(),
                CancellationPolicyVersionNumber: 1,
                RefundableBeforeCapture: true,
                RetainDepositAfterCapture: true,
                AllowedFreeNoShowReschedules: 1,
                EvaluatedAt: evaluatedAt)));
        }
    }

    private sealed class TestExecutionContext : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId { get; set; }
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class TestSurveyApprovalOutbox : ISurveyApprovalOutbox
    {
        public List<object> Envelopes { get; } = [];

        public void Add<TPayload>(IntegrationEventEnvelope<TPayload> envelope, string? partitionKey = null) =>
            Envelopes.Add(envelope);
    }

    private sealed class TestAuditWriter : IAuditWriter
    {
        public void AddUserAction(
            IAuditLogSink sink,
            Guid tenantId,
            Guid? farmId,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt)
        {
        }

        public void AddSystemAdminAction(
            IAuditLogSink sink,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt)
        {
        }
    }

    private sealed class TestNumberGenerator : ISurveyApprovalNumberGenerator
    {
        public string CreateFarmCode() => "FARM-001";
        public string CreateOrderNumber() => "SO-2026-0001";
    }

    private sealed class TestSurveyApprovalRequestRepository : ISurveyApprovalRequestRepository
    {
        public SurveyRequest? Request { get; set; }

        public Task<SurveyRequest?> GetForApprovalAsync(
            Guid surveyRequestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Request?.Id == surveyRequestId ? Request : null);
    }

    private sealed class TestSurveyApprovalPrerequisiteResolver : ISurveyApprovalPrerequisiteResolver
    {
        public Result<SurveyApprovalPrerequisites>? ResultToReturn { get; set; }
        public SurveyApprovalFarmBaselineReference? BaselineToReturn { get; set; }

        public Task<Result<SurveyApprovalPrerequisites>> ResolveAsync(
            SurveyRequest request,
            Guid? selectedSystemManagerProfileId,
            DateTimeOffset approvalTime,
            CancellationToken cancellationToken = default)
        {
            if (ResultToReturn is not null)
            {
                return Task.FromResult(ResultToReturn);
            }

            var manager = new SurveyApprovalManagerReference(
                selectedSystemManagerProfileId ?? Guid.NewGuid(),
                Guid.NewGuid(),
                IsUserActive: true,
                IsProfileActive: true,
                IsAvailable: true,
                Version: 1);

            return Task.FromResult(Result.Success(new SurveyApprovalPrerequisites(
                Service: new SurveyApprovalServiceReference(
                    request.SurveyServiceId,
                    request.SurveyService.ServiceType,
                    SurveyServiceStatus.Active,
                    Version: 1,
                    Scope: request.SurveyService.Scope,
                    PriceId: request.SurveyService.Scope == ContractScope.Legacy ? Guid.NewGuid() : null,
                    PricePerPole: request.SurveyService.Scope == ContractScope.Legacy ? 1000m : null),
                Manager: manager,
                TenantOwner: request.TenantId.HasValue && request.RequestedByUserId.HasValue
                    ? new SurveyApprovalTenantOwnerReference(
                        request.TenantId.Value,
                        request.RequestedByUserId.Value,
                        IsOwner: true,
                        IsMembershipActive: true,
                        IsTenantActive: true,
                        IsUserActive: true,
                        MembershipVersion: 1)
                    : null,
                Farm: request.FarmId.HasValue && request.TenantId.HasValue
                    ? new SurveyApprovalFarmReference(
                        request.TenantId.Value,
                        request.FarmId.Value,
                        IsActive: true,
                        Version: 1)
                    : null,
                CurrentPrimaryAssignment: request.FarmId.HasValue && request.TenantId.HasValue
                    ? new SurveyApprovalPrimaryAssignmentReference(
                        Guid.NewGuid(),
                        request.TenantId.Value,
                        request.FarmId.Value,
                        manager.SystemManagerProfileId,
                        Version: 1)
                    : null,
                FarmBaseline: BaselineToReturn ?? (request.Kind == SurveyRequestKind.ExistingFarmSurvey
                    ? new SurveyApprovalFarmBaselineReference(
                        request.TenantId ?? Guid.NewGuid(),
                        request.FarmId!.Value,
                        Guid.NewGuid(),
                        10)
                    : null),
                RequiresBaselineMapping: BaselineToReturn is not null
                    ? SurveyApprovalBaselineRequirementResolver.RequiresBaselineMapping(BaselineToReturn)
                    : request.Kind != SurveyRequestKind.ExistingFarmSurvey,
                OrderCompatibility: new SurveyApprovalOrderCompatibilityDecision(
                    PreviousCompatibleOrderId: null,
                    PolicyVersion: "v1",
                    Reason: "NO_PREVIOUS_ORDER"),
                ApplicableDepositAmount: 5_000m,
                ApplicableComboDiscountPercentage: 20m)));
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
