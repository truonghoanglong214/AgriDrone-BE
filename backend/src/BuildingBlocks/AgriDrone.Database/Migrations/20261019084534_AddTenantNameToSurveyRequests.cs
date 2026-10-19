using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantNameToSurveyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddColumn<string>(
                name: "tenant_name",
                schema: "survey",
                table: "survey_requests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE survey.survey_requests AS request
                SET tenant_name = COALESCE(
                    (
                        SELECT tenant.name
                        FROM survey.survey_orders AS survey_order
                        INNER JOIN identity.tenants AS tenant
                            ON tenant.id = survey_order.tenant_id
                        WHERE survey_order.survey_request_id = request.id
                        LIMIT 1
                    ),
                    request.applicant_name
                )
                WHERE request.kind = 'NEW_CUSTOMER'::system.survey_request_kind;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests",
                sql: "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL AND tenant_code IS NOT NULL AND tenant_name IS NOT NULL) OR (kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL AND tenant_name IS NULL) OR (kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL AND tenant_name IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.DropColumn(
                name: "tenant_name",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_kind_context",
                schema: "survey",
                table: "survey_requests",
                sql: "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL AND tenant_code IS NOT NULL) OR (kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL) OR (kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL AND tenant_code IS NULL)");
        }
    }
}
