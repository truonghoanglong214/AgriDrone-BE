using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddComboComponentPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.AddColumn<decimal>(
                name: "mapping_price_per_pole",
                schema: "survey",
                table: "survey_service_prices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "plant_health_price_per_pole",
                schema: "survey",
                table: "survey_service_prices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "booked_mapping_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "booked_plant_health_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "combo_discount_amount",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "confirmed_mapping_pole_count",
                schema: "survey",
                table: "survey_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "confirmed_plant_health_pole_count",
                schema: "survey",
                table: "survey_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mapping_subtotal",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "plant_health_subtotal",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_service_prices_combo_components",
                schema: "survey",
                table: "survey_service_prices",
                sql: "(mapping_price_per_pole IS NULL AND plant_health_price_per_pole IS NULL) OR (mapping_price_per_pole IS NOT NULL AND plant_health_price_per_pole IS NOT NULL AND mapping_price_per_pole > 0 AND plant_health_price_per_pole > 0 AND mapping_price_per_pole = trunc(mapping_price_per_pole) AND plant_health_price_per_pole = trunc(plant_health_price_per_pole) AND price_per_pole IS NOT NULL AND price_per_pole = mapping_price_per_pole + plant_health_price_per_pole)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_combo_component_booking",
                schema: "survey",
                table: "survey_orders",
                sql: "(booked_mapping_price_per_pole IS NULL AND booked_plant_health_price_per_pole IS NULL) OR (booked_mapping_price_per_pole IS NOT NULL AND booked_plant_health_price_per_pole IS NOT NULL AND scope = 'LEGACY' AND post_flight_settlement AND booked_service_price_id IS NOT NULL AND booked_price_per_pole IS NOT NULL AND booked_mapping_price_per_pole > 0 AND booked_plant_health_price_per_pole > 0 AND booked_mapping_price_per_pole = trunc(booked_mapping_price_per_pole) AND booked_plant_health_price_per_pole = trunc(booked_plant_health_price_per_pole) AND booked_price_per_pole = booked_mapping_price_per_pole + booked_plant_health_price_per_pole)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_combo_component_invoice",
                schema: "survey",
                table: "survey_orders",
                sql: "(confirmed_mapping_pole_count IS NULL AND confirmed_plant_health_pole_count IS NULL AND mapping_subtotal IS NULL AND plant_health_subtotal IS NULL AND combo_discount_amount IS NULL AND (booked_mapping_price_per_pole IS NULL OR final_price IS NULL)) OR (booked_mapping_price_per_pole IS NOT NULL AND booked_plant_health_price_per_pole IS NOT NULL AND confirmed_mapping_pole_count IS NOT NULL AND confirmed_plant_health_pole_count IS NOT NULL AND confirmed_mapping_pole_count > 0 AND confirmed_plant_health_pole_count > 0 AND confirmed_plant_health_pole_count <= confirmed_mapping_pole_count AND mapping_subtotal IS NOT NULL AND plant_health_subtotal IS NOT NULL AND combo_discount_amount IS NOT NULL AND combo_discount_percentage IS NOT NULL AND mapping_subtotal = confirmed_mapping_pole_count * booked_mapping_price_per_pole AND plant_health_subtotal = confirmed_plant_health_pole_count * booked_plant_health_price_per_pole AND final_price IS NOT NULL AND final_price = round((mapping_subtotal + plant_health_subtotal) * (1 - combo_discount_percentage / 100), 0) AND combo_discount_amount = mapping_subtotal + plant_health_subtotal - final_price AND original_final_price IS NOT NULL AND original_final_price = mapping_subtotal + plant_health_subtotal AND original_survey_service_price_id IS NOT NULL AND original_survey_service_price_id = booked_service_price_id AND confirmed_survey_pole_count IS NOT NULL AND confirmed_survey_pole_count = confirmed_plant_health_pole_count)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "booked_mapping_price_per_pole IS NOT NULL OR scope = 'V3' OR final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders",
                sql: "booked_mapping_price_per_pole IS NOT NULL OR (original_final_price IS NULL AND original_survey_service_price_id IS NULL AND original_confirmed_pole_count IS NULL AND original_price_per_pole_snapshot IS NULL) OR (original_final_price = round(original_confirmed_pole_count * original_price_per_pole_snapshot, 2) AND original_survey_service_price_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_service_prices_combo_components",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_combo_component_booking",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_combo_component_invoice",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "mapping_price_per_pole",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "plant_health_price_per_pole",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "booked_mapping_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_plant_health_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "combo_discount_amount",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_mapping_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_plant_health_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "mapping_subtotal",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "plant_health_subtotal",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders",
                sql: "(original_final_price IS NULL AND original_survey_service_price_id IS NULL AND original_confirmed_pole_count IS NULL AND original_price_per_pole_snapshot IS NULL) OR (original_final_price = round(original_confirmed_pole_count * original_price_per_pole_snapshot, 2) AND original_survey_service_price_id IS NOT NULL)");
        }
    }
}
