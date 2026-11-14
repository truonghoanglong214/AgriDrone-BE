using AgriDrone.Database;
using AgriDrone.Database.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class RegistrationV26MigrationTests
{
    [Fact]
    public void UpgradeOnlyAddsNullableSnapshotsAndLegacyDefaultFlag()
    {
        var migration = new RegistrationV26PostFlightSettlement();
        Assert.Equal(5, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation => Assert.IsType<AddColumnOperation>(operation));
        var columns = migration.UpOperations.Cast<AddColumnOperation>().ToArray();
        Assert.All(columns, column => Assert.Equal("survey_orders", column.Table));
        var flag = Assert.Single(columns, column => column.Name == "post_flight_settlement");
        Assert.False(flag.IsNullable);
        Assert.Equal(false, flag.DefaultValue);
        Assert.All(columns.Where(column => column != flag), column => Assert.True(column.IsNullable));
    }

    [Fact]
    public void ModelMatchesSnapshotAndIdempotentScriptPreservesExistingData()
    {
        var builder = new NpgsqlDataSourceBuilder("Host=127.0.0.1;Database=unused;Username=unused;Password=unused");
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        using var source = builder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, source);
        using var context = new AgriDroneSchemaDbContext(options.Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var script = context.GetService<IMigrator>().GenerateScript(
            "20261112141351_ValidateSurveyComboDiscountRange",
            "20261113075731_RegistrationV26PostFlightSettlement",
            MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("ADD booked_price_per_pole", script);
        Assert.Contains("ADD post_flight_settlement boolean NOT NULL DEFAULT FALSE", script);
        Assert.Contains("IF NOT EXISTS", script);
        Assert.DoesNotContain("DROP TABLE", script);
        Assert.DoesNotContain("DROP COLUMN", script);
        Assert.DoesNotContain("UPDATE survey.survey_orders", script);
        Assert.DoesNotContain("DELETE FROM survey.survey_orders", script);
    }
}
