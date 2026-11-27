using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261128040000_MissionDiseaseJobs")]
public sealed class MissionDiseaseJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE
            constraint_name text;
        BEGIN
            FOR constraint_name IN
                SELECT con.conname
                FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
                WHERE nsp.nspname = 'mission'
                  AND rel.relname = 'tiling_jobs'
                  AND con.contype = 'c'
                  AND pg_get_constraintdef(con.oid) LIKE '%kind%'
            LOOP
                EXECUTE format('ALTER TABLE mission.tiling_jobs DROP CONSTRAINT %I', constraint_name);
            END LOOP;
        END $$;

        ALTER TABLE mission.tiling_jobs
            ADD CONSTRAINT ck_tiling_jobs_kind CHECK (kind IN ('TILING', 'YOLO', 'DISEASE'));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM mission.tiling_jobs WHERE kind = 'DISEASE';
        ALTER TABLE mission.tiling_jobs DROP CONSTRAINT IF EXISTS ck_tiling_jobs_kind;
        ALTER TABLE mission.tiling_jobs
            ADD CONSTRAINT ck_tiling_jobs_kind CHECK (kind IN ('TILING', 'YOLO'));
        """);
}
