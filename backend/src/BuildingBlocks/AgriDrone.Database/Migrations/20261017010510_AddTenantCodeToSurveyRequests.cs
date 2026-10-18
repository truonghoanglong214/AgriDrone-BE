using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCodeToSurveyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddColumn<string>(
                name: "tenant_code",
                schema: "survey",
                table: "survey_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE survey.survey_requests AS request
                SET tenant_code = COALESCE(
                    (
                        SELECT tenant.code
                        FROM survey.survey_orders AS survey_order
                        INNER JOIN identity.tenants AS tenant
                            ON tenant.id = survey_order.tenant_id
                        WHERE survey_order.survey_request_id = request.id
                        LIMIT 1
                    ),
                    'TEN-' || UPPER(LEFT(REPLACE(request.id::text, '-', ''), 26))
                )
                WHERE request.kind = 'NEW_CUSTOMER'::system.survey_request_kind;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_survey_requests_active_tenant_code",
                schema: "survey",
                table: "survey_requests",
                column: "tenant_code",
                unique: true,
                filter: "tenant_code IS NOT NULL AND status IN ('SUBMITTED'::system.survey_request_status, 'UNDER_REVIEW'::system.survey_request_status, 'APPROVED'::system.survey_request_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests",
                sql: "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL AND tenant_code IS NOT NULL) OR (kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL) OR (kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_survey_requests_active_tenant_code",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.DropColumn(
                name: "tenant_code",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests",
                sql: "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL) OR (kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL) OR (kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL)");
        }
    }
}
