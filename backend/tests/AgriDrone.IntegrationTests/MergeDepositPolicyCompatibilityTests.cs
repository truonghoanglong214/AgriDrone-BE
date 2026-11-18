using AgriDrone.Database;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AgriDrone.IntegrationTests;

// Schema/model and migration script checks only; never connects to PostgreSQL.
public sealed class MergeDepositPolicyCompatibilityTests
{
    private static AgriDroneSchemaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=merge_model;Username=test;Password=test",
                options => options.UseNetTopologySuite())
            .Options);

    [Fact]
    public void LegacyPoliciesCannotBeReadAsV3PolicyHistory()
    {
        using var context = CreateContext();
        var legacy = context.Model.FindEntityType(typeof(LegacySurveyDepositPolicy))!;
        var v3 = context.Model.FindEntityType(typeof(SurveyDepositPolicy))!;
        Assert.Equal("survey_deposit_policies", legacy.GetTableName());
        Assert.Equal("v3_survey_deposit_policies", v3.GetTableName());
        Assert.Null(legacy.FindProperty(nameof(SurveyDepositPolicy.PredecessorVersionId)));
        Assert.Null(v3.FindProperty(nameof(LegacySurveyDepositPolicy.AmountVnd)));

        var v3Sql = context.Set<SurveyDepositPolicy>().ToQueryString();
        Assert.Contains("v3_survey_deposit_policies", v3Sql);
        Assert.DoesNotContain("amount_vnd", v3Sql);
        var legacySql = context.Set<LegacySurveyDepositPolicy>().ToQueryString();
        Assert.Contains("survey_deposit_policies", legacySql);
        Assert.DoesNotContain("v3_survey_deposit_policies", legacySql);

        var bundle = context.Model.FindEntityType(typeof(BusinessPolicyVersion))!;
        var depositFk = Assert.Single(bundle.GetForeignKeys(), fk =>
            fk.Properties.Any(property => property.Name == nameof(BusinessPolicyVersion.SurveyDepositPolicyId)));
        Assert.Equal(typeof(SurveyDepositPolicy), depositFk.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void MergeBridgesRunAroundStep8BeforeMainPolicyMigrations()
    {
        using var context = CreateContext();
        var migrations = context.Database.GetMigrations().ToList();
        var before = migrations.IndexOf("20261111040000_StageLegacyDepositPoliciesForMerge");
        var foundation = migrations.IndexOf("20261111041444_Step8_V3CommercialPolicyFoundation");
        var snapshots = migrations.IndexOf("20261111061205_Step8D_V3OrderCommercialSnapshotAndHistoryFoundation");
        var after = migrations.IndexOf("20261111150000_SeparateLegacyAndV3DepositPolicies");
        var main = migrations.IndexOf("20261112121158_AddSurveyComboDiscount");
        Assert.True(before >= 0 && before < foundation && foundation < snapshots && snapshots < after && after < main);

        var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("legacy_survey_deposit_policies_merge", script);
        Assert.Contains("v3_survey_deposit_policies", script);
        Assert.Contains("RENAME TO", script);
    }
}
