using AgriDrone.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261015010000_BackfillSelectedSegmentJson")]
public partial class BackfillSelectedSegmentJson : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE mission.mission_telemetry_imports AS telemetry_import
            SET selected_segment_json = jsonb_build_object(
                'segmentIndex', NULL,
                'points', (
                    SELECT jsonb_agg(
                        jsonb_build_object(
                            'timestamp', to_char(
                                point.recorded_at AT TIME ZONE 'Asia/Bangkok',
                                'YYYY-MM-DD HH24:MI:SS'),
                            'lat', trunc(ST_Y(point.location)::numeric, 6),
                            'lon', trunc(ST_X(point.location)::numeric, 6),
                            'alt', point.altitude_m
                        ) ORDER BY point.sequence_number)
                    FROM mission.mission_telemetry_points AS point
                    WHERE point.mission_id = telemetry_import.mission_id
                )
            )
            WHERE telemetry_import.selected_segment_json IS NULL
              AND telemetry_import.point_count = (
                  SELECT count(*)
                  FROM mission.mission_telemetry_points AS point
                  WHERE point.mission_id = telemetry_import.mission_id
              )
              AND telemetry_import.first_recorded_at = (
                  SELECT min(point.recorded_at)
                  FROM mission.mission_telemetry_points AS point
                  WHERE point.mission_id = telemetry_import.mission_id
              )
              AND telemetry_import.last_recorded_at = (
                  SELECT max(point.recorded_at)
                  FROM mission.mission_telemetry_points AS point
                  WHERE point.mission_id = telemetry_import.mission_id
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep recovered telemetry JSON when rolling back this data migration.
    }
}
