using AgriDrone.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261007120000_RebaselinePerPoleOrder")]
public sealed class RebaselinePerPoleOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL requires newly-added enum labels to be committed before they
        // can be used by defaults or DML in a later transaction.
        migrationBuilder.Sql(
            """
            ALTER TABLE survey.survey_orders
                ALTER COLUMN status DROP DEFAULT;

            ALTER TYPE system.survey_order_status
                RENAME VALUE 'PENDING_SCOPE_CONFIRMATION' TO 'PENDING_BOUNDARY_VERIFICATION';
            ALTER TYPE system.survey_order_status
                RENAME VALUE 'AWAITING_APPOINTMENT' TO 'AWAITING_PAID_APPOINTMENT';
            ALTER TYPE system.survey_order_status
                RENAME VALUE 'READY_FOR_OPERATIONS' TO 'READY_FOR_PAID_SERVICE';

            ALTER TYPE system.survey_order_status
                ADD VALUE IF NOT EXISTS 'AWAITING_BASELINE_APPOINTMENT';
            ALTER TYPE system.survey_order_status
                ADD VALUE IF NOT EXISTS 'BASELINE_READY';
            ALTER TYPE system.survey_order_status
                ADD VALUE IF NOT EXISTS 'BASELINE_IN_PROGRESS';
            ALTER TYPE system.survey_order_status
                ADD VALUE IF NOT EXISTS 'AWAITING_BASELINE_REVIEW';
            ALTER TYPE system.survey_order_status
                ADD VALUE IF NOT EXISTS 'AWAITING_PRICING';
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            """
            CREATE TYPE system.survey_appointment_purpose AS ENUM
                ('BASELINE_MAPPING', 'PAID_SERVICE');

            ALTER TABLE survey.survey_service_prices
                DROP CONSTRAINT ck_survey_service_prices_amount_positive,
                ADD COLUMN price_per_pole numeric(18,2),
                ALTER COLUMN price_per_ha DROP NOT NULL;

            ALTER TABLE survey.survey_service_prices
                ADD CONSTRAINT ck_survey_service_prices_amount_positive
                    CHECK (
                        (price_per_pole IS NULL OR price_per_pole > 0)
                        AND (price_per_ha IS NULL OR price_per_ha > 0)),
                ADD CONSTRAINT ck_survey_service_prices_pricing_mode
                    CHECK (
                        (price_per_pole IS NOT NULL AND price_per_ha IS NULL)
                        OR (price_per_pole IS NULL AND price_per_ha IS NOT NULL));

            ALTER TABLE survey.survey_orders
                ALTER COLUMN status
                    SET DEFAULT 'PENDING_BOUNDARY_VERIFICATION'::system.survey_order_status,
                DROP CONSTRAINT ck_survey_orders_pricing_snapshot_complete,
                DROP CONSTRAINT ck_survey_orders_price_nonnegative,
                DROP CONSTRAINT ck_survey_orders_final_price,
                ADD COLUMN confirmed_survey_pole_count integer,
                ADD COLUMN price_per_pole_snapshot numeric(18,2),
                ADD COLUMN farm_boundary_version_id uuid,
                ADD COLUMN farm_base_map_version_id uuid,
                ADD COLUMN pole_count_confirmed_by uuid,
                ADD COLUMN pole_count_confirmed_at timestamp with time zone,
                ADD COLUMN pricing_confirmed_by uuid,
                ADD COLUMN pricing_confirmed_at timestamp with time zone;

            ALTER TABLE survey.survey_orders
                ADD CONSTRAINT ck_survey_orders_price_nonnegative
                    CHECK (
                        (price_per_pole_snapshot IS NULL OR price_per_pole_snapshot > 0)
                        AND (price_per_ha_snapshot IS NULL OR price_per_ha_snapshot > 0)
                        AND (final_price IS NULL OR final_price >= 0)),
                ADD CONSTRAINT ck_survey_orders_pole_count_positive
                    CHECK (
                        confirmed_survey_pole_count IS NULL
                        OR confirmed_survey_pole_count > 0),
                ADD CONSTRAINT ck_survey_orders_legacy_pricing_snapshot_complete
                    CHECK (
                        (confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL)
                        OR
                        (confirmed_survey_area_ha IS NOT NULL
                         AND price_per_ha_snapshot IS NOT NULL
                         AND currency IS NOT NULL
                         AND final_price IS NOT NULL
                         AND scope_confirmed_by IS NOT NULL
                         AND scope_confirmed_at IS NOT NULL)),
                ADD CONSTRAINT ck_survey_orders_pole_count_snapshot_complete
                    CHECK (
                        (confirmed_survey_pole_count IS NULL
                         AND pole_count_confirmed_by IS NULL
                         AND pole_count_confirmed_at IS NULL)
                        OR
                        (confirmed_survey_pole_count IS NOT NULL
                         AND farm_boundary_version_id IS NOT NULL
                         AND farm_base_map_version_id IS NOT NULL
                         AND pole_count_confirmed_by IS NOT NULL
                         AND pole_count_confirmed_at IS NOT NULL)),
                ADD CONSTRAINT ck_survey_orders_per_pole_pricing_snapshot_complete
                    CHECK (
                        (price_per_pole_snapshot IS NULL
                         AND pricing_confirmed_by IS NULL
                         AND pricing_confirmed_at IS NULL)
                        OR
                        (price_per_pole_snapshot IS NOT NULL
                         AND survey_service_price_id IS NOT NULL
                         AND confirmed_survey_pole_count IS NOT NULL
                         AND farm_boundary_version_id IS NOT NULL
                         AND farm_base_map_version_id IS NOT NULL
                         AND pricing_confirmed_by IS NOT NULL
                         AND pricing_confirmed_at IS NOT NULL
                         AND currency IS NOT NULL
                         AND final_price IS NOT NULL)),
                ADD CONSTRAINT ck_survey_orders_pricing_mode
                    CHECK (NOT (
                        price_per_pole_snapshot IS NOT NULL
                        AND price_per_ha_snapshot IS NOT NULL)),
                ADD CONSTRAINT ck_survey_orders_final_price
                    CHECK (
                        final_price IS NULL
                        OR
                        (price_per_pole_snapshot IS NOT NULL
                         AND final_price = round(
                             confirmed_survey_pole_count * price_per_pole_snapshot,
                             2))
                        OR
                        (price_per_ha_snapshot IS NOT NULL
                         AND final_price = round(
                             confirmed_survey_area_ha * price_per_ha_snapshot,
                             2)));

            CREATE INDEX "IX_survey_orders_farm_base_map_version_id_farm_id"
                ON survey.survey_orders (farm_base_map_version_id, farm_id);
            CREATE INDEX "IX_survey_orders_pole_count_confirmed_by"
                ON survey.survey_orders (pole_count_confirmed_by);
            CREATE INDEX "IX_survey_orders_pricing_confirmed_by"
                ON survey.survey_orders (pricing_confirmed_by);

            ALTER TABLE survey.survey_orders
                ADD CONSTRAINT fk_survey_orders_farm_base_map_same_farm
                    FOREIGN KEY (farm_base_map_version_id, farm_id)
                    REFERENCES farm.farm_base_map_versions (id, farm_id)
                    ON DELETE RESTRICT,
                ADD CONSTRAINT fk_survey_orders_users_pole_count_confirmed_by
                    FOREIGN KEY (pole_count_confirmed_by)
                    REFERENCES identity.users (id)
                    ON DELETE RESTRICT,
                ADD CONSTRAINT fk_survey_orders_users_pricing_confirmed_by
                    FOREIGN KEY (pricing_confirmed_by)
                    REFERENCES identity.users (id)
                    ON DELETE RESTRICT;

            DROP INDEX survey.uq_survey_appointments_one_active_per_order;
            ALTER TABLE survey.survey_appointments
                ADD COLUMN purpose system.survey_appointment_purpose NOT NULL
                    DEFAULT 'PAID_SERVICE'::system.survey_appointment_purpose;
            CREATE UNIQUE INDEX uq_survey_appointments_one_active_per_order_purpose
                ON survey.survey_appointments (survey_order_id, purpose)
                WHERE status IN (
                    'PROPOSED'::system.survey_appointment_status,
                    'CONFIRMED'::system.survey_appointment_status,
                    'RESCHEDULE_REQUESTED'::system.survey_appointment_status);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $guard$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM survey.survey_service_prices
                    WHERE price_per_pole IS NOT NULL
                ) OR EXISTS
                (
                    SELECT 1
                    FROM survey.survey_orders
                    WHERE price_per_pole_snapshot IS NOT NULL
                       OR confirmed_survey_pole_count IS NOT NULL
                       OR farm_boundary_version_id IS NOT NULL
                       OR farm_base_map_version_id IS NOT NULL
                ) OR EXISTS
                (
                    SELECT 1
                    FROM survey.survey_appointments
                    WHERE purpose = 'BASELINE_MAPPING'::system.survey_appointment_purpose
                ) OR EXISTS
                (
                    SELECT 1
                    FROM survey.survey_orders
                    WHERE status::text IN
                    (
                        'AWAITING_BASELINE_APPOINTMENT',
                        'BASELINE_READY',
                        'BASELINE_IN_PROGRESS',
                        'AWAITING_BASELINE_REVIEW',
                        'AWAITING_PRICING'
                    )
                ) THEN
                    RAISE EXCEPTION
                        'Cannot roll back RebaselinePerPoleOrder after target per-pole or baseline data exists.'
                        USING ERRCODE = 'P0001';
                END IF;
            END
            $guard$;

            DROP INDEX survey.uq_survey_appointments_one_active_per_order_purpose;
            ALTER TABLE survey.survey_appointments DROP COLUMN purpose;
            CREATE UNIQUE INDEX uq_survey_appointments_one_active_per_order
                ON survey.survey_appointments (survey_order_id)
                WHERE status IN (
                    'PROPOSED'::system.survey_appointment_status,
                    'CONFIRMED'::system.survey_appointment_status,
                    'RESCHEDULE_REQUESTED'::system.survey_appointment_status);
            DROP TYPE system.survey_appointment_purpose;

            ALTER TABLE survey.survey_orders
                DROP CONSTRAINT fk_survey_orders_farm_base_map_same_farm,
                DROP CONSTRAINT fk_survey_orders_users_pole_count_confirmed_by,
                DROP CONSTRAINT fk_survey_orders_users_pricing_confirmed_by,
                DROP CONSTRAINT ck_survey_orders_price_nonnegative,
                DROP CONSTRAINT ck_survey_orders_pole_count_positive,
                DROP CONSTRAINT ck_survey_orders_legacy_pricing_snapshot_complete,
                DROP CONSTRAINT ck_survey_orders_pole_count_snapshot_complete,
                DROP CONSTRAINT ck_survey_orders_per_pole_pricing_snapshot_complete,
                DROP CONSTRAINT ck_survey_orders_pricing_mode,
                DROP CONSTRAINT ck_survey_orders_final_price;

            DROP INDEX survey."IX_survey_orders_farm_base_map_version_id_farm_id";
            DROP INDEX survey."IX_survey_orders_pole_count_confirmed_by";
            DROP INDEX survey."IX_survey_orders_pricing_confirmed_by";

            ALTER TABLE survey.survey_orders
                DROP COLUMN confirmed_survey_pole_count,
                DROP COLUMN price_per_pole_snapshot,
                DROP COLUMN farm_boundary_version_id,
                DROP COLUMN farm_base_map_version_id,
                DROP COLUMN pole_count_confirmed_by,
                DROP COLUMN pole_count_confirmed_at,
                DROP COLUMN pricing_confirmed_by,
                DROP COLUMN pricing_confirmed_at,
                ADD CONSTRAINT ck_survey_orders_price_nonnegative
                    CHECK (
                        (price_per_ha_snapshot IS NULL OR price_per_ha_snapshot > 0)
                        AND (final_price IS NULL OR final_price >= 0)),
                ADD CONSTRAINT ck_survey_orders_pricing_snapshot_complete
                    CHECK (
                        (confirmed_survey_area_ha IS NULL
                         AND price_per_ha_snapshot IS NULL
                         AND currency IS NULL
                         AND final_price IS NULL
                         AND scope_confirmed_by IS NULL
                         AND scope_confirmed_at IS NULL)
                        OR
                        (confirmed_survey_area_ha IS NOT NULL
                         AND price_per_ha_snapshot IS NOT NULL
                         AND currency IS NOT NULL
                         AND final_price IS NOT NULL
                         AND scope_confirmed_by IS NOT NULL
                         AND scope_confirmed_at IS NOT NULL)),
                ADD CONSTRAINT ck_survey_orders_final_price
                    CHECK (
                        final_price IS NULL
                        OR final_price = round(
                            confirmed_survey_area_ha * price_per_ha_snapshot,
                            2));

            ALTER TABLE survey.survey_service_prices
                DROP CONSTRAINT ck_survey_service_prices_amount_positive,
                DROP CONSTRAINT ck_survey_service_prices_pricing_mode,
                DROP COLUMN price_per_pole,
                ALTER COLUMN price_per_ha SET NOT NULL,
                ADD CONSTRAINT ck_survey_service_prices_amount_positive
                    CHECK (price_per_ha > 0);

            ALTER TABLE survey.survey_orders
                ALTER COLUMN status DROP DEFAULT,
                ALTER COLUMN status TYPE text USING status::text;

            DROP TYPE system.survey_order_status;
            CREATE TYPE system.survey_order_status AS ENUM
            (
                'PENDING_SCOPE_CONFIRMATION',
                'AWAITING_APPOINTMENT',
                'AWAITING_PAYMENT',
                'READY_FOR_OPERATIONS',
                'IN_PROGRESS',
                'PENDING_REVIEW',
                'COMPLETED',
                'CANCELLED'
            );

            UPDATE survey.survey_orders
            SET status = CASE status
                WHEN 'PENDING_BOUNDARY_VERIFICATION' THEN 'PENDING_SCOPE_CONFIRMATION'
                WHEN 'AWAITING_PAID_APPOINTMENT' THEN 'AWAITING_APPOINTMENT'
                WHEN 'READY_FOR_PAID_SERVICE' THEN 'READY_FOR_OPERATIONS'
                ELSE status
            END;

            ALTER TABLE survey.survey_orders
                ALTER COLUMN status TYPE system.survey_order_status
                    USING status::system.survey_order_status,
                ALTER COLUMN status
                    SET DEFAULT 'PENDING_SCOPE_CONFIRMATION'::system.survey_order_status;
            """);
    }
}
