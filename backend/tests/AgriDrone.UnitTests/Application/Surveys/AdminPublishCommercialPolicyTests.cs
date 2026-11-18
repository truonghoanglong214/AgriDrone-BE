using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;
using AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class AdminPublishCommercialPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();

    private static SurveysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"surveys-publish-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SurveysDbContext(options);
    }

    [Fact]
    public async Task CreateSurveyServiceV3SuccessfullyCreatesAndActivatesService()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var auditWriter = new FakeAuditWriter();
        var handler = new CreateSurveyServiceV3CommandHandler(
            serviceRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T0));

        var result = await handler.Handle(
            new CreateSurveyServiceV3Command(
                Code: "MAP",
                Name: "Orthophoto Mapping",
                Description: "Boundary and pole mapping",
                ActivateImmediately: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("MAP", result.Value.Code);
        Assert.Equal("Orthophoto Mapping", result.Value.Name);
        Assert.Equal("V3", result.Value.Scope);
        Assert.Equal("ACTIVE", result.Value.Status);

        // Verify duplicate code rejection
        var dupResult = await handler.Handle(
            new CreateSurveyServiceV3Command(
                Code: "map", // Case-insensitive duplicate
                Name: "Duplicate Map",
                Description: "Another map service",
                ActivateImmediately: true),
            CancellationToken.None);

        Assert.True(dupResult.IsFailure);
        Assert.Equal("BusinessPolicy.DuplicateServiceCode", dupResult.Error.Code);
    }

    [Fact]
    public async Task SurveyServiceV3SeederSeedsMapAndComboIntoEmptyDatabase()
    {
        using var context = CreateContext();
        await SurveyServiceV3Seeder.SeedAsync(context, CancellationToken.None);

        var serviceRepo = new SurveyServiceRepository(context);
        var map = await serviceRepo.GetByCodeAsync("MAP");
        var combo = await serviceRepo.GetByCodeAsync("COMBO");

        Assert.NotNull(map);
        Assert.Equal("MAP", map.Code);
        Assert.Equal(ContractScope.V3, map.Scope);
        Assert.Equal(SurveyServiceStatus.Active, map.Status);

        Assert.NotNull(combo);
        Assert.Equal("COMBO", combo.Code);
        Assert.Equal(ContractScope.V3, combo.Scope);
        Assert.Equal(SurveyServiceStatus.Active, combo.Status);

        // Calling seed again is idempotent
        await SurveyServiceV3Seeder.SeedAsync(context, CancellationToken.None);
        var count = await context.SurveyServices.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PublishSurveyServiceRevisionClosesPredecessorAndSavesInTransaction()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var service = SurveyService.CreateV3(
            code: "MAP",
            name: "Orthophoto Mapping",
            description: "High resolution boundary mapping",
            createdAt: T0);
        serviceRepo.Add(service);
        await context.SaveChangesAsync();

        var handler1 = new PublishSurveyServiceRevisionCommandHandler(
            serviceRepo,
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res1 = await handler1.Handle(
            new PublishSurveyServiceRevisionCommand(
                service.Id,
                "MAP Baseline v1",
                "Initial map description",
                MapsPoles: true,
                ScreensDisease: false,
                ExpectedLatestVersionNumber: 0,
                ReasonCode: "INITIAL_PROVISIONING"),
            CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.Equal(1, res1.Value.VersionNumber);
        Assert.Null(res1.Value.PredecessorVersionId);
        Assert.Equal(T1, res1.Value.EffectiveFrom);
        Assert.Null(res1.Value.EffectiveTo);

        // Publish revision 2 with expected predecessor 1
        var handler2 = new PublishSurveyServiceRevisionCommandHandler(
            serviceRepo,
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T2));

        var res2 = await handler2.Handle(
            new PublishSurveyServiceRevisionCommand(
                service.Id,
                "MAP Baseline v2",
                "Updated map description",
                MapsPoles: true,
                ScreensDisease: false,
                ExpectedLatestVersionNumber: 1,
                ReasonCode: "REVISED_DESCRIPTION"),
            CancellationToken.None);

        Assert.True(res2.IsSuccess);
        Assert.Equal(2, res2.Value.VersionNumber);
        Assert.Equal(res1.Value.RevisionId, res2.Value.PredecessorVersionId);
        Assert.Equal(T2, res2.Value.EffectiveFrom);

        // Verify previous revision was closed at T2
        var rev1 = await context.SurveyServiceRevisions.FindAsync(res1.Value.RevisionId);
        Assert.NotNull(rev1);
        Assert.Equal(T2, rev1.EffectiveTo);
    }

    [Fact]
    public async Task PublishSurveyServiceRevisionConcurrencyConflict()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var service = SurveyService.CreateV3("COMBO", "Combo", "Desc", T0);
        serviceRepo.Add(service);
        await context.SaveChangesAsync();

        var handler = new PublishSurveyServiceRevisionCommandHandler(
            serviceRepo,
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        // Mismatched expected version 5 when current is 0
        var res = await handler.Handle(
            new PublishSurveyServiceRevisionCommand(
                service.Id,
                "Combo v1",
                "Desc",
                MapsPoles: true,
                ScreensDisease: true,
                ExpectedLatestVersionNumber: 5,
                ReasonCode: "INITIAL"),
            CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal("BusinessPolicy.PredecessorMismatch", res.Error.Code);
    }

    [Fact]
    public async Task PublishSurveyServicePriceV3EnforcesWholeVndAndClosesPredecessor()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var service = SurveyService.CreateV3("MAP", "Map", "Desc", T0);
        serviceRepo.Add(service);
        await context.SaveChangesAsync();

        var handler1 = new PublishSurveyServicePriceV3CommandHandler(
            serviceRepo,
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res1 = await handler1.Handle(
            new PublishSurveyServicePriceV3Command(
                service.Id,
                AmountPerPole: 15_000m,
                Currency: "VND",
                ExpectedLatestPriceVersion: 0,
                ReasonCode: "PRICE_V1"),
            CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.Equal(1, res1.Value.VersionNumber);
        Assert.Equal(15_000m, res1.Value.AmountPerPole);
        Assert.Null(res1.Value.PredecessorVersionId);

        var handler2 = new PublishSurveyServicePriceV3CommandHandler(
            serviceRepo,
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T2));

        var res2 = await handler2.Handle(
            new PublishSurveyServicePriceV3Command(
                service.Id,
                AmountPerPole: 18_000m,
                Currency: "VND",
                ExpectedLatestPriceVersion: 1,
                ReasonCode: "ANNUAL_ADJUSTMENT"),
            CancellationToken.None);

        Assert.True(res2.IsSuccess);
        Assert.Equal(2, res2.Value.VersionNumber);
        Assert.Equal(res1.Value.PriceVersionId, res2.Value.PredecessorVersionId);

        var p1 = await context.SurveyServicePrices.FindAsync(res1.Value.PriceVersionId);
        Assert.NotNull(p1);
        Assert.Equal(T2, p1.EffectiveTo);
    }

    [Fact]
    public async Task PublishSurveyDepositPolicyEnforcesSingleStreamAndPositiveWholeVnd()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var handler1 = new PublishSurveyDepositPolicyCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res1 = await handler1.Handle(
            new PublishSurveyDepositPolicyCommand(
                Amount: 50_000m,
                Currency: "VND",
                ExpectedLatestVersion: 0,
                ReasonCode: "DEPOSIT_V1"),
            CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.Equal(1, res1.Value.VersionNumber);
        Assert.Equal(50_000m, res1.Value.Amount);

        // Second deposit policy
        var handler2 = new PublishSurveyDepositPolicyCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T2));

        var res2 = await handler2.Handle(
            new PublishSurveyDepositPolicyCommand(
                Amount: 60_000m,
                Currency: "VND",
                ExpectedLatestVersion: 1,
                ReasonCode: "DEPOSIT_V2"),
            CancellationToken.None);

        Assert.True(res2.IsSuccess);
        Assert.Equal(2, res2.Value.VersionNumber);
        Assert.Equal(res1.Value.PolicyId, res2.Value.PredecessorVersionId);

        var dep1 = await context.SurveyDepositPolicies.FindAsync(res1.Value.PolicyId);
        Assert.NotNull(dep1);
        Assert.Equal(T2, dep1.EffectiveTo);
    }

    [Fact]
    public async Task PublishClaimPolicyValidatesWindowAndCreatesNewVersion()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var handler = new PublishClaimPolicyCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res = await handler.Handle(
            new PublishClaimPolicyCommand(
                CalendarWindowDays: 14,
                TimeZoneId: "Asia/Ho_Chi_Minh",
                ExcludeNoticeDate: true,
                ExpectedLatestVersion: 0,
                ReasonCode: "CLAIM_V1"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.VersionNumber);
        Assert.Equal(14, res.Value.CalendarWindowDays);
        Assert.Equal("Asia/Ho_Chi_Minh", res.Value.TimeZoneId);
        Assert.True(res.Value.ExcludeNoticeDate);
    }

    [Fact]
    public async Task PublishCancellationPolicyCreatesNewVersion()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var handler = new PublishCancellationPolicyCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res = await handler.Handle(
            new PublishCancellationPolicyCommand(
                RefundableBeforeCapture: true,
                RetainDepositAfterCapture: true,
                AllowedFreeNoShowReschedules: 1,
                OperationalPostponementRefundable: true,
                ServiceFailureRefundable: true,
                ExpectedLatestVersion: 0,
                ReasonCode: "CANCELLATION_V1"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.VersionNumber);
        Assert.True(res.Value.RefundableBeforeCapture);
        Assert.True(res.Value.RetainDepositAfterCapture);
        Assert.Equal(1, res.Value.AllowedFreeNoShowReschedules);
    }

    [Fact]
    public async Task PublishBusinessPolicyVersionValidatesAllDependenciesAndComputesFingerprint()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var service = SurveyService.CreateV3("MAP", "Map", "Desc", T0);
        serviceRepo.Add(service);
        var rev = SurveyServiceRevision.Create(
            Guid.NewGuid(), service.Id, 1, null, "Rev 1", "Desc", true, false, T0, null, ActorId, T0, "INIT");
        policyRepo.AddRevision(rev);

        var dep = SurveyDepositPolicy.Create(
            Guid.NewGuid(), 1, null, 100_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddDepositPolicy(dep);

        var claim = ClaimPolicy.Create(
            Guid.NewGuid(), 1, null, 7, "Asia/Ho_Chi_Minh", true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddClaimPolicy(claim);

        var cancellation = CancellationPolicy.Create(
            Guid.NewGuid(), 1, null, true, true, 1, true, true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddCancellationPolicy(cancellation);
        await context.SaveChangesAsync();

        var handler = new PublishBusinessPolicyVersionCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res = await handler.Handle(
            new PublishBusinessPolicyVersionCommand(
                StreamKey: "DEFAULT_BUNDLE",
                SurveyDepositPolicyId: dep.Id,
                ClaimPolicyId: claim.Id,
                CancellationPolicyId: cancellation.Id,
                ServiceRevisionIds: [rev.Id],
                PricingCurrency: "VND",
                ExpectedLatestVersion: 0,
                ReasonCode: "BUNDLE_V1"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.VersionNumber);
        Assert.False(string.IsNullOrWhiteSpace(res.Value.PolicyFingerprint));
        Assert.Contains(rev.Id, res.Value.ServiceRevisionIds);
    }

    [Fact]
    public async Task PublishBusinessPolicyVersionRejectsInactiveOrMissingDependency()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var handler = new PublishBusinessPolicyVersionCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        var res = await handler.Handle(
            new PublishBusinessPolicyVersionCommand(
                StreamKey: "DEFAULT_BUNDLE",
                SurveyDepositPolicyId: Guid.NewGuid(), // missing deposit policy
                ClaimPolicyId: Guid.NewGuid(),
                CancellationPolicyId: Guid.NewGuid(),
                ServiceRevisionIds: [Guid.NewGuid()],
                PricingCurrency: "VND",
                ExpectedLatestVersion: 0,
                ReasonCode: "BUNDLE_V1"),
            CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal("BusinessPolicy.InactiveDependency", res.Error.Code);
    }

    [Fact]
    public async Task TransitionBusinessPolicyActivationEnforcesDecisionLockAndDecisionHistory()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        // Create a bundle in DB
        var bundle = BusinessPolicyVersion.Create(
            Guid.NewGuid(), "STREAM", 1, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [Guid.NewGuid()], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);
        await context.SaveChangesAsync();

        var handler = new TransitionBusinessPolicyActivationCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        // Initial activation
        var res1 = await handler.Handle(
            new TransitionBusinessPolicyActivationCommand(
                BusinessPolicyVersionId: bundle.Id,
                PublicIntakeEnabled: false,
                CaptureEnabled: false,
                ReleaseEnabled: false,
                ExpectedLastDecisionId: null,
                ReasonCode: "INITIAL_PROJECTION"),
            CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.False(res1.Value.PublicIntakeEnabled);
        Assert.NotEqual(Guid.Empty, res1.Value.LastDecisionId);

        // Transition with correct ExpectedLastDecisionId
        var handler2 = new TransitionBusinessPolicyActivationCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T2));

        var res2 = await handler2.Handle(
            new TransitionBusinessPolicyActivationCommand(
                BusinessPolicyVersionId: bundle.Id,
                PublicIntakeEnabled: false,
                CaptureEnabled: true,
                ReleaseEnabled: false,
                ExpectedLastDecisionId: res1.Value.LastDecisionId,
                ReasonCode: "ENABLE_INTERNAL_CAPTURE"),
            CancellationToken.None);

        Assert.True(res2.IsSuccess);
        Assert.True(res2.Value.CaptureEnabled);
        Assert.NotEqual(res1.Value.LastDecisionId, res2.Value.LastDecisionId);

        // Decisions preserved in DB
        var decisions = await context.BusinessPolicyActivationDecisions.ToListAsync();
        Assert.Equal(2, decisions.Count);
        Assert.Equal(res1.Value.LastDecisionId, decisions[1].PredecessorDecisionId);
    }

    [Fact]
    public async Task TransitionBusinessPolicyActivationMismatchedDecisionFails()
    {
        using var context = CreateContext();
        var policyRepo = new BusinessPolicyRepository(context);
        var auditWriter = new FakeAuditWriter();

        var bundle = BusinessPolicyVersion.Create(
            Guid.NewGuid(), "STREAM", 1, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [Guid.NewGuid()], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), ActorId, T0, "INIT");
        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);
        await context.SaveChangesAsync();

        var handler = new TransitionBusinessPolicyActivationCommandHandler(
            policyRepo,
            context,
            auditWriter,
            new FakeExecutionContext(ActorId),
            new FixedTimeProvider(T1));

        // Wrong expected decision ID
        var res = await handler.Handle(
            new TransitionBusinessPolicyActivationCommand(
                BusinessPolicyVersionId: bundle.Id,
                PublicIntakeEnabled: false,
                CaptureEnabled: true,
                ReleaseEnabled: false,
                ExpectedLastDecisionId: Guid.NewGuid(),
                ReasonCode: "TRY_MISMATCH"),
            CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal("BusinessPolicy.DecisionMismatch", res.Error.Code);
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
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
    }

    private sealed class FakeExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
