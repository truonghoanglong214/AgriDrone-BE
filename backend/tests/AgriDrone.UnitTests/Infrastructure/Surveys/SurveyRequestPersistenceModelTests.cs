using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Surveys;

public sealed class SurveyRequestPersistenceModelTests
{
    [Fact]
    public void ReviewIdIsApplicationGenerated()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new SurveysDbContext(options);
        var entityType = context.Model.FindEntityType(
            typeof(SurveyRequestReview));
        var id = entityType?.FindProperty(nameof(SurveyRequestReview.Id));

        Assert.NotNull(id);
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Null(id.GetDefaultValueSql());
    }

    [Fact]
    public void NewCustomerContextColumnsAreNullableAndNotPartOfAnAlternateKey()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new SurveysDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(SurveyRequest));

        Assert.NotNull(entityType);
        Assert.True(entityType.FindProperty(nameof(SurveyRequest.TenantId))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(SurveyRequest.FarmId))!.IsNullable);
        Assert.DoesNotContain(
            entityType.GetKeys(),
            key => !key.IsPrimaryKey());
    }

    [Fact]
    public void ActiveNewCustomerTenantCodeIsUniquelyReserved()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new SurveysDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(SurveyRequest));
        var tenantCode = entityType?.FindProperty(
            nameof(SurveyRequest.TenantCode));
        var index = entityType?.GetIndexes().Single(candidate =>
            candidate.GetDatabaseName() ==
                "ux_survey_requests_active_tenant_code");

        Assert.NotNull(tenantCode);
        Assert.True(tenantCode.IsNullable);
        Assert.Equal(50, tenantCode.GetMaxLength());
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Contains("APPROVED", index.GetFilter());
    }

    [Fact]
    public void OrderPersistsVersionedCompatibilityDecision()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new SurveysDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(SurveyOrder));
        var policyVersion = entityType?.FindProperty(
            nameof(SurveyOrder.PreviousOrderCompatibilityPolicyVersion));
        var reason = entityType?.FindProperty(
            nameof(SurveyOrder.PreviousOrderCompatibilityReason));

        Assert.NotNull(policyVersion);
        Assert.NotNull(reason);
        Assert.Equal(100, policyVersion.GetMaxLength());
        Assert.Equal(100, reason.GetMaxLength());
    }

    [Fact]
    public void CaptureAuthorizationHasOneRequirementPerOrderAndType()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new SurveysDbContext(options);
        var entityType = context.Model.FindEntityType(
            typeof(SurveyCaptureAuthorization));
        var index = entityType?.GetIndexes().Single(candidate =>
            candidate.GetDatabaseName() ==
                "uq_capture_authorizations_order_type");

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(
            [
                nameof(SurveyCaptureAuthorization.SurveyOrderId),
                nameof(SurveyCaptureAuthorization.Type)
            ],
            index.Properties.Select(property => property.Name));
    }
}
