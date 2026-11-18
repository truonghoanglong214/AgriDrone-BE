using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Identity;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Services;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Npgsql;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyScopeBaselineRemediationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminActorId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid PrimaryManagerProfileId = Guid.NewGuid();

    private static SurveysDbContext CreateSurveysDbContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"survey-scope-remediation-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SurveysDbContext(options);
    }

    private static async Task<(SurveyService service, BusinessPolicyVersion bundle)> SeedPoliciesAsync(
        SurveysDbContext context,
        ContractScope scope = ContractScope.V3)
    {
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);

        SurveyService service;
        if (scope == ContractScope.V3)
        {
            service = SurveyService.CreateV3("MAP", "Orthophoto Mapping", "Boundary mapping", T0);
            service.Activate(T0, expectedVersion: 0);
        }
        else
        {
            service = SurveyService.Create("PLANT_HEALTH", "Plant Health", "Desc", SurveyServiceType.PlantHealth, T0);
            service.Activate(T0, expectedVersion: 0);
        }
        serviceRepo.Add(service);

        var rev = SurveyServiceRevision.Create(
            Guid.NewGuid(), service.Id, 1, null, "Revision 1", "Desc", true, false, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddRevision(rev);

        var price = SurveyServicePrice.CreatePerPoleV3(
            Guid.NewGuid(), service.Id, 1, null, 15_000m, CurrencyCode.Vnd, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddPriceV3(price);

        var deposit = SurveyDepositPolicy.Create(
            Guid.NewGuid(), 1, null, 100_000m, CurrencyCode.Vnd, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddDepositPolicy(deposit);

        var claim = ClaimPolicy.Create(
            Guid.NewGuid(), 1, null, 7, "Asia/Ho_Chi_Minh", true, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddClaimPolicy(claim);

        var cancellation = CancellationPolicy.Create(
            Guid.NewGuid(), 1, null, true, true, 1, true, true, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddCancellationPolicy(cancellation);

        var bundle = BusinessPolicyVersion.Create(
            Guid.NewGuid(), "DEFAULT_COMMERCIAL_BUNDLE", 1, null, deposit.Id, claim.Id, cancellation.Id,
            [rev.Id], CurrencyCode.Vnd, T0, null, AdminActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), AdminActorId, T0, "INIT");
        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);

        var nextDecision = activation.ApplyDecision(
            Guid.NewGuid(),
            bundle.Id,
            publicIntakeEnabled: true,
            captureEnabled: true,
            releaseEnabled: true,
            changedBy: AdminActorId,
            changedAt: T0.AddMinutes(5),
            reasonCode: "CONFIGURE_POLICY_GATES");
        policyRepo.AddActivationDecision(nextDecision);

        await context.SaveChangesAsync();
        return (service, bundle);
    }

    // -----------------------------------------------------------------------------------
    // Test 1: Legacy có published baseline/count hợp lệ -> Approval không yêu cầu mapping lại
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task LegacyWithPublishedBaselineAndConfirmedCountDoesNotRequireMapping()
    {
        var publishedBaseMapId = Guid.NewGuid();
        var baseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: publishedBaseMapId,
            ConfirmedActivePoleCount: 100);

        var harness = new RemediationApprovalTestHarness(baseline);
        var request = harness.CreateExistingFarmRequest(ContractScope.Legacy, TenantId, FarmId, OwnerUserId);
        harness.RequestRepository.Request = request;

        var command = RemediationApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.Legacy, order.Scope);
        Assert.False(order.RequiresBaselineMapping);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, order.Status);
    }

    // -----------------------------------------------------------------------------------
    // Test 2: Legacy chưa có baseline -> Approval tạo Order cần mapping
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task LegacyWithoutPublishedBaselineApprovalCreatesOrderRequiringMapping()
    {
        var baseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var harness = new RemediationApprovalTestHarness(baseline);
        var request = harness.CreateExistingFarmRequest(ContractScope.Legacy, TenantId, FarmId, OwnerUserId);
        harness.RequestRepository.Request = request;

        var command = RemediationApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var order = harness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.Legacy, order.Scope);
        Assert.True(order.RequiresBaselineMapping);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
    }

    // -----------------------------------------------------------------------------------
    // Test 3: V3 chỉ có map legacy chưa adoption -> Intake và approval đều từ chối
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task V3WithLegacyUnadoptedMapIntakeAndApprovalBothReject()
    {
        // 3a: Intake rejects
        using var context = CreateSurveysDbContext();
        var (service, _) = await SeedPoliciesAsync(context, ContractScope.V3);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm A", "123 Farm Rd", 10m, 106.6, 10.8, 4326));
        var ownerQuery = new FakeTenantOwnerReferenceQuery(
            new TenantOwnerRequestReference(TenantId, OwnerUserId, "Owner A", "owner@example.com", "0901234567"));

        var legacyMapBaselineQuery = new FakeV3FollowUpBaselineQuery(
            V3FollowUpBaselineEligibilityResult.Ineligible("Base map originated from a Legacy survey order without verified adoption."));

        var intakeHandler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-V3-LEGACYMAP"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, OwnerUserId),
            new FixedTimeProvider(T1),
            legacyMapBaselineQuery);

        var intakeCommand = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-v3-unadopted-map",
            service.Id,
            FarmId: FarmId,
            FarmName: null,
            FarmAddress: null,
            ApproximateAreaHa: null,
            Longitude: null,
            Latitude: null,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var intakeResult = await intakeHandler.Handle(intakeCommand, CancellationToken.None);
        Assert.False(intakeResult.IsSuccess);
        Assert.Equal("SurveyRequest.FollowUpBaselineRequired", intakeResult.Error.Code);

        // 3b: Approval rejects
        var unadoptedBaseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var approvalHarness = new RemediationApprovalTestHarness(unadoptedBaseline);
        var approvalRequest = approvalHarness.CreateExistingFarmRequest(ContractScope.V3, TenantId, FarmId, OwnerUserId);
        approvalHarness.RequestRepository.Request = approvalRequest;

        var approvalCommand = RemediationApprovalTestHarness.CreateValidCommand(approvalRequest.Id, approvalRequest.Version);
        var approvalResult = await approvalHarness.Handler.Handle(approvalCommand, CancellationToken.None);

        Assert.True(approvalResult.IsFailure);
        Assert.Equal(SurveyApprovalError.RequestContextInvalidCode, approvalResult.Error.Code);
        Assert.Null(approvalHarness.OrderRepository.SavedOrder);
    }

    // -----------------------------------------------------------------------------------
    // Test 4: V3 baseline chưa bàn giao/đã withdrawn -> Intake và approval đều từ chối
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task V3BaselineNotDeliveredOrWithdrawnIntakeAndApprovalBothReject()
    {
        // 4a: Intake rejects when baseline not delivered
        using var context = CreateSurveysDbContext();
        var (service, _) = await SeedPoliciesAsync(context, ContractScope.V3);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm B", "456 Farm Rd", 12m, 106.6, 10.8, 4326));
        var ownerQuery = new FakeTenantOwnerReferenceQuery(
            new TenantOwnerRequestReference(TenantId, OwnerUserId, "Owner B", "owner@example.com", "0901234567"));

        var withdrawnBaselineQuery = new FakeV3FollowUpBaselineQuery(
            V3FollowUpBaselineEligibilityResult.Ineligible("Source survey order is not completed."));

        var intakeHandler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-V3-WITHDRAWN"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, OwnerUserId),
            new FixedTimeProvider(T1),
            withdrawnBaselineQuery);

        var intakeCommand = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-v3-withdrawn",
            service.Id,
            FarmId: FarmId,
            FarmName: null,
            FarmAddress: null,
            ApproximateAreaHa: null,
            Longitude: null,
            Latitude: null,
            EstimatedPoleCount: 80,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var intakeResult = await intakeHandler.Handle(intakeCommand, CancellationToken.None);
        Assert.False(intakeResult.IsSuccess);
        Assert.Equal("SurveyRequest.FollowUpBaselineRequired", intakeResult.Error.Code);

        // 4b: Approval rejects
        var withdrawnApprovalBaseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var approvalHarness = new RemediationApprovalTestHarness(withdrawnApprovalBaseline);
        var approvalRequest = approvalHarness.CreateExistingFarmRequest(ContractScope.V3, TenantId, FarmId, OwnerUserId);
        approvalHarness.RequestRepository.Request = approvalRequest;

        var approvalCommand = RemediationApprovalTestHarness.CreateValidCommand(approvalRequest.Id, approvalRequest.Version);
        var approvalResult = await approvalHarness.Handler.Handle(approvalCommand, CancellationToken.None);

        Assert.True(approvalResult.IsFailure);
        Assert.Equal(SurveyApprovalError.RequestContextInvalidCode, approvalResult.Error.Code);
        Assert.Null(approvalHarness.OrderRepository.SavedOrder);
    }

    // -----------------------------------------------------------------------------------
    // Test 5: V3 có bằng chứng bàn giao hợp lệ -> Intake thành công; Order ghim đúng baseline ID
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task V3WithValidDeliveredBaselineIntakeSucceedsAndOrderPinsDeliveredBaselineId()
    {
        var deliveredBaseMapId = Guid.NewGuid();
        var sourceOrderId = Guid.NewGuid();

        // 5a: Intake succeeds
        using var context = CreateSurveysDbContext();
        var (service, bundle) = await SeedPoliciesAsync(context, ContractScope.V3);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm C", "789 Farm Rd", 15m, 106.6, 10.8, 4326));
        var ownerQuery = new FakeTenantOwnerReferenceQuery(
            new TenantOwnerRequestReference(TenantId, OwnerUserId, "Owner C", "owner@example.com", "0901234567"));

        var validBaselineQuery = new FakeV3FollowUpBaselineQuery(
            V3FollowUpBaselineEligibilityResult.Eligible(deliveredBaseMapId, sourceOrderId, 120));

        var intakeHandler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-V3-DELIVERED"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, OwnerUserId),
            new FixedTimeProvider(T1),
            validBaselineQuery);

        var intakeCommand = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-v3-delivered",
            service.Id,
            FarmId: FarmId,
            FarmName: null,
            FarmAddress: null,
            ApproximateAreaHa: null,
            Longitude: null,
            Latitude: null,
            EstimatedPoleCount: 120,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var intakeResult = await intakeHandler.Handle(intakeCommand, CancellationToken.None);
        Assert.True(intakeResult.IsSuccess);
        Assert.Equal("REQ-V3-DELIVERED", intakeResult.Value.RequestNumber);

        // 5b: Approval pins delivered baseline ID
        var validApprovalBaseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: deliveredBaseMapId,
            ConfirmedActivePoleCount: 120);

        var approvalHarness = new RemediationApprovalTestHarness(validApprovalBaseline);
        var approvalRequest = approvalHarness.CreateExistingFarmRequest(ContractScope.V3, TenantId, FarmId, OwnerUserId);
        approvalHarness.RequestRepository.Request = approvalRequest;

        var approvalCommand = RemediationApprovalTestHarness.CreateValidCommand(approvalRequest.Id, approvalRequest.Version);
        var approvalResult = await approvalHarness.Handler.Handle(approvalCommand, CancellationToken.None);

        Assert.True(approvalResult.IsSuccess, approvalResult.Error?.Description);
        var order = approvalHarness.OrderRepository.SavedOrder;
        Assert.NotNull(order);
        Assert.Equal(ContractScope.V3, order.Scope);
        Assert.False(order.RequiresBaselineMapping);
        Assert.Equal(deliveredBaseMapId, order.FarmBaseMapVersionId);
    }

    // -----------------------------------------------------------------------------------
    // Test 6: Baseline mất eligibility sau intake -> Approval thất bại, không lưu Order/snapshot/history dở dang
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task BaselineLosesEligibilityAfterIntakeApprovalFailsWithoutPartialState()
    {
        // Baseline was lost/withdrawn after intake
        var lostEligibilityBaseline = new SurveyApprovalFarmBaselineReference(
            TenantId,
            FarmId,
            CurrentPublishedBaseMapVersionId: null,
            ConfirmedActivePoleCount: null);

        var harness = new RemediationApprovalTestHarness(lostEligibilityBaseline);
        var request = harness.CreateExistingFarmRequest(ContractScope.V3, TenantId, FarmId, OwnerUserId);
        harness.RequestRepository.Request = request;

        var command = RemediationApprovalTestHarness.CreateValidCommand(request.Id, request.Version);
        var result = await harness.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyApprovalError.RequestContextInvalidCode, result.Error.Code);

        // Verify NO partial state was saved
        Assert.Null(harness.OrderRepository.SavedOrder);
        Assert.Null(harness.OrderRepository.SavedCommercialSnapshot);
        Assert.Null(harness.OrderRepository.SavedStateHistory);
        Assert.False(harness.UnitOfWork.Flushed);
    }

    // -----------------------------------------------------------------------------------
    // Test 7: Retry request đã tạo sau khi baseline thay đổi -> Trả acknowledgement cũ, không tạo request mới
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task RetryRequestAfterBaselineChangesReturnsOriginalAcknowledgementWithoutCreatingNewRequest()
    {
        using var context = CreateSurveysDbContext();
        var (service, bundle) = await SeedPoliciesAsync(context, ContractScope.V3);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm D", "100 Farm Rd", 15m, 106.6, 10.8, 4326));
        var ownerQuery = new FakeTenantOwnerReferenceQuery(
            new TenantOwnerRequestReference(TenantId, OwnerUserId, "Owner D", "owner@example.com", "0901234567"));

        var baselineQuery = new MutableFakeV3FollowUpBaselineQuery();
        var baseMapId = Guid.NewGuid();
        baselineQuery.Result = V3FollowUpBaselineEligibilityResult.Eligible(baseMapId, Guid.NewGuid(), 100);

        var handler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-ORIGINAL-ACK"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, OwnerUserId),
            new FixedTimeProvider(T1),
            baselineQuery);

        var command = new SubmitTenantOwnerSurveyRequestV3Command(
            "idemp-replay-key-1",
            service.Id,
            FarmId: FarmId,
            FarmName: null,
            FarmAddress: null,
            ApproximateAreaHa: null,
            Longitude: null,
            Latitude: null,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        // First attempt succeeds
        var firstResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal("REQ-ORIGINAL-ACK", firstResult.Value.RequestNumber);

        // Baseline now changes (loses eligibility)
        baselineQuery.Result = V3FollowUpBaselineEligibilityResult.Ineligible("Baseline was withdrawn by owner.");

        // Retry with exact same key and payload
        var retryResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(firstResult.Value.Id, retryResult.Value.Id);
        Assert.Equal(firstResult.Value.RequestNumber, retryResult.Value.RequestNumber);

        // Verify only 1 request was created in DB
        var totalRequests = await context.SurveyRequests.CountAsync();
        Assert.Equal(1, totalRequests);
    }

    // -----------------------------------------------------------------------------------
    // Test 8: Migrate tới schema trước V3 -> Seeder bỏ qua, không truy vấn cột scope;
    //         và kiểm tra lỗi kết nối được ném ra thật sự (không nuốt ngoại lệ)
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task MigrateToPreV3SchemaSeederSkipsWithoutQueryingScopeColumnAndPropagatesConnectionErrors()
    {
        // 8a: Schema check query returns null -> seeder skips
        var mockPreV3Connection = new FakeDbConnection(hasScopeColumn: false);
        var servicesPreV3 = new ServiceCollection();
        servicesPreV3.AddDbContext<TestSeederDbContext>(options => options.UseNpgsql(mockPreV3Connection));
        var providerPreV3 = servicesPreV3.BuildServiceProvider();

        using (var dbPreV3 = providerPreV3.GetRequiredService<TestSeederDbContext>())
        {
            // SeedAsync should cleanly skip because scope column does not exist
            await SurveyServiceV3Seeder.SeedAsync(dbPreV3, CancellationToken.None);
            Assert.False(mockPreV3Connection.QueriedSurveyServicesTable);
        }

        // 8b: Connection failure throws real exception, not swallowed
        var throwingConnection = new FakeDbConnection(hasScopeColumn: false, shouldThrowOnOpen: true);
        var servicesThrowing = new ServiceCollection();
        servicesThrowing.AddDbContext<TestSeederDbContext>(options => options.UseNpgsql(throwingConnection));
        var providerThrowing = servicesThrowing.BuildServiceProvider();

        using (var dbThrowing = providerThrowing.GetRequiredService<TestSeederDbContext>())
        {
            var ex = await Assert.ThrowsAsync<NpgsqlException>(() =>
                SurveyServiceV3Seeder.SeedAsync(dbThrowing, CancellationToken.None));
            Assert.Equal("Simulated DB connection failure", ex.Message);
        }
    }

    // -----------------------------------------------------------------------------------
    // Test 9: Fresh/upgrade tới schema V3 và seed lại -> Có đúng MAP/COMBO, giữ nguyên dữ liệu legacy
    // -----------------------------------------------------------------------------------
    [Fact]
    public async Task UpgradeToV3SchemaAndReseedSeedsMapAndComboIdempotentlyWhilePreservingLegacyData()
    {
        using var context = CreateSurveysDbContext();

        // 9a: Pre-existing legacy service
        var legacyService = SurveyService.Create(
            "PLANT_HEALTH_LEGACY", "Legacy Plant Health", "Legacy service description", SurveyServiceType.PlantHealth, T0);
        context.SurveyServices.Add(legacyService);
        await context.SaveChangesAsync();

        // 9b: First seed V3 (in-memory provider bypasses relational schema check and returns true)
        await SurveyServiceV3Seeder.SeedAsync(context, CancellationToken.None);

        var services = await context.SurveyServices.ToListAsync();
        Assert.Equal(3, services.Count);

        var legacy = services.Single(s => s.Code == "PLANT_HEALTH_LEGACY");
        Assert.Equal(ContractScope.Legacy, legacy.Scope);

        var map = services.Single(s => s.Code == "MAP");
        Assert.Equal(ContractScope.V3, map.Scope);
        Assert.Equal(SurveyServiceStatus.Active, map.Status);

        var combo = services.Single(s => s.Code == "COMBO");
        Assert.Equal(ContractScope.V3, combo.Scope);
        Assert.Equal(SurveyServiceStatus.Active, combo.Status);

        // 9c: Reseed is idempotent: no duplicates added
        await SurveyServiceV3Seeder.SeedAsync(context, CancellationToken.None);
        var reseededServices = await context.SurveyServices.ToListAsync();
        Assert.Equal(3, reseededServices.Count);
    }

    // -----------------------------------------------------------------------------------
    // Test Harness & Fakes
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(ContractScope.Legacy, false)]
    [InlineData(ContractScope.Legacy, true)]
    [InlineData(ContractScope.V3, false)]
    public async Task ApprovalKeepsMainLegacyPoliciesSeparateFromPinnedV3Terms(ContractScope scope, bool components)
    {
        var baseline = new SurveyApprovalFarmBaselineReference(TenantId, FarmId, Guid.NewGuid(), 100);
        var policies = new LegacyPolicyProbe();
        var harness = new RemediationApprovalTestHarness(baseline, scope, policies, policies, components);
        var request = harness.CreateExistingFarmRequest(scope, TenantId, FarmId, OwnerUserId);
        harness.RequestRepository.Request = request;

        var result = await harness.Handler.Handle(
            RemediationApprovalTestHarness.CreateValidCommand(request.Id, request.Version),
            CancellationToken.None);

        if (scope == ContractScope.Legacy && !components)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(SurveyApprovalError.PolicyConfigurationMissingCode, result.Error.Code);
            Assert.Null(harness.OrderRepository.SavedOrder);
            return;
        }
        Assert.True(result.IsSuccess, result.Error?.Description);
        var order = Assert.IsType<SurveyOrder>(harness.OrderRepository.SavedOrder);
        Assert.Equal(scope, order.Scope);
        if (scope == ContractScope.V3)
        {
            Assert.Equal(0, policies.DepositReads);
            Assert.Equal(0, policies.DiscountReads);
            Assert.Equal(harness.SnapshotResolver.Snapshot!.DepositAmount, order.DepositAmount);
            Assert.Equal(harness.SnapshotResolver.Snapshot.BusinessPolicyVersionId, order.BusinessPolicyVersionId);
            Assert.Null(order.ComboDiscountPercentage);
        }
        else
        {
            Assert.Equal(1, policies.DepositReads);
            Assert.Equal(1, policies.DiscountReads);
            Assert.Equal(25_000m, order.DepositAmount);
            Assert.Equal(35m, order.ComboDiscountPercentage);
            Assert.Equal(components ? 600m : (decimal?)null, order.BookedMappingPricePerPole);
            Assert.Equal(components ? 400m : (decimal?)null, order.BookedPlantHealthPricePerPole);
            Assert.Null(order.BusinessPolicyVersionId);
        }
    }

    private sealed class LegacyPolicyProbe : ISurveyDepositPolicyRepository, ISurveyComboDiscountPolicyRepository
    {
        public int DepositReads { get; private set; }
        public int DiscountReads { get; private set; }

        Task<LegacySurveyDepositPolicy?> ISurveyDepositPolicyRepository.GetCurrentAsync(CancellationToken cancellationToken)
        {
            DepositReads++;
            return Task.FromResult<LegacySurveyDepositPolicy?>(LegacySurveyDepositPolicy.Create(1, 25_000m, AdminActorId, T0));
        }

        Task<SurveyComboDiscountPolicy?> ISurveyComboDiscountPolicyRepository.GetCurrentAsync(CancellationToken cancellationToken)
        {
            DiscountReads++;
            return Task.FromResult<SurveyComboDiscountPolicy?>(SurveyComboDiscountPolicy.Create(1, 35m, AdminActorId, T0));
        }

        Task<LegacySurveyDepositPolicy?> ISurveyDepositPolicyRepository.GetCurrentForUpdateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<LegacySurveyDepositPolicy>> ISurveyDepositPolicyRepository.GetHistoryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        void ISurveyDepositPolicyRepository.Add(LegacySurveyDepositPolicy policy) => throw new NotSupportedException();
        Task<SurveyComboDiscountPolicy?> ISurveyComboDiscountPolicyRepository.GetCurrentForUpdateAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<SurveyComboDiscountPolicy>> ISurveyComboDiscountPolicyRepository.GetHistoryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        void ISurveyComboDiscountPolicyRepository.Add(SurveyComboDiscountPolicy policy) => throw new NotSupportedException();
    }

    private sealed class RemediationApprovalTestHarness
    {
        public TestSurveyApprovalUnitOfWork UnitOfWork { get; } = new();
        public TestSurveyApprovalRequestRepository RequestRepository { get; } = new();
        public TestSurveyApprovalIdempotencyRepository IdempotencyRepository { get; } = new();
        public TestSurveyOrderRepository OrderRepository { get; } = new();
        public TestSurveyApprovalProvisioningPort ProvisioningPort { get; } = new();
        public TestSurveyApprovalOutbox Outbox { get; } = new();
        public TestAuditWriter AuditWriter { get; } = new();
        public FakeExecutionContext ExecutionContext { get; } = new(TenantId, AdminActorId);
        public FixedTimeProvider TimeProvider { get; } = new(T0);
        public TestNumberGenerator NumberGenerator { get; } = new();
        public TestSurveyCommercialSnapshotResolver SnapshotResolver { get; } = new();
        public ApproveSurveyRequestCommandHandler Handler { get; }

        public RemediationApprovalTestHarness(
            SurveyApprovalFarmBaselineReference baseline,
            ContractScope scope = ContractScope.Legacy,
            ISurveyDepositPolicyRepository? depositPolicies = null,
            ISurveyComboDiscountPolicyRepository? discountPolicies = null,
            bool components = true)
        {

            var snapshot = new ResolvedSurveyCommercialSnapshot(
                BusinessPolicyVersionId: Guid.NewGuid(),
                PolicyFingerprint: "fingerprint_remediation_test",
                SurveyServiceRevisionId: Guid.NewGuid(),
                SurveyServiceRevisionVersion: 1,
                ServiceCode: "MAP",
                MapsPoles: true,
                ScreensDisease: false,
                PriceVersionId: Guid.NewGuid(),
                PriceVersionNumber: 1,
                PricePerPole: 15000m,
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
            SnapshotResolver.Snapshot = snapshot;

            var referenceQueries = new FakeSurveyApprovalReferenceQueries(baseline, scope, components);
            var prerequisiteResolver = new SurveyApprovalPrerequisiteResolver(referenceQueries, depositPolicies, discountPolicies);

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
                prerequisiteResolver,
                SnapshotResolver,
                orchestrators,
                Outbox,
                AuditWriter,
                ExecutionContext,
                TimeProvider,
                NullLogger<ApproveSurveyRequestCommandHandler>.Instance);
        }

        public SurveyRequest CreateExistingFarmRequest(ContractScope scope, Guid tenantId, Guid farmId, Guid requestedByUserId)
        {
            var now = TimeProvider.GetUtcNow();
            var createdAt = now.AddHours(-2);
            var service = scope == ContractScope.V3
                ? SurveyService.CreateV3("MAP", "Orthophoto Mapping", "Description", createdAt)
                : SurveyService.Create("PLANT_HEALTH", "Plant Health", "Description", SurveyServiceType.PlantHealth, createdAt);

            var point = new Point(106.660172, 10.762622) { SRID = 4326 };
            var idempotency = RequestIdempotency.Create("test-scope", $"idemp_{Guid.NewGuid():N}");
            var request = SurveyRequest.CreateExistingFarmSurvey(
                tenantId,
                farmId,
                requestedByUserId,
                "REQ-2026-FARM-01",
                service,
                idempotency,
                "John Doe",
                "john@example.com",
                "0901234567",
                "Dragon Fruit Farm",
                "123 Farm Road",
                25m,
                point,
                100,
                null,
                null,
                null,
                createdAt);

            request.StartReview(now.AddHours(-1));
            return request;
        }

        public static ApproveSurveyRequestCommand CreateValidCommand(Guid requestId, uint version)
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
                PrimaryManagerProfileId,
                "Approved after verifying farm location and baseline status.",
                checklist,
                version,
                $"idemp_app_{Guid.NewGuid():N}"[..20]);
        }
    }

    private sealed class FakeSurveyApprovalReferenceQueries(
        SurveyApprovalFarmBaselineReference baseline,
        ContractScope scope = ContractScope.Legacy, bool components = true) : ISurveyApprovalReferenceQueries
    {
        private readonly SurveyApprovalFarmBaselineReference _baseline = baseline;

        public Task<SurveyApprovalServiceReference?> GetServiceForApprovalAsync(Guid surveyServiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalServiceReference?>(new SurveyApprovalServiceReference(
                surveyServiceId,
                SurveyServiceType.PlantHealth,
                SurveyServiceStatus.Active,
                1,
                scope,
                PriceId: scope == ContractScope.Legacy ? Guid.NewGuid() : null,
                PricePerPole: scope == ContractScope.Legacy ? 1000m : null,
                MappingPricePerPole: components && scope == ContractScope.Legacy ? 600m : null,
                PlantHealthPricePerPole: components && scope == ContractScope.Legacy ? 400m : null));

        public Task<SurveyApprovalTenantOwnerReference?> GetTenantOwnerForApprovalAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalTenantOwnerReference?>(new SurveyApprovalTenantOwnerReference(
                tenantId, userId, IsOwner: true, IsMembershipActive: true, IsTenantActive: true, IsUserActive: true, MembershipVersion: 1));

        public Task<SurveyApprovalFarmReference?> GetFarmForApprovalAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalFarmReference?>(new SurveyApprovalFarmReference(
                tenantId, farmId, IsActive: true, Version: 1));

        public Task<SurveyApprovalManagerReference?> GetManagerForApprovalAsync(Guid systemManagerProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalManagerReference?>(new SurveyApprovalManagerReference(
                systemManagerProfileId, Guid.NewGuid(), IsUserActive: true, IsProfileActive: true, IsAvailable: true, Version: 1));

        public Task<SurveyApprovalPrimaryAssignmentReference?> GetActivePrimaryAssignmentForApprovalAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalPrimaryAssignmentReference?>(new SurveyApprovalPrimaryAssignmentReference(
                Guid.NewGuid(), tenantId, farmId, PrimaryManagerProfileId, Version: 1));

        public Task<SurveyApprovalFarmBaselineReference> GetFarmBaselineForApprovalAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_baseline);

        public Task<SurveyApprovalFarmBaselineReference> GetV3FollowUpBaselineForApprovalAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_baseline);

        public Task<SurveyApprovalPreviousOrderReference?> GetPreviousOrderCandidateForApprovalAsync(Guid tenantId, Guid farmId, Guid surveyServiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyApprovalPreviousOrderReference?>(null);
    }

    private sealed class FakeV3FollowUpBaselineQuery(V3FollowUpBaselineEligibilityResult result) : IV3FollowUpBaselineQuery
    {
        public Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class MutableFakeV3FollowUpBaselineQuery : IV3FollowUpBaselineQuery
    {
        public V3FollowUpBaselineEligibilityResult Result { get; set; } =
            V3FollowUpBaselineEligibilityResult.Eligible(Guid.NewGuid(), Guid.NewGuid(), 100);

        public Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }

    private sealed class FakeFarmReferenceQuery(SurveyRequestFarmReference? farm = null) : ISurveyRequestFarmReferenceQuery
    {
        public Task<SurveyRequestFarmReference?> GetAsync(Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(farm);

        public Task<bool> HasPublishedBaseMapAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeTenantOwnerReferenceQuery(TenantOwnerRequestReference? owner = null) : ITenantOwnerRequestReferenceQuery
    {
        public Task<TenantOwnerRequestReference?> GetActiveOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(owner);
    }

    private sealed class FakeNumberGenerator(string number) : ISurveyRequestNumberGenerator
    {
        public string Create() => number;
    }

    private sealed class FakeOutbox : ISurveyIntegrationOutbox
    {
        public void Add<TPayload>(IntegrationEventEnvelope<TPayload> envelope, string? partitionKey = null) { }
    }

    private sealed class FakeExecutionContext(Guid tenantId, Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId => actorId;
        public Guid CorrelationId => Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestSurveyApprovalUnitOfWork : ISurveyApprovalUnitOfWork
    {
        public bool Flushed { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            Flushed = true;
            return Task.CompletedTask;
        }
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public void AddAuditLog(AuditLog auditLog) { }
    }

    private sealed class TestSurveyOrderRepository : ISurveyOrderRepository
    {
        public SurveyOrder? SavedOrder { get; private set; }
        public SurveyOrderCommercialSnapshot? SavedCommercialSnapshot { get; private set; }
        public OrderStateHistory? SavedStateHistory { get; private set; }

        public Task<SurveyOrder?> GetBySurveyRequestIdAsync(Guid surveyRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedOrder?.SurveyRequestId == surveyRequestId ? SavedOrder : null);

        public void Add(SurveyOrder surveyOrder) => SavedOrder = surveyOrder;
        public void AddCommercialSnapshot(SurveyOrderCommercialSnapshot snapshot) => SavedCommercialSnapshot = snapshot;
        public void AddStateHistory(OrderStateHistory history) => SavedStateHistory = history;
    }

    private sealed class TestSurveyApprovalProvisioningPort : ISurveyApprovalProvisioningPort
    {
        public Task<Result<SurveyApprovalProvisionedTenant>> StageTenantAsync(StageSurveyApprovalTenant request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SurveyApprovalProvisionedTenant(Guid.NewGuid(), request.Code, request.Name)));

        public Task<Result<SurveyApprovalProvisionedFarm>> StageFarmAsync(StageSurveyApprovalFarm request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SurveyApprovalProvisionedFarm(Guid.NewGuid(), request.TenantId, request.Code, request.Name)));

        public Task<Result<SurveyApprovalPrimaryAssignment>> StagePrimaryManagerAssignmentAsync(StageSurveyApprovalPrimaryAssignment request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SurveyApprovalPrimaryAssignment(Guid.NewGuid(), request.SystemManagerProfileId)));

        public Task<Result<SurveyApprovalOwnerInvitation>> StageOwnerInvitationAsync(StageSurveyApprovalOwnerInvitation request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SurveyApprovalOwnerInvitation(Guid.NewGuid(), request.Email, request.CreatedAt.AddDays(7))));
    }

    private sealed class TestSurveyApprovalIdempotencyRepository : ISurveyApprovalIdempotencyRepository
    {
        private readonly Dictionary<string, SurveyApprovalIdempotencyRecord> _records = new();

        public Task<SurveyApprovalIdempotencyRecord?> GetByActorAndKeyAsync(Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(idempotencyKey, out var record);
            return Task.FromResult(record);
        }

        public Task<SurveyApprovalIdempotencyRecord?> GetByRequestIdAsync(Guid surveyRequestId, CancellationToken cancellationToken = default)
        {
            var record = _records.Values.FirstOrDefault(r => r.SurveyRequestId == surveyRequestId);
            return Task.FromResult(record);
        }

        public Task AddClaimAsync(SurveyApprovalIdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.IdempotencyKey] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class TestSurveyCommercialSnapshotResolver : ISurveyCommercialSnapshotResolver
    {
        public ResolvedSurveyCommercialSnapshot? Snapshot { get; set; }

        public Task<Result<ResolvedSurveyCommercialSnapshot>> ResolveForApprovalAsync(Guid surveyServiceId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(Snapshot!));
    }

    private sealed class TestSurveyApprovalRequestRepository : ISurveyApprovalRequestRepository
    {
        public SurveyRequest? Request { get; set; }
        public Task<SurveyRequest?> GetForApprovalAsync(Guid surveyRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Request?.Id == surveyRequestId ? Request : null);
    }

    private sealed class TestSurveyApprovalOutbox : ISurveyApprovalOutbox
    {
        public void Add<TPayload>(IntegrationEventEnvelope<TPayload> envelope, string? partitionKey = null) { }
    }

    private sealed class TestAuditWriter : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId, Guid actorId, Guid correlationId, string entityType, Guid entityId, string action, System.Text.Json.JsonDocument? oldData, System.Text.Json.JsonDocument? newData, DateTimeOffset createdAt) { }
        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId, Guid correlationId, string entityType, Guid entityId, string action, System.Text.Json.JsonDocument? oldData, System.Text.Json.JsonDocument? newData, DateTimeOffset createdAt) { }
    }

    private sealed class TestNumberGenerator : ISurveyApprovalNumberGenerator
    {
        public string CreateFarmCode() => "FARM-001";
        public string CreateOrderNumber() => "SO-2026-0001";
    }

    private sealed class TestSeederDbContext(DbContextOptions<TestSeederDbContext> options)
        : DbContext(options)
    {
        public DbSet<SurveyService> SurveyServices => Set<SurveyService>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SurveyService>(b =>
            {
                b.Ignore(s => s.Prices);
                b.Ignore(s => s.Revisions);
            });
        }
    }

    // --- Mock DbConnection for Test 8 ---

    private sealed class FakeDbConnection(bool hasScopeColumn, bool shouldThrowOnOpen = false) : DbConnection
    {
        public bool QueriedSurveyServicesTable { get; private set; }
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string ConnectionString { get; set; } = "Host=localhost;Database=test;Username=test;Password=test";
        public override string Database => "test";
        public override string DataSource => "localhost";
        public override string ServerVersion => "16.0";
        public override ConnectionState State { get; } = ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }

        public override void Open()
        {
            if (shouldThrowOnOpen)
            {
                throw new NpgsqlException("Simulated DB connection failure");
            }
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            if (shouldThrowOnOpen)
            {
                throw new NpgsqlException("Simulated DB connection failure");
            }
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() =>
            new FakeDbCommand(this, hasScopeColumn, onQuerySurveyServices: () => QueriedSurveyServicesTable = true);
    }

    private sealed class FakeDbCommand(
        DbConnection connection,
        bool hasScopeColumn,
        Action onQuerySurveyServices) : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Prepare() { }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public override object? ExecuteScalar()
        {
            if (CommandText.Contains("information_schema.columns", StringComparison.OrdinalIgnoreCase))
            {
                return hasScopeColumn ? 1 : null;
            }

            if (CommandText.Contains("survey.survey_services", StringComparison.OrdinalIgnoreCase) ||
                CommandText.Contains("survey_services", StringComparison.OrdinalIgnoreCase))
            {
                onQuerySurveyServices();
            }

            return null;
        }

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ExecuteScalar());
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
