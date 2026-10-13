using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowPlatformScopedMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "system",
                table: "outbox_messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "system",
                table: "inbox_messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_messages_tenant_scope",
                schema: "system",
                table: "outbox_messages",
                sql: "tenant_id IS NOT NULL OR event_type = 'notification.email-requested.v1'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inbox_messages_tenant_scope",
                schema: "system",
                table: "inbox_messages",
                sql: "tenant_id IS NOT NULL OR event_type = 'notification.email-requested.v1'");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.survey_requests
                        WHERE estimated_pole_count = 0
                    ) THEN
                        RAISE EXCEPTION 'Cannot require positive estimated pole counts while zero-valued survey requests exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_pole_count",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_pole_count",
                schema: "survey",
                table: "survey_requests",
                sql: "estimated_pole_count IS NULL OR estimated_pole_count > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_messages_tenant_scope",
                schema: "system",
                table: "outbox_messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inbox_messages_tenant_scope",
                schema: "system",
                table: "inbox_messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_requests_pole_count",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_requests_pole_count",
                schema: "survey",
                table: "survey_requests",
                sql: "estimated_pole_count IS NULL OR estimated_pole_count >= 0");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM system.outbox_messages
                        WHERE tenant_id IS NULL
                    ) OR EXISTS (
                        SELECT 1
                        FROM system.inbox_messages
                        WHERE tenant_id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back platform-scoped messaging while tenantless Inbox/Outbox rows exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "system",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "system",
                table: "inbox_messages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
