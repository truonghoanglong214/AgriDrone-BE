using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261127100000_MissionTilingJobs")]
public sealed class MissionTilingJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE mission.tiling_jobs (
            id uuid PRIMARY KEY,
            mission_id uuid NOT NULL REFERENCES mission.drone_missions (id) ON DELETE CASCADE,
            tenant_id uuid NOT NULL,
            farm_id uuid NOT NULL,
            status text NOT NULL CHECK (status IN ('QUEUED', 'RUNNING', 'COMPLETED', 'FAILED')),
            input jsonb NOT NULL,
            output jsonb,
            run_id uuid,
            error text,
            created_at timestamptz NOT NULL,
            started_at timestamptz,
            completed_at timestamptz
        );

        CREATE UNIQUE INDEX uq_tiling_jobs_active_mission
            ON mission.tiling_jobs (mission_id) WHERE status IN ('QUEUED', 'RUNNING');
        CREATE INDEX ix_tiling_jobs_mission_recent
            ON mission.tiling_jobs (mission_id, created_at DESC, id DESC);
        CREATE INDEX ix_tiling_jobs_active
            ON mission.tiling_jobs (created_at, id) WHERE status IN ('QUEUED', 'RUNNING');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE mission.tiling_jobs;");
}
