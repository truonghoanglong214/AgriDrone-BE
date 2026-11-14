using AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicCatalogueV3;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.Modules.Surveys.Infrastructure.Queries;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class PublicCatalogueV3Tests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();

    private static SurveysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"surveys-catalogue-v3-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SurveysDbContext(options);
    }

    [Fact]
    public async Task ReturnsNullWhenNoActivationConfigured()
    {
        using var context = CreateContext();
        var queries = new SurveyCatalogueQueries(context);
        var handler = new GetPublicCatalogueV3QueryHandler(queries, new FixedTimeProvider(T1));

        var result = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ReturnsConsistentV3CatalogueWithMapAndComboServices()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);

        // 1. Create MAP service and COMBO service
        var mapService = SurveyService.CreateV3("MAP", "Orthophoto Mapping", "Boundary mapping", T0);
        mapService.Activate(T0, expectedVersion: 0);
        var comboService = SurveyService.CreateV3("COMBO", "Map and Disease Combo", "Combined mapping and health", T0);
        comboService.Activate(T0, expectedVersion: 0);
        serviceRepo.Add(mapService);
        serviceRepo.Add(comboService);

        // 2. Revisions
        var mapRev = SurveyServiceRevision.Create(
            Guid.NewGuid(), mapService.Id, 1, null, "MAP Revision 1", "Map desc", true, false, T0, null, ActorId, T0, "INIT");
        var comboRev = SurveyServiceRevision.Create(
            Guid.NewGuid(), comboService.Id, 1, null, "COMBO Revision 1", "Combo desc", true, true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddRevision(mapRev);
        policyRepo.AddRevision(comboRev);

        // 3. Prices
        var mapPrice = SurveyServicePrice.CreatePerPoleV3(
            Guid.NewGuid(), mapService.Id, 1, null, 15_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        var comboPrice = SurveyServicePrice.CreatePerPoleV3(
            Guid.NewGuid(), comboService.Id, 1, null, 25_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddPriceV3(mapPrice);
        policyRepo.AddPriceV3(comboPrice);

        // 4. Deposit Policy
        var deposit = SurveyDepositPolicy.Create(
            Guid.NewGuid(), 1, null, 100_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddDepositPolicy(deposit);

        // 5. Claim Policy
        var claim = ClaimPolicy.Create(
            Guid.NewGuid(), 1, null, 7, "Asia/Ho_Chi_Minh", true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddClaimPolicy(claim);

        // 6. Cancellation Policy
        var cancellation = CancellationPolicy.Create(
            Guid.NewGuid(), 1, null, true, true, 1, true, true, T0, null, ActorId, T0, "INIT");
        policyRepo.AddCancellationPolicy(cancellation);

        // 7. Bundle Version
        var bundle = BusinessPolicyVersion.Create(
            Guid.NewGuid(), "DEFAULT_COMMERCIAL_BUNDLE", 1, null, deposit.Id, claim.Id, cancellation.Id,
            [mapRev.Id, comboRev.Id], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        // 8. Activation (PublicIntakeEnabled = false by default)
        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), ActorId, T0, "INIT");
        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);

        await context.SaveChangesAsync();

        var queries = new SurveyCatalogueQueries(context);
        var handler = new GetPublicCatalogueV3QueryHandler(queries, new FixedTimeProvider(T1));

        var result = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var catalogue = result.Value;
        Assert.NotNull(catalogue);

        // Verify bookability is false because PublicIntakeEnabled is false
        Assert.False(catalogue.IsBookable);
        Assert.Equal(T1, catalogue.EvaluatedAt);

        // Verify shared deposit
        Assert.Equal(deposit.Id, catalogue.SharedDeposit.DepositPolicyId);
        Assert.Equal(1, catalogue.SharedDeposit.VersionNumber);
        Assert.Equal(100_000m, catalogue.SharedDeposit.Amount);
        Assert.Equal("VND", catalogue.SharedDeposit.Currency);

        // Verify policy summary
        Assert.Equal(bundle.Id, catalogue.PolicySummary.BundleVersionId);
        Assert.Equal(1, catalogue.PolicySummary.BundleVersionNumber);
        Assert.Equal(7, catalogue.PolicySummary.ClaimWindowDays);
        Assert.Equal("Asia/Ho_Chi_Minh", catalogue.PolicySummary.ClaimTimeZoneId);
        Assert.True(catalogue.PolicySummary.ExcludeNoticeDate);
        Assert.True(catalogue.PolicySummary.RefundableBeforeCapture);
        Assert.True(catalogue.PolicySummary.RetainDepositAfterCapture);
        Assert.Equal(1, catalogue.PolicySummary.AllowedFreeNoShowReschedules);

        // Verify services (MAP and COMBO)
        Assert.Equal(2, catalogue.Services.Count);

        var mapItem = Assert.Single(catalogue.Services, s => s.Code == "MAP");
        Assert.Equal(mapService.Id, mapItem.SurveyServiceId);
        Assert.Equal(mapRev.Id, mapItem.RevisionId);
        Assert.Equal(15_000m, mapItem.PricePerPole);
        Assert.Equal("VND", mapItem.Currency);
        Assert.True(mapItem.MapsPoles);
        Assert.False(mapItem.ScreensDisease);
        Assert.Contains("Disease screening is not assessed in this service tier.", mapItem.PreliminaryLimitations);

        var comboItem = Assert.Single(catalogue.Services, s => s.Code == "COMBO");
        Assert.Equal(comboService.Id, comboItem.SurveyServiceId);
        Assert.Equal(comboRev.Id, comboItem.RevisionId);
        Assert.Equal(25_000m, comboItem.PricePerPole);
        Assert.True(comboItem.MapsPoles);
        Assert.True(comboItem.ScreensDisease);
        Assert.Contains("Findings require expert agronomist review before confirmation.", comboItem.PreliminaryLimitations);
    }

    [Fact]
    public async Task IsBookableOnlyTrueWhenIntakeCaptureAndReleaseAllEnabled()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);

        var mapService = SurveyService.CreateV3("MAP", "Map", "Desc", T0);
        mapService.Activate(T0, expectedVersion: 0);
        serviceRepo.Add(mapService);
        var mapRev = SurveyServiceRevision.Create(
            Guid.NewGuid(), mapService.Id, 1, null, "MAP Rev", "Desc", true, false, T0, null, ActorId, T0, "INIT");
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
            Guid.NewGuid(), "DEFAULT", 1, null, deposit.Id, claim.Id, cancellation.Id,
            [mapRev.Id], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), ActorId, T0, "INIT");
        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);
        await context.SaveChangesAsync();

        var queries = new SurveyCatalogueQueries(context);
        var handler = new GetPublicCatalogueV3QueryHandler(queries, new FixedTimeProvider(T1));

        // 1. Default: PublicIntakeEnabled = false -> IsBookable is false
        var res1 = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);
        Assert.NotNull(res1.Value);
        Assert.False(res1.Value.IsBookable);

        // 2. Enable intake and capture, but keep release disabled -> IsBookable MUST still be false (BR-GAT-03)
        var decision2 = activation.ApplyDecision(
            Guid.NewGuid(), bundle.Id, publicIntakeEnabled: true, captureEnabled: true, releaseEnabled: false,
            ActorId, T1, "ENABLE_WITHOUT_RELEASE");
        policyRepo.AddActivationDecision(decision2);
        await context.SaveChangesAsync();

        var res2 = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);
        Assert.NotNull(res2.Value);
        Assert.False(res2.Value.IsBookable);

        // 3. Enable intake, capture AND release -> IsBookable is finally true
        var decision3 = activation.ApplyDecision(
            Guid.NewGuid(), bundle.Id, publicIntakeEnabled: true, captureEnabled: true, releaseEnabled: true,
            ActorId, T1.AddMinutes(5), "ENABLE_ALL");
        policyRepo.AddActivationDecision(decision3);
        await context.SaveChangesAsync();

        var res3 = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);
        Assert.NotNull(res3.Value);
        Assert.True(res3.Value.IsBookable);
    }

    [Fact]
    public async Task IgnoresRetiredServicesAndInactivePrices()
    {
        using var context = CreateContext();
        var serviceRepo = new SurveyServiceRepository(context);
        var policyRepo = new BusinessPolicyRepository(context);

        // Create an experimental/retired service
        var retiredService = SurveyService.CreateV3("RETIRED", "Retired Service", "Desc", T0);
        retiredService.Retire(T0, expectedVersion: 0);
        serviceRepo.Add(retiredService);

        var rev = SurveyServiceRevision.Create(
            Guid.NewGuid(), retiredService.Id, 1, null, "Rev 1", "Desc", true, false, T0, null, ActorId, T0, "INIT");
        policyRepo.AddRevision(rev);
        var price = SurveyServicePrice.CreatePerPoleV3(
            Guid.NewGuid(), retiredService.Id, 1, null, 10_000m, CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddPriceV3(price);

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
            Guid.NewGuid(), "DEFAULT", 1, null, deposit.Id, claim.Id, cancellation.Id,
            [rev.Id], CurrencyCode.Vnd, T0, null, ActorId, T0, "INIT");
        policyRepo.AddBundleVersion(bundle);

        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            bundle.Id, Guid.NewGuid(), ActorId, T0, "INIT");
        policyRepo.AddActivation(activation);
        policyRepo.AddActivationDecision(initialDecision);
        await context.SaveChangesAsync();

        var queries = new SurveyCatalogueQueries(context);
        var handler = new GetPublicCatalogueV3QueryHandler(queries, new FixedTimeProvider(T1));

        var res = await handler.Handle(new GetPublicCatalogueV3Query(), CancellationToken.None);
        Assert.NotNull(res.Value);
        // Retired service is not returned in public catalogue
        Assert.Empty(res.Value.Services);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
