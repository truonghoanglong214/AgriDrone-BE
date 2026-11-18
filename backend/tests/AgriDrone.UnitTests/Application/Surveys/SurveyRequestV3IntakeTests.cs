using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Identity;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Services;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyRequestV3IntakeTests
{
    private static readonly TrackingTokenProtector TokenProtector =
        new(new EphemeralDataProtectionProvider());

    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    private static SurveysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"surveys-request-v3-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SurveysDbContext(options);
    }

    private static async Task<(SurveyService mapService, BusinessPolicyVersion bundle)> SeedPoliciesAsync(
        SurveysDbContext context,
        bool enablePublicIntake = true,
        bool enableRelease = true)
    {
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);

        var mapService = SurveyService.CreateV3("MAP", "Orthophoto Mapping", "Boundary mapping", T0);
        mapService.Activate(T0, expectedVersion: 0);
        serviceRepo.Add(mapService);

        var mapRev = SurveyServiceRevision.Create(
            Guid.NewGuid(), mapService.Id, 1, null, "MAP Revision 1", "Map desc", true, false, T0, null, ActorId, T0, "INIT");
        policyRepo.AddRevision(mapRev);

        var mapPrice = SurveyServicePrice.CreatePerPoleV3(
            Guid.NewGuid(), mapService.Id, 1, null, 15_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddPriceV3(mapPrice);

        var deposit = SurveyDepositPolicy.Create(
            Guid.NewGuid(), 1, null, 100_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddDepositPolicy(deposit);

        var claim = ClaimPolicy.Create(
            Guid.NewGuid(), 1, null, 7, "Asia/Ho_Chi_Minh", true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddClaimPolicy(claim);

        var cancellation = CancellationPolicy.Create(
            Guid.NewGuid(), 1, null, true, true, 1, true, true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddCancellationPolicy(cancellation);

        var bundle = BusinessPolicyVersion.Create(
            Guid.NewGuid(), "DEFAULT_COMMERCIAL_BUNDLE", 1, null, deposit.Id, claim.Id, cancellation.Id,
            [mapRev.Id], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), ActorId, T0, "INIT");

        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);

        var nextDecision = activation.ApplyDecision(
            Guid.NewGuid(),
            bundle.Id,
            publicIntakeEnabled: enablePublicIntake,
            captureEnabled: true,
            releaseEnabled: enableRelease,
            changedBy: ActorId,
            changedAt: T0.AddMinutes(5),
            reasonCode: "CONFIGURE_POLICY_GATES");

        policyRepo.AddActivationDecision(nextDecision);

        await context.SaveChangesAsync();
        return (mapService, bundle);
    }

    [Fact]
    public async Task NewCustomerWhenPublicIntakeDisabledReturnsPublicIntakeDisabledConflict()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var handler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-001"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var command = new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-1"),
            service.Id,
            "T-NEW",
            "New Farm Corp",
            "Nguyen Van A",
            "nva@example.com",
            "0901234567",
            "Farm A",
            "123 Farm Road",
            10.5m,
            106.6,
            10.8,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SurveyRequest.PublicIntakeDisabled", result.Error.Code);
    }

    [Fact]
    public async Task NewCustomerWhenReleaseDisabledFailsWithPublicIntakeDisabled()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: true, enableRelease: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var handler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-RELEASE-OFF"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var command = new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-rel-off"),
            service.Id,
            "T-NEW",
            "New Farm Corp",
            "Nguyen Van A",
            "nva@example.com",
            "0901234567",
            "Farm A",
            "123 Farm Road",
            10.5m,
            106.6,
            10.8,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SurveyRequest.PublicIntakeDisabled", result.Error.Code);
    }

    [Fact]
    public async Task NewCustomerWhenPublicIntakeEnabledSuccessfullyCreatesRequestQuoteAndTracking()
    {
        using var context = CreateContext();
        var (service, bundle) = await SeedPoliciesAsync(context, enablePublicIntake: true);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var handler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-002"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var command = new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-2"),
            service.Id,
            "T-NEW-2",
            "New Farm 2",
            "Nguyen Van B",
            "nvb@example.com",
            "0901234568",
            "Farm B",
            "456 Farm Road",
            20.0m,
            106.7,
            10.9,
            EstimatedPoleCount: 50,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: "Handle carefully");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("REQ-002", result.Value.RequestNumber);
        Assert.Equal(SurveyRequestKind.NewCustomer, result.Value.Kind);
        Assert.Equal(SurveyRequestStatus.Submitted, result.Value.Status);
        Assert.StartsWith("trk_", result.Value.TrackingToken, StringComparison.Ordinal);

        // Verify quote
        Assert.Equal(bundle.Id, result.Value.IndicativeQuote.BusinessPolicyVersionId);
        Assert.Equal(15_000m, result.Value.IndicativeQuote.PricePerPole);
        Assert.Equal(100_000m, result.Value.IndicativeQuote.DepositAmount);
        Assert.Equal(50, result.Value.IndicativeQuote.EstimatedPoleCount);
        Assert.Equal(750_000m, result.Value.IndicativeQuote.IndicativeTotalAmount);
        Assert.Equal("VND", result.Value.IndicativeQuote.Currency);

        // Verify tracking persisted in DB
        var tracking = await requestRepo.GetTrackingByRequestIdAsync(result.Value.Id);
        Assert.NotNull(tracking);
        Assert.Equal(TokenProtector.ComputeHash(result.Value.TrackingToken), tracking.TrackingTokenHash);
        Assert.Equal(result.Value.TrackingToken, TokenProtector.Unprotect(tracking.EncryptedToken));
    }

    [Fact]
    public async Task NewCustomerIdempotentReplayReturnsSameTrackingTokenAndQuote()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: true);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var handler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-REPLAY"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var command = new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-replay"),
            service.Id,
            "T-REP",
            "Replay Corp",
            "Nguyen Van C",
            "nvc@example.com",
            "0901234569",
            "Farm C",
            "789 Farm Road",
            15.0m,
            106.8,
            10.95,
            EstimatedPoleCount: 80,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var firstResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Replay identical command
        var replayResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(replayResult.IsSuccess);

        Assert.Equal(firstResult.Value.Id, replayResult.Value.Id);
        Assert.Equal(firstResult.Value.RequestNumber, replayResult.Value.RequestNumber);
        Assert.Equal(firstResult.Value.TrackingToken, replayResult.Value.TrackingToken);
        Assert.Equal(firstResult.Value.IndicativeQuote.PricePerPole, replayResult.Value.IndicativeQuote.PricePerPole);
    }

    [Fact]
    public async Task NewCustomerIdempotentPayloadMismatchReturnsConflict()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: true);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var handler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-CONFLICT"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var command1 = new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-conflict"),
            service.Id,
            "T-CONF",
            "Conflict Corp",
            "Nguyen Van D",
            "nvd@example.com",
            "0901234570",
            "Farm D",
            "101 Farm Road",
            10.0m,
            106.5,
            10.7,
            EstimatedPoleCount: 40,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var firstResult = await handler.Handle(command1, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Same idempotency key but different FarmName
        var command2 = command1 with { FarmName = "Changed Farm Name" };
        var secondResult = await handler.Handle(command2, CancellationToken.None);

        Assert.False(secondResult.IsSuccess);
        Assert.Equal(SurveyRequestError.IdempotencyPayloadMismatchCode, secondResult.Error.Code);
    }

    [Fact]
    public async Task TrackSurveyRequestV3WithValidTokenReturnsMinimalPublicStatusWithoutPii()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: true);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);
        var submitHandler = new SubmitNewCustomerSurveyRequestV3CommandHandler(
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-TRACK"),
            new FakeOutbox(),
            context,
            TokenProtector,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1));

        var submitResult = await submitHandler.Handle(new SubmitNewCustomerSurveyRequestV3Command(
            RequestIdempotency.Create("public:test", "key-track"),
            service.Id,
            "T-TRK",
            "Track Corp",
            "Secret Applicant",
            "secret@example.com",
            "0999999999",
            "Secret Farm",
            "Confidential Address",
            5.0m,
            106.1,
            10.2,
            EstimatedPoleCount: 60,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: "Confidential internal note"), CancellationToken.None);

        Assert.True(submitResult.IsSuccess);
        var token = submitResult.Value.TrackingToken;

        // Query tracking
        var trackHandler = new TrackSurveyRequestV3QueryHandler(requestRepo);
        var trackResult = await trackHandler.Handle(new TrackSurveyRequestV3Query(token), CancellationToken.None);

        Assert.True(trackResult.IsSuccess);
        Assert.Equal("REQ-TRACK", trackResult.Value.RequestNumber);
        Assert.Equal(SurveyRequestStatus.Submitted, trackResult.Value.Status);
        Assert.Equal(service.Name, trackResult.Value.ServiceName);
        Assert.Equal(60, trackResult.Value.EstimatedPoleCount);
        Assert.Equal(900_000m, trackResult.Value.IndicativeTotalAmount);
        Assert.Equal(100_000m, trackResult.Value.DepositAmount);
        Assert.Equal("VND", trackResult.Value.Currency);
    }

    [Fact]
    public async Task TrackSurveyRequestV3WithInvalidTokenReturnsNotFound()
    {
        using var context = CreateContext();
        var requestRepo = new SurveyRequestRepository(context);
        var trackHandler = new TrackSurveyRequestV3QueryHandler(requestRepo);

        var trackResult = await trackHandler.Handle(
            new TrackSurveyRequestV3Query("trk_nonexistent0000000000000000000000000000000000000000000000000000"),
            CancellationToken.None);

        Assert.False(trackResult.IsSuccess);
        Assert.Equal("SurveyRequest.TrackingTokenNotFound", trackResult.Error.Code);
    }

    [Fact]
    public async Task TenantOwnerExistingTenantNewFarmCreatesRequestAndQuoteSnapshot()
    {
        using var context = CreateContext();
        var (service, bundle) = await SeedPoliciesAsync(context, enablePublicIntake: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery();
        var ownerQuery = new FakeTenantOwnerReferenceQuery(new TenantOwnerRequestReference(
            TenantId, ActorId, "Owner Name", "owner@example.com", "0908888888"));

        var handler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-OWNER-NEWFARM"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1),
            new FakeV3FollowUpBaselineQuery());

        var command = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-owner-newfarm",
            service.Id,
            FarmId: null,
            FarmName: "Owner New Farm",
            FarmAddress: "New Farm Location",
            ApproximateAreaHa: 12.0m,
            Longitude: 106.4,
            Latitude: 10.6,
            EstimatedPoleCount: 75,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("REQ-OWNER-NEWFARM", result.Value.RequestNumber);
        Assert.Equal(SurveyRequestKind.ExistingTenantNewFarm, result.Value.Kind);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Null(result.Value.FarmId);
        Assert.Equal(bundle.Id, result.Value.IndicativeQuote.BusinessPolicyVersionId);
        Assert.Equal(15_000m, result.Value.IndicativeQuote.PricePerPole);

        // Ensure no tracking token row was created for TenantOwner
        var tracking = await requestRepo.GetTrackingByRequestIdAsync(result.Value.Id);
        Assert.Null(tracking);
    }

    [Fact]
    public async Task TenantOwnerExistingFarmFollowUpWithoutPublishedBaselineRejectsWithFollowUpBaselineRequired()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm 1", "Address 1", 10m, 106.0, 10.0, 4326),
            hasBaseline: false); // Baseline NOT published!

        var ownerQuery = new FakeTenantOwnerReferenceQuery(new TenantOwnerRequestReference(
            TenantId, ActorId, "Owner Name", "owner@example.com", "0908888888"));

        var handler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-FOLLOWUP-FAIL"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1),
            new FakeV3FollowUpBaselineQuery(V3FollowUpBaselineEligibilityResult.Ineligible("No published base map.")));

        var command = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-followup-nobaseline",
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

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SurveyRequest.FollowUpBaselineRequired", result.Error.Code);
    }

    [Fact]
    public async Task TenantOwnerExistingFarmFollowUpWithPublishedBaselineSuccessfullyCreatesFollowUp()
    {
        using var context = CreateContext();
        var (service, bundle) = await SeedPoliciesAsync(context, enablePublicIntake: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(TenantId, FarmId, true, "Farm 1", "Address 1", 10m, 106.0, 10.0, 4326),
            hasBaseline: true); // Baseline published!

        var ownerQuery = new FakeTenantOwnerReferenceQuery(new TenantOwnerRequestReference(
            TenantId, ActorId, "Owner Name", "owner@example.com", "0908888888"));

        var handler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-FOLLOWUP-SUCCESS"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1),
            new FakeV3FollowUpBaselineQuery(V3FollowUpBaselineEligibilityResult.Eligible(Guid.NewGuid(), Guid.NewGuid(), 80)));

        var command = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-followup-success",
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

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("REQ-FOLLOWUP-SUCCESS", result.Value.RequestNumber);
        Assert.Equal(SurveyRequestKind.ExistingFarmSurvey, result.Value.Kind);
        Assert.Equal(FarmId, result.Value.FarmId);
        Assert.Equal(bundle.Id, result.Value.IndicativeQuote.BusinessPolicyVersionId);
        Assert.Equal(15_000m, result.Value.IndicativeQuote.PricePerPole);
    }

    [Fact]
    public async Task TenantOwnerExistingFarmFollowUpFarmNotOwnedByTenantRejectsWithForbidden()
    {
        using var context = CreateContext();
        var (service, _) = await SeedPoliciesAsync(context, enablePublicIntake: false);

        var requestRepo = new SurveyRequestRepository(context);
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var idempotencyResolver = new SurveyRequestIdempotencyResolver(requestRepo);

        var otherTenantId = Guid.NewGuid();
        var farmQuery = new FakeFarmReferenceQuery(
            farm: new SurveyRequestFarmReference(otherTenantId, FarmId, true, "Other Farm", "Address 2", 10m, 106.0, 10.0, 4326),
            hasBaseline: true);

        var ownerQuery = new FakeTenantOwnerReferenceQuery(new TenantOwnerRequestReference(
            TenantId, ActorId, "Owner Name", "owner@example.com", "0908888888"));

        var handler = new SubmitTenantOwnerSurveyRequestV3CommandHandler(
            farmQuery,
            ownerQuery,
            requestRepo,
            serviceRepo,
            policyRepo,
            idempotencyResolver,
            new FakeNumberGenerator("REQ-FOLLOWUP-FORBIDDEN"),
            new FakeOutbox(),
            context,
            new FakeExecutionContext(TenantId, ActorId),
            new FixedTimeProvider(T1),
            new FakeV3FollowUpBaselineQuery());

        var command = new SubmitTenantOwnerSurveyRequestV3Command(
            "key-followup-other-tenant",
            service.Id,
            FarmId: FarmId,
            FarmName: null,
            FarmAddress: null,
            ApproximateAreaHa: null,
            Longitude: null,
            Latitude: null,
            EstimatedPoleCount: 50,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SurveyRequest.FarmNotOwnedByTenant", result.Error.Code);
    }

    // --- Helpers / Fakes ---

    private sealed class FakeNumberGenerator(string requestNumber) : ISurveyRequestNumberGenerator
    {
        public string Create() => requestNumber;
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

    private sealed class FakeFarmReferenceQuery(
        SurveyRequestFarmReference? farm = null,
        bool hasBaseline = false) : ISurveyRequestFarmReferenceQuery
    {
        public Task<SurveyRequestFarmReference?> GetAsync(Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(farm);

        public Task<bool> HasPublishedBaseMapAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(hasBaseline);
    }

    private sealed class FakeTenantOwnerReferenceQuery(TenantOwnerRequestReference? owner = null) : ITenantOwnerRequestReferenceQuery
    {
        public Task<TenantOwnerRequestReference?> GetActiveOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(owner);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeV3FollowUpBaselineQuery(
        V3FollowUpBaselineEligibilityResult? result = null) : IV3FollowUpBaselineQuery
    {
        private readonly V3FollowUpBaselineEligibilityResult _result =
            result ?? V3FollowUpBaselineEligibilityResult.Eligible(Guid.NewGuid(), Guid.NewGuid(), 100);

        public Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }
}
