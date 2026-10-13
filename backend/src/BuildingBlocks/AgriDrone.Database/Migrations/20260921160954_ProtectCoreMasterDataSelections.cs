using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProtectCoreMasterDataSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.protect_required_health_levels()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF OLD.code IN ('UNKNOWN', 'HEALTHY', 'MILD', 'MODERATE', 'SEVERE') THEN
                        RAISE EXCEPTION
                            'Required system health level % cannot be updated or deleted.',
                            OLD.code
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_health_levels_required_immutable';
                    END IF;

                    IF TG_OP = 'UPDATE' AND
                       NEW.code IN ('UNKNOWN', 'HEALTHY', 'MILD', 'MODERATE', 'SEVERE') THEN
                        RAISE EXCEPTION
                            'A custom health level cannot replace required system health level %.',
                            NEW.code
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_health_levels_required_immutable';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER trg_health_levels_protect_required
                BEFORE UPDATE OR DELETE ON plant.health_levels
                FOR EACH ROW
                EXECUTE FUNCTION plant.protect_required_health_levels();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.validate_condition_detection_master_data()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    selected_condition_type text;
                    condition_is_active boolean;
                    severity_code text;
                    severity_is_active boolean;
                BEGIN
                    SELECT condition_type::text, is_active
                    INTO selected_condition_type, condition_is_active
                    FROM plant.plant_conditions
                    WHERE id = NEW.condition_id;

                    IF NOT FOUND OR NOT condition_is_active THEN
                        RAISE EXCEPTION
                            'Condition detection must reference an active plant condition.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_condition_detection_active_condition';
                    END IF;

                    SELECT code, is_active
                    INTO severity_code, severity_is_active
                    FROM plant.health_levels
                    WHERE id = NEW.severity_level_id;

                    IF NOT FOUND OR NOT severity_is_active THEN
                        RAISE EXCEPTION
                            'Condition detection must reference an active health level.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_condition_detection_active_severity';
                    END IF;

                    IF selected_condition_type = 'DISEASE' AND
                       severity_code NOT IN ('MILD', 'MODERATE', 'SEVERE') THEN
                        RAISE EXCEPTION
                            'Disease detections require MILD, MODERATE, or SEVERE severity.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_condition_detection_disease_severity';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER trg_condition_detections_validate_master_data
                BEFORE INSERT OR UPDATE OF condition_id, severity_level_id
                ON plant.condition_detections
                FOR EACH ROW
                EXECUTE FUNCTION plant.validate_condition_detection_master_data();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION harvest.validate_active_quality_grade()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    grade_is_active boolean;
                BEGIN
                    SELECT is_active
                    INTO grade_is_active
                    FROM harvest.harvest_quality_grades
                    WHERE id = NEW.quality_grade_id;

                    IF NOT FOUND OR NOT grade_is_active THEN
                        RAISE EXCEPTION
                            'Harvest quality detail must reference an active quality grade.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_quality_detail_active_grade';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER trg_quality_details_validate_active_grade
                BEFORE INSERT OR UPDATE OF quality_grade_id
                ON harvest.plant_harvest_quality_details
                FOR EACH ROW
                EXECUTE FUNCTION harvest.validate_active_quality_grade();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_quality_details_validate_active_grade
                    ON harvest.plant_harvest_quality_details;
                DROP FUNCTION IF EXISTS harvest.validate_active_quality_grade();

                DROP TRIGGER IF EXISTS trg_condition_detections_validate_master_data
                    ON plant.condition_detections;
                DROP FUNCTION IF EXISTS plant.validate_condition_detection_master_data();

                DROP TRIGGER IF EXISTS trg_health_levels_protect_required
                    ON plant.health_levels;
                DROP FUNCTION IF EXISTS plant.protect_required_health_levels();
                """);

        }
    }
}
