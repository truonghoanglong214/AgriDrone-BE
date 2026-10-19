using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261017090000_ReplaceBlackboxDecodeWithMissionPlantGps")]
public sealed class ReplaceBlackboxDecodeWithMissionPlantGps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM mission.blackbox_decodes LIMIT 1) THEN
                    RAISE EXCEPTION 'mission.blackbox_decodes is not empty';
                END IF;
            END $$;
            """);
        migrationBuilder.DropTable(name: "blackbox_decodes", schema: "mission");
        migrationBuilder.Sql("""
            CREATE TABLE mission.mission_plant_gps (
                mission_id uuid NOT NULL REFERENCES mission.drone_missions(id) ON DELETE RESTRICT,
                tree_id varchar(100) NOT NULL,
                row_number integer NOT NULL CHECK (row_number > 0),
                col_number integer NOT NULL CHECK (col_number > 0),
                latitude numeric(18,12) NOT NULL CHECK (latitude BETWEEN -90 AND 90),
                longitude numeric(18,12) NOT NULL CHECK (longitude BETWEEN -180 AND 180),
                confidence numeric(18,15) NOT NULL CHECK (confidence BETWEEN 0 AND 1),
                status varchar(20) NOT NULL CHECK (status IN ('DETECTED', 'MISSING')),
                source varchar(100) NOT NULL,
                saved_at timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT pk_mission_plant_gps PRIMARY KEY (mission_id, tree_id)
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "mission_plant_gps", schema: "mission");
        migrationBuilder.Sql("""
            CREATE TABLE mission.blackbox_decodes (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                farm_id uuid NOT NULL,
                mission_id uuid NOT NULL,
                source_file_name varchar(255) NOT NULL,
                source_checksum char(64) NOT NULL,
                points_json jsonb NOT NULL CHECK (jsonb_typeof(points_json) = 'object'),
                created_by uuid NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_blackbox_farm FOREIGN KEY (mission_id, farm_id)
                    REFERENCES mission.drone_missions(id, farm_id) ON DELETE RESTRICT,
                CONSTRAINT fk_blackbox_tenant FOREIGN KEY (mission_id, tenant_id)
                    REFERENCES mission.drone_missions(id, tenant_id) ON DELETE RESTRICT,
                CONSTRAINT uq_blackbox_decodes_mission_checksum UNIQUE (mission_id, source_checksum)
            );
            CREATE INDEX ix_blackbox_decodes_mission_id_farm_id
                ON mission.blackbox_decodes(mission_id, farm_id);
            CREATE INDEX ix_blackbox_decodes_mission_id_tenant_id
                ON mission.blackbox_decodes(mission_id, tenant_id);
            """);
    }
}
