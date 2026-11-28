using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261128200000_V3ComboInvoiceComponentPricing")]
public sealed class V3ComboInvoiceComponentPricing : Migration
{
    private const string Schema = "survey";
    private const string Table = "survey_invoice_revisions";
    private const string CommercialTable = "order_commercial_snapshots";
    private const string AmountsConstraint = "ck_survey_invoice_revisions_amounts";
    private const string ComponentsConstraint = "ck_survey_invoice_revisions_components";
    private const string HealthAreaConstraint = "ck_survey_invoice_revisions_health_area";
    private const string CommercialRatesConstraint = "ck_order_commercial_snapshots_component_rates";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("mapping_price_per_pole", CommercialTable, type: "numeric(18,2)", schema: Schema, nullable: true);
        migrationBuilder.AddColumn<decimal>("plant_health_price_per_pole", CommercialTable, type: "numeric(18,2)", schema: Schema, nullable: true);
        migrationBuilder.AddCheckConstraint(CommercialRatesConstraint, CommercialTable,
            "(mapping_price_per_pole IS NULL AND plant_health_price_per_pole IS NULL) OR (service_code = 'COMBO' AND mapping_price_per_pole IS NOT NULL AND plant_health_price_per_pole IS NOT NULL AND mapping_price_per_pole > 0 AND mapping_price_per_pole = FLOOR(mapping_price_per_pole) AND plant_health_price_per_pole > 0 AND plant_health_price_per_pole = FLOOR(plant_health_price_per_pole) AND price_per_pole = mapping_price_per_pole + plant_health_price_per_pole)", schema: Schema);

        migrationBuilder.AddColumn<int>("plant_health_pole_count", Table, schema: Schema, nullable: true);
        migrationBuilder.AddColumn<decimal>("mapping_price_per_pole", Table, type: "numeric(18,2)", schema: Schema, nullable: true);
        migrationBuilder.AddColumn<decimal>("plant_health_price_per_pole", Table, type: "numeric(18,2)", schema: Schema, nullable: true);
        migrationBuilder.AddColumn<Polygon>("plant_health_survey_area", Table, type: "geometry(Polygon,4326)", schema: Schema, nullable: true);
        migrationBuilder.DropCheckConstraint(AmountsConstraint, Table, schema: Schema);
        migrationBuilder.AddCheckConstraint(AmountsConstraint, Table,
            "total_amount >= 0 AND total_amount = FLOOR(total_amount) AND total_amount = CASE WHEN plant_health_pole_count IS NULL THEN verified_billable_pole_count * price_per_pole ELSE verified_billable_pole_count * mapping_price_per_pole + plant_health_pole_count * plant_health_price_per_pole END AND valid_allocated_deposit_net >= 0 AND valid_allocated_deposit_net = FLOOR(valid_allocated_deposit_net) AND due_amount >= 0 AND due_amount = FLOOR(due_amount) AND due_amount = GREATEST(0, total_amount - valid_allocated_deposit_net) AND overpayment_amount >= 0 AND overpayment_amount = FLOOR(overpayment_amount) AND overpayment_amount = GREATEST(0, valid_allocated_deposit_net - total_amount)", schema: Schema);
        migrationBuilder.AddCheckConstraint(ComponentsConstraint, Table,
            "(plant_health_pole_count IS NULL AND mapping_price_per_pole IS NULL AND plant_health_price_per_pole IS NULL) OR (plant_health_pole_count IS NOT NULL AND mapping_price_per_pole IS NOT NULL AND plant_health_price_per_pole IS NOT NULL AND plant_health_pole_count BETWEEN 0 AND verified_billable_pole_count AND mapping_price_per_pole > 0 AND mapping_price_per_pole = FLOOR(mapping_price_per_pole) AND plant_health_price_per_pole > 0 AND plant_health_price_per_pole = FLOOR(plant_health_price_per_pole) AND price_per_pole = mapping_price_per_pole + plant_health_price_per_pole)", schema: Schema);
        migrationBuilder.AddCheckConstraint(HealthAreaConstraint, Table,
            "plant_health_survey_area IS NULL OR (plant_health_pole_count IS NOT NULL AND NOT ST_IsEmpty(plant_health_survey_area) AND ST_IsValid(plant_health_survey_area))", schema: Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM survey.survey_invoice_revisions WHERE plant_health_pole_count IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM survey.order_commercial_snapshots WHERE mapping_price_per_pole IS NOT NULL) THEN
                    RAISE EXCEPTION 'Component pricing cannot be removed after component orders or invoices exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint(ComponentsConstraint, Table, schema: Schema);
        migrationBuilder.DropCheckConstraint(HealthAreaConstraint, Table, schema: Schema);
        migrationBuilder.DropCheckConstraint(AmountsConstraint, Table, schema: Schema);
        migrationBuilder.DropCheckConstraint(CommercialRatesConstraint, CommercialTable, schema: Schema);
        migrationBuilder.DropColumn("mapping_price_per_pole", CommercialTable, schema: Schema);
        migrationBuilder.DropColumn("plant_health_price_per_pole", CommercialTable, schema: Schema);
        migrationBuilder.DropColumn("plant_health_pole_count", Table, schema: Schema);
        migrationBuilder.DropColumn("mapping_price_per_pole", Table, schema: Schema);
        migrationBuilder.DropColumn("plant_health_price_per_pole", Table, schema: Schema);
        migrationBuilder.DropColumn("plant_health_survey_area", Table, schema: Schema);
        migrationBuilder.AddCheckConstraint(AmountsConstraint, Table, "total_amount >= 0 AND total_amount = FLOOR(total_amount) AND total_amount = verified_billable_pole_count * price_per_pole AND valid_allocated_deposit_net >= 0 AND valid_allocated_deposit_net = FLOOR(valid_allocated_deposit_net) AND due_amount >= 0 AND due_amount = FLOOR(due_amount) AND due_amount = GREATEST(0, total_amount - valid_allocated_deposit_net) AND overpayment_amount >= 0 AND overpayment_amount = FLOOR(overpayment_amount) AND overpayment_amount = GREATEST(0, valid_allocated_deposit_net - total_amount)", schema: Schema);
    }
}
