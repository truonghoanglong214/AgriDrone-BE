using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.IntegrationTests;

// These component tests deliberately require no database. They verify the real
// production adapters fail closed; they are not PostgreSQL acceptance evidence.
public sealed class V3BaselineDeliveryProofTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    [Fact]
    public async Task ProductionIntakeFailsClosedWhenReleaseStoreIsNotImplemented()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IV3FollowUpBaselineQuery>();

        var result = await query.GetEligibleAsync(TenantId, FarmId);

        Assert.False(result.IsEligible);
        Assert.Null(result.BaseMapVersionId);
        Assert.Null(result.SourceOrderId);
        Assert.Contains("release evidence is not available", result.RejectionReason);
    }

    [Fact]
    public async Task ApprovalRequiresSpecializedTransactionEvenWhenReleaseStoreIsUnavailable()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<SurveyApprovalReferenceQueries>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            query.GetV3FollowUpBaselineForApprovalAsync(TenantId, FarmId));

        Assert.Contains("active specialized transaction", exception.Message);
    }

    [Theory]
    [InlineData("withdrawn")]
    [InlineData("unreleased")]
    [InlineData("unsettled")]
    [InlineData("other-tenant")]
    [InlineData("other-farm")]
    [InlineData("missing-release")]
    [InlineData("missing-map")]
    [InlineData("missing-order")]
    public async Task InvalidDeliveryEvidenceCannotOpenFollowUp(string invalidEvidence)
    {
        var proof = ValidProof();
        proof = invalidEvidence switch
        {
            "withdrawn" => proof with { IsWithdrawn = true },
            "unreleased" => proof with { IsReleased = false },
            "unsettled" => proof with { FinanciallySettledAtRelease = false },
            "other-tenant" => proof with { TenantId = Guid.NewGuid() },
            "other-farm" => proof with { FarmId = Guid.NewGuid() },
            "missing-release" => proof with { ReleaseId = Guid.Empty },
            "missing-map" => proof with { BaseMapVersionId = Guid.Empty },
            "missing-order" => proof with { SourceOrderId = Guid.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidEvidence))
        };
        var proofReader = new FixedProofReader(proof);
        var reader = new V3FollowUpBaselineEligibilityReader(proofReader);
        // No provider: touching a candidate query would throw rather than let
        // invalid evidence silently pass through another database read.
        using var context = new DbContext(new DbContextOptions<DbContext>());

        var result = await reader.GetEligibleAsync(context, TenantId, FarmId);

        Assert.False(result.IsEligible);
        Assert.Null(result.BaseMapVersionId);
        Assert.Same(context, proofReader.ReceivedContext);
    }

    [Fact]
    public void SettledReleaseEvidenceMatchesOnlyItsOwnTenantAndFarm()
    {
        var proof = ValidProof();

        Assert.True(proof.AllowsFollowUp(TenantId, FarmId));
        Assert.False(proof.AllowsFollowUp(Guid.NewGuid(), FarmId));
        Assert.False(proof.AllowsFollowUp(TenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ValidReleaseEvidenceStillRequiresReadingCurrentBaselineAndCount()
    {
        var reader = new V3FollowUpBaselineEligibilityReader(new FixedProofReader(ValidProof()));
        using var context = new DbContext(new DbContextOptions<DbContext>());

        // A proof alone cannot produce Eligible: candidate verification must
        // run, and provider/read failures must propagate, not become success.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.GetEligibleAsync(context, TenantId, FarmId));
    }

    [Fact]
    public async Task UnavailableProofSourcePreservesCancellation()
    {
        var reader = new V3FollowUpBaselineEligibilityReader(new UnavailableV3BaselineDeliveryProofReader());
        using var context = new DbContext(new DbContextOptions<DbContext>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.GetEligibleAsync(context, TenantId, FarmId, cancellation.Token));
    }

    private static V3BaselineDeliveryProof ValidProof() =>
        new(TenantId, FarmId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            IsReleased: true, FinanciallySettledAtRelease: true, IsWithdrawn: false);

    private static IServiceCollection CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No server is needed: fail-closed intake must not query it.
                ["ConnectionStrings:AgriDrone"] =
                    "Host=127.0.0.1;Port=1;Database=v3_proof_test;Username=test;Password=test;Timeout=1"
            })
            .Build();
        var services = new ServiceCollection()
            .AddAgriDroneDatabase(configuration)
            .AddSurveyApprovalPersistence(configuration);
        services.AddScoped<SurveyApprovalReferenceQueries>();
        return services;
    }

    private sealed class FixedProofReader(V3BaselineDeliveryProof proof) : IV3BaselineDeliveryProofReader
    {
        public DbContext? ReceivedContext { get; private set; }

        public Task<V3BaselineDeliveryProof?> ReadCurrentAsync(
            DbContext context,
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default)
        {
            ReceivedContext = context;
            return Task.FromResult<V3BaselineDeliveryProof?>(proof);
        }
    }
}
