using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step8C_V3RequestQuoteAndTrackingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_quote_snapshots",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    survey_service_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_service_revision_version = table.Column<int>(type: "integer", nullable: false),
                    price_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_version_number = table.Column<int>(type: "integer", nullable: false),
                    price_per_pole = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    deposit_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_version_number = table.Column<int>(type: "integer", nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estimated_pole_count = table.Column<int>(type: "integer", nullable: true),
                    indicative_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_quote_snapshots", x => x.id);
                    table.CheckConstraint("ck_request_quote_snapshots_currency", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_request_quote_snapshots_deposit", "deposit_amount > 0 AND deposit_amount = FLOOR(deposit_amount)");
                    table.CheckConstraint("ck_request_quote_snapshots_poles", "estimated_pole_count IS NULL OR estimated_pole_count > 0");
                    table.CheckConstraint("ck_request_quote_snapshots_price", "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                    table.ForeignKey(
                        name: "fk_request_quote_snapshots_deposit_policy",
                        column: x => x.deposit_policy_id,
                        principalSchema: "survey",
                        principalTable: "survey_deposit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_quote_snapshots_policy_version",
                        column: x => x.business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_quote_snapshots_price_version",
                        column: x => x.price_version_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_quote_snapshots_request",
                        column: x => x.survey_request_id,
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_quote_snapshots_service_revision",
                        column: x => x.survey_service_revision_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_request_tracking",
                schema: "survey",
                columns: table => new
                {
                    survey_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_token = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_request_tracking", x => x.survey_request_id);
                    table.ForeignKey(
                        name: "fk_survey_request_tracking_request",
                        column: x => x.survey_request_id,
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_quote_snapshots_business_policy_version_id",
                schema: "survey",
                table: "request_quote_snapshots",
                column: "business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_quote_snapshots_deposit_policy_id",
                schema: "survey",
                table: "request_quote_snapshots",
                column: "deposit_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_quote_snapshots_price_version_id",
                schema: "survey",
                table: "request_quote_snapshots",
                column: "price_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_quote_snapshots_survey_service_revision_id",
                schema: "survey",
                table: "request_quote_snapshots",
                column: "survey_service_revision_id");

            migrationBuilder.CreateIndex(
                name: "uq_request_quote_snapshots_request",
                schema: "survey",
                table: "request_quote_snapshots",
                column: "survey_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_request_tracking_hash",
                schema: "survey",
                table: "survey_request_tracking",
                column: "tracking_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_quote_snapshots",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_request_tracking",
                schema: "survey");
        }
    }
}
