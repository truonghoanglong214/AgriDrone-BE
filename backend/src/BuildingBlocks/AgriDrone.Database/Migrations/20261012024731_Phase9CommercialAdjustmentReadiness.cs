using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase9CommercialAdjustmentReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_price_adjustments_users_approved_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_price_adjustments_approved_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropIndex(
                name: "uq_price_adjustments_one_pending_per_order",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_approval",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_area_positive",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_changed",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_price_nonnegative",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.AlterColumn<decimal>(
                name: "old_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "old_area_ha",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldPrecision: 12,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "new_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "new_area_ha",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldPrecision: 12,
                oldScale: 4);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "applied_at",
                schema: "survey",
                table: "price_adjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "applied_by",
                schema: "survey",
                table: "price_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                schema: "survey",
                table: "price_adjustments",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_reference",
                schema: "survey",
                table: "price_adjustments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "new_final_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "new_pole_count",
                schema: "survey",
                table: "price_adjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "new_price_per_pole",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "new_survey_service_price_id",
                schema: "survey",
                table: "price_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "old_final_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "old_pole_count",
                schema: "survey",
                table: "price_adjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "old_price_per_pole",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "old_survey_service_price_id",
                schema: "survey",
                table: "price_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_evidence_reference",
                schema: "survey",
                table: "price_adjustments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_reason",
                schema: "survey",
                table: "price_adjustments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "survey",
                table: "price_adjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by",
                schema: "survey",
                table: "price_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "survey",
                table: "price_adjustments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.Sql(
                """
                UPDATE survey.price_adjustments AS adjustment
                SET
                    old_pole_count = legacy.old_pole_count,
                    new_pole_count = legacy.new_pole_count,
                    old_survey_service_price_id = COALESCE(
                        survey_order.survey_service_price_id,
                        gen_random_uuid()),
                    new_survey_service_price_id = COALESCE(
                        survey_order.survey_service_price_id,
                        gen_random_uuid()),
                    old_price_per_pole = legacy.old_price_per_pole,
                    new_price_per_pole = legacy.new_price_per_pole,
                    old_final_price = round(
                        legacy.old_pole_count * legacy.old_price_per_pole,
                        2),
                    new_final_price = round(
                        legacy.new_pole_count * legacy.new_price_per_pole,
                        2),
                    currency = COALESCE(
                        NULLIF(btrim(survey_order.currency), ''),
                        'VND'),
                    evidence_reference = 'LEGACY_AREA_ADJUSTMENT',
                    reviewed_by = CASE
                        WHEN adjustment.status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'REJECTED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN COALESCE(
                            adjustment.approved_by,
                            adjustment.requested_by)
                        ELSE NULL
                    END,
                    reviewed_at = CASE
                        WHEN adjustment.status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'REJECTED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN COALESCE(
                            adjustment.approved_at,
                            adjustment.created_at)
                        ELSE NULL
                    END,
                    review_reason = CASE
                        WHEN adjustment.status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'REJECTED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN 'Migrated legacy area-based adjustment'
                        ELSE NULL
                    END,
                    review_evidence_reference = CASE
                        WHEN adjustment.status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'REJECTED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN 'LEGACY_AREA_ADJUSTMENT'
                        ELSE NULL
                    END,
                    applied_by = CASE
                        WHEN adjustment.status =
                            'APPLIED'::system.price_adjustment_status
                        THEN COALESCE(
                            adjustment.approved_by,
                            adjustment.requested_by)
                        ELSE NULL
                    END,
                    applied_at = CASE
                        WHEN adjustment.status =
                            'APPLIED'::system.price_adjustment_status
                        THEN COALESCE(
                            adjustment.approved_at,
                            adjustment.created_at)
                        ELSE NULL
                    END
                FROM survey.survey_orders AS survey_order
                CROSS JOIN LATERAL (
                    SELECT
                        GREATEST(
                            1,
                            COALESCE(
                                survey_order.confirmed_survey_pole_count,
                                round(adjustment.old_area_ha)::integer,
                                1)) AS old_pole_count,
                        GREATEST(
                            1,
                            COALESCE(
                                survey_order.confirmed_survey_pole_count,
                                round(adjustment.new_area_ha)::integer,
                                1)) AS new_pole_count
                ) AS counts
                CROSS JOIN LATERAL (
                    SELECT
                        counts.old_pole_count,
                        counts.new_pole_count,
                        GREATEST(
                            0.01,
                            round(
                                COALESCE(
                                    NULLIF(adjustment.old_price, 0) /
                                        counts.old_pole_count,
                                    survey_order.price_per_pole_snapshot,
                                    0.01),
                                2)) AS old_price_per_pole,
                        GREATEST(
                            0.01,
                            round(
                                COALESCE(
                                    NULLIF(adjustment.new_price, 0) /
                                        counts.new_pole_count,
                                    survey_order.price_per_pole_snapshot,
                                    0.01),
                                2)) AS new_price_per_pole
                ) AS legacy
                WHERE survey_order.id = adjustment.survey_order_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "currency",
                schema: "survey",
                table: "price_adjustments",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3,
                oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "evidence_reference",
                schema: "survey",
                table: "price_adjustments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
            migrationBuilder.Sql(
                """
                ALTER TABLE survey.price_adjustments
                    ALTER COLUMN old_pole_count SET NOT NULL,
                    ALTER COLUMN new_pole_count SET NOT NULL,
                    ALTER COLUMN old_survey_service_price_id SET NOT NULL,
                    ALTER COLUMN new_survey_service_price_id SET NOT NULL,
                    ALTER COLUMN old_price_per_pole SET NOT NULL,
                    ALTER COLUMN new_price_per_pole SET NOT NULL,
                    ALTER COLUMN old_final_price SET NOT NULL,
                    ALTER COLUMN new_final_price SET NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.price_adjustments
                        WHERE status IN (
                            'PENDING'::system.price_adjustment_status,
                            'APPROVED'::system.price_adjustment_status)
                        GROUP BY survey_order_id
                        HAVING count(*) > 1)
                    THEN
                        RAISE EXCEPTION
                            'Phase9CommercialAdjustmentReadiness requires manual disposition of Orders with multiple open price adjustments';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_price_adjustments_applied_by",
                schema: "survey",
                table: "price_adjustments",
                column: "applied_by");

            migrationBuilder.CreateIndex(
                name: "IX_price_adjustments_reviewed_by",
                schema: "survey",
                table: "price_adjustments",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "uq_price_adjustments_one_open_per_order",
                schema: "survey",
                table: "price_adjustments",
                column: "survey_order_id",
                unique: true,
                filter: "status IN ('PENDING'::system.price_adjustment_status, 'APPROVED'::system.price_adjustment_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_application",
                schema: "survey",
                table: "price_adjustments",
                sql: "(status = 'APPLIED'::system.price_adjustment_status AND applied_by IS NOT NULL AND applied_at IS NOT NULL) OR (status <> 'APPLIED'::system.price_adjustment_status AND applied_by IS NULL AND applied_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_changed",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_area_ha IS NOT NULL OR old_pole_count <> new_pole_count OR old_survey_service_price_id <> new_survey_service_price_id OR old_price_per_pole <> new_price_per_pole");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_currency",
                schema: "survey",
                table: "price_adjustments",
                sql: "currency ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_final_price",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_final_price = round(old_pole_count * old_price_per_pole, 2) AND new_final_price = round(new_pole_count * new_price_per_pole, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_pole_count_positive",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_pole_count > 0 AND new_pole_count > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_price_positive",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_price_per_pole > 0 AND new_price_per_pole > 0 AND old_final_price > 0 AND new_final_price > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_review",
                schema: "survey",
                table: "price_adjustments",
                sql: "(status = 'PENDING'::system.price_adjustment_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence_reference IS NULL) OR (status IN ('APPROVED'::system.price_adjustment_status, 'REJECTED'::system.price_adjustment_status, 'APPLIED'::system.price_adjustment_status) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence_reference IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_price_adjustments_users_applied_by",
                schema: "survey",
                table: "price_adjustments",
                column: "applied_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_price_adjustments_users_reviewed_by",
                schema: "survey",
                table: "price_adjustments",
                column: "reviewed_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_price_adjustments_users_applied_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropForeignKey(
                name: "fk_price_adjustments_users_reviewed_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_price_adjustments_applied_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_price_adjustments_reviewed_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropIndex(
                name: "uq_price_adjustments_one_open_per_order",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_application",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_changed",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_currency",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_final_price",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_pole_count_positive",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_price_positive",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_adjustments_review",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.Sql(
                """
                UPDATE survey.price_adjustments
                SET
                    old_area_ha = old_pole_count,
                    new_area_ha = CASE
                        WHEN old_pole_count = new_pole_count
                         AND old_final_price = new_final_price
                        THEN new_pole_count + 0.0001
                        ELSE new_pole_count
                    END,
                    old_price = old_final_price,
                    new_price = new_final_price,
                    approved_by = CASE
                        WHEN status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN reviewed_by
                        ELSE approved_by
                    END,
                    approved_at = CASE
                        WHEN status IN (
                            'APPROVED'::system.price_adjustment_status,
                            'APPLIED'::system.price_adjustment_status)
                        THEN reviewed_at
                        ELSE approved_at
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "applied_at",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "applied_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "currency",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "evidence_reference",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "new_final_price",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "new_pole_count",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "new_price_per_pole",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "new_survey_service_price_id",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "old_final_price",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "old_pole_count",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "old_price_per_pole",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "old_survey_service_price_id",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "review_evidence_reference",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "review_reason",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "survey",
                table: "price_adjustments");

            migrationBuilder.AlterColumn<decimal>(
                name: "old_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "old_area_ha",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldPrecision: 12,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "new_price",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "new_area_ha",
                schema: "survey",
                table: "price_adjustments",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldPrecision: 12,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_adjustments_approved_by",
                schema: "survey",
                table: "price_adjustments",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "uq_price_adjustments_one_pending_per_order",
                schema: "survey",
                table: "price_adjustments",
                column: "survey_order_id",
                unique: true,
                filter: "status = 'PENDING'::system.price_adjustment_status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_approval",
                schema: "survey",
                table: "price_adjustments",
                sql: "(status IN ('APPROVED'::system.price_adjustment_status, 'APPLIED'::system.price_adjustment_status) AND approved_by IS NOT NULL AND approved_at IS NOT NULL) OR status IN ('PENDING'::system.price_adjustment_status, 'REJECTED'::system.price_adjustment_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_area_positive",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_area_ha > 0 AND new_area_ha > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_changed",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_area_ha <> new_area_ha OR old_price <> new_price");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_adjustments_price_nonnegative",
                schema: "survey",
                table: "price_adjustments",
                sql: "old_price >= 0 AND new_price >= 0");

            migrationBuilder.AddForeignKey(
                name: "fk_price_adjustments_users_approved_by",
                schema: "survey",
                table: "price_adjustments",
                column: "approved_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
