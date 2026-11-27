using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261127120000_MissionYoloJobs")]
public sealed class MissionYoloJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE mission.tiling_jobs ADD COLUMN kind text NOT NULL DEFAULT 'TILING'
            CHECK (kind IN ('TILING', 'YOLO'));
        DROP INDEX mission.uq_tiling_jobs_active_mission;
        CREATE UNIQUE INDEX uq_tiling_jobs_active_mission_kind
            ON mission.tiling_jobs (mission_id, kind) WHERE status IN ('QUEUED', 'RUNNING');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM mission.tiling_jobs WHERE kind = 'YOLO';
        DROP INDEX mission.uq_tiling_jobs_active_mission_kind;
        CREATE UNIQUE INDEX uq_tiling_jobs_active_mission
            ON mission.tiling_jobs (mission_id) WHERE status IN ('QUEUED', 'RUNNING');
        ALTER TABLE mission.tiling_jobs DROP COLUMN kind;
        """);
}
