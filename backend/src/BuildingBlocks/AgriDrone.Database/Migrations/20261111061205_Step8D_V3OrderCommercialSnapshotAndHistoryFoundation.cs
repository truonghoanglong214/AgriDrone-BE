using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step8D_V3OrderCommercialSnapshotAndHistoryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_deposit_consistency",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_legacy_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_per_pole_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.AddColumn<Guid>(
                name: "business_policy_version_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<ContractScope>(
                name: "scope",
                schema: "survey",
                table: "survey_orders",
                type: "system.contract_scope",
                nullable: false,
                defaultValue: ContractScope.Legacy);

            migrationBuilder.CreateTable(
                name: "order_commercial_snapshots",
                schema: "survey",
                columns: table => new
                {
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    survey_service_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_service_revision_version = table.Column<int>(type: "integer", nullable: false),
                    service_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    maps_poles = table.Column<bool>(type: "boolean", nullable: false),
                    screens_disease = table.Column<bool>(type: "boolean", nullable: false),
                    price_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_version_number = table.Column<int>(type: "integer", nullable: false),
                    price_per_pole = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    deposit_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_version_number = table.Column<int>(type: "integer", nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    claim_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    claim_window_days = table.Column<int>(type: "integer", nullable: false),
                    claim_time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    exclude_notice_date = table.Column<bool>(type: "boolean", nullable: false),
                    cancellation_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    refundable_before_capture = table.Column<bool>(type: "boolean", nullable: false),
                    retain_deposit_after_capture = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_free_no_show_reschedules = table.Column<int>(type: "integer", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_commercial_snapshots", x => x.survey_order_id);
                    table.CheckConstraint("ck_order_commercial_snapshots_claim_window", "claim_window_days >= 1");
                    table.CheckConstraint("ck_order_commercial_snapshots_currency", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_order_commercial_snapshots_deposit", "deposit_amount > 0 AND deposit_amount = FLOOR(deposit_amount)");
                    table.CheckConstraint("ck_order_commercial_snapshots_price", "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                    table.CheckConstraint("ck_order_commercial_snapshots_reschedules", "allowed_free_no_show_reschedules >= 0");
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_cancellation_policy",
                        column: x => x.cancellation_policy_id,
                        principalSchema: "survey",
                        principalTable: "cancellation_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_claim_policy",
                        column: x => x.claim_policy_id,
                        principalSchema: "survey",
                        principalTable: "claim_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_deposit_policy",
                        column: x => x.deposit_policy_id,
                        principalSchema: "survey",
                        principalTable: "survey_deposit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_policy_version",
                        column: x => x.business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_price_version",
                        column: x => x.price_version_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_commercial_snapshots_service_revision",
                        column: x => x.survey_service_revision_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_state_history",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_version = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<SurveyOrderStatus>(type: "system.survey_order_status", nullable: true),
                    to_status = table.Column<SurveyOrderStatus>(type: "system.survey_order_status", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_state_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_state_history_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_state_history_predecessor",
                        column: x => x.predecessor_history_id,
                        principalSchema: "survey",
                        principalTable: "order_state_history",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_business_policy_version_id",
                schema: "survey",
                table: "survey_orders",
                column: "business_policy_version_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_deposit_consistency",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR (requires_baseline_mapping = false AND deposit_amount = 0 AND deposit_payment_status = 'PAID'::system.survey_payment_status) OR (requires_baseline_mapping = true AND deposit_amount > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_legacy_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR (confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL) OR (confirmed_survey_area_ha IS NOT NULL AND price_per_ha_snapshot IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL AND scope_confirmed_by IS NOT NULL AND scope_confirmed_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_per_pole_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR (price_per_pole_snapshot IS NULL AND pricing_confirmed_by IS NULL AND pricing_confirmed_at IS NULL) OR (price_per_pole_snapshot IS NOT NULL AND survey_service_price_id IS NOT NULL AND confirmed_survey_pole_count IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND pricing_confirmed_by IS NOT NULL AND pricing_confirmed_at IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_scope_policy",
                schema: "survey",
                table: "survey_orders",
                sql: "(scope = 'LEGACY' AND business_policy_version_id IS NULL) OR (scope = 'V3' AND business_policy_version_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_v3_deposit",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'LEGACY' OR (deposit_amount > 0 AND deposit_amount = FLOOR(deposit_amount))");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_business_policy_version_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_cancellation_policy_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "cancellation_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_claim_policy_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "claim_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_deposit_policy_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "deposit_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_price_version_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "price_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_commercial_snapshots_survey_service_revision_id",
                schema: "survey",
                table: "order_commercial_snapshots",
                column: "survey_service_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_state_history_order_version",
                schema: "survey",
                table: "order_state_history",
                columns: new[] { "survey_order_id", "order_version" });

            migrationBuilder.CreateIndex(
                name: "IX_order_state_history_predecessor_history_id",
                schema: "survey",
                table: "order_state_history",
                column: "predecessor_history_id");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_orders_business_policy_version",
                schema: "survey",
                table: "survey_orders",
                column: "business_policy_version_id",
                principalSchema: "survey",
                principalTable: "business_policy_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_business_policy_version",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropTable(
                name: "order_commercial_snapshots",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "order_state_history",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "IX_survey_orders_business_policy_version_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_deposit_consistency",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_legacy_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_per_pole_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_scope_policy",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_v3_deposit",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "business_policy_version_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_deposit_consistency",
                schema: "survey",
                table: "survey_orders",
                sql: "(requires_baseline_mapping = false AND deposit_amount = 0 AND deposit_payment_status = 'PAID'::system.survey_payment_status) OR (requires_baseline_mapping = true AND deposit_amount > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_legacy_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "(confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL) OR (confirmed_survey_area_ha IS NOT NULL AND price_per_ha_snapshot IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL AND scope_confirmed_by IS NOT NULL AND scope_confirmed_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_per_pole_pricing_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "(price_per_pole_snapshot IS NULL AND pricing_confirmed_by IS NULL AND pricing_confirmed_at IS NULL) OR (price_per_pole_snapshot IS NOT NULL AND survey_service_price_id IS NOT NULL AND confirmed_survey_pole_count IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND pricing_confirmed_by IS NOT NULL AND pricing_confirmed_at IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL)");
        }
    }
}
