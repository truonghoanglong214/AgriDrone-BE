using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261128192000_V3UpdateComboZoneInferencePolicyDefaults")]
public sealed class V3UpdateComboZoneInferencePolicyDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE mission.ai_model_versions
            SET metrics = jsonb_set(
                    metrics,
                    '{comboInferencePolicy}',
                    jsonb_build_object(
                        'version', 'combo-v2',
                        'confidence', COALESCE((metrics -> 'comboInferencePolicy' ->> 'confidence')::numeric, 0.25),
                        'clusterRadiusMeters', 4.5,
                        'bufferMeters', 1.5,
                        'minClusterTrees', 3
                    )
                ),
                updated_at = now()
            WHERE model_type = 'DISEASE_DETECTION'
              AND jsonb_typeof(metrics) = 'object'
              AND metrics -> 'comboInferencePolicy' ->> 'version' = 'combo-v1'
              AND (metrics -> 'comboInferencePolicy' ->> 'clusterRadiusMeters')::numeric = 15
              AND (metrics -> 'comboInferencePolicy' ->> 'bufferMeters')::numeric = 3;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE mission.ai_model_versions
            SET metrics = jsonb_set(
                    metrics,
                    '{comboInferencePolicy}',
                    jsonb_build_object(
                        'version', 'combo-v1',
                        'confidence', COALESCE((metrics -> 'comboInferencePolicy' ->> 'confidence')::numeric, 0.25),
                        'clusterRadiusMeters', 15.0,
                        'bufferMeters', 3.0
                    )
                ),
                updated_at = now()
            WHERE model_type = 'DISEASE_DETECTION'
              AND jsonb_typeof(metrics) = 'object'
              AND metrics -> 'comboInferencePolicy' ->> 'version' = 'combo-v2'
              AND (metrics -> 'comboInferencePolicy' ->> 'clusterRadiusMeters')::numeric = 4.5
              AND (metrics -> 'comboInferencePolicy' ->> 'bufferMeters')::numeric = 1.5;
            """);
    }
}
