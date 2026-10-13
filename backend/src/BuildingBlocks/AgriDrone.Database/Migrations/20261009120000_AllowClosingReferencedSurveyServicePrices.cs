using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261009120000_AllowClosingReferencedSurveyServicePrices")]
public sealed class AllowClosingReferencedSurveyServicePrices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION survey.protect_referenced_survey_service_price()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM survey.survey_orders AS orders
                    WHERE orders.survey_service_price_id = OLD.id
                ) THEN
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;

                    RETURN NEW;
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION
                        'Survey service price % cannot be deleted because an order references it.',
                        OLD.id
                        USING ERRCODE = '23514';
                END IF;

                IF ROW(
                    NEW.id,
                    NEW.survey_service_id,
                    NEW.price_per_pole,
                    NEW.price_per_ha,
                    NEW.currency,
                    NEW.effective_from,
                    NEW.created_by,
                    NEW.created_at)
                   IS DISTINCT FROM
                   ROW(
                    OLD.id,
                    OLD.survey_service_id,
                    OLD.price_per_pole,
                    OLD.price_per_ha,
                    OLD.currency,
                    OLD.effective_from,
                    OLD.created_by,
                    OLD.created_at)
                   OR OLD.effective_to IS NOT NULL
                   OR NEW.effective_to IS NULL
                   OR NEW.effective_to <= OLD.effective_from THEN
                    RAISE EXCEPTION
                        'Referenced survey service price % is immutable except for closing its open effective window once.',
                        OLD.id
                        USING ERRCODE = '23514';
                END IF;

                RETURN NEW;
            END;
            $function$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION survey.protect_referenced_survey_service_price()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM survey.survey_orders AS orders
                    WHERE orders.survey_service_price_id = OLD.id
                ) THEN
                    RAISE EXCEPTION
                        'Survey service price % is immutable because an order references it.',
                        OLD.id
                        USING ERRCODE = '23503';
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;

                RETURN NEW;
            END;
            $function$;
            """);
    }
}
