using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Surveys;

public sealed class SurveyRequestPersistenceModelTests
{
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
}
