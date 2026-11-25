using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

public sealed partial class IdentityRefreshSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "authentication_version",
            schema: "identity",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddCheckConstraint(
            name: "ck_users_authentication_version",
            schema: "identity",
            table: "users",
            sql: "authentication_version >= 1");

        migrationBuilder.AddUniqueConstraint(
            name: "uq_tenant_memberships_id_tenant_user",
            schema: "identity",
            table: "tenant_memberships",
            columns: new[] { "id", "tenant_id", "user_id" });

        migrationBuilder.CreateTable(
            name: "refresh_sessions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                tenant_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                authentication_version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revocation_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_sessions", x => x.id);
                table.CheckConstraint(
                    "ck_refresh_sessions_scope_pair",
                    "(tenant_id IS NULL AND tenant_membership_id IS NULL) OR (tenant_id IS NOT NULL AND tenant_membership_id IS NOT NULL)");
                table.CheckConstraint(
                    "ck_refresh_sessions_absolute_expiration",
                    "absolute_expires_at > created_at");
                table.CheckConstraint(
                    "ck_refresh_sessions_auth_version",
                    "authentication_version >= 1");
                table.CheckConstraint(
                    "ck_refresh_sessions_revocation",
                    "(revoked_at IS NULL AND revocation_reason IS NULL) OR (revoked_at IS NOT NULL AND revocation_reason IS NOT NULL)");
                table.ForeignKey(
                    name: "fk_refresh_sessions_user",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_refresh_sessions_membership",
                    columns: x => new { x.tenant_membership_id, x.tenant_id, x.user_id },
                    principalSchema: "identity",
                    principalTable: "tenant_memberships",
                    principalColumns: new[] { "id", "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Server-side refresh authentication sessions binding scope and auth version.");

        migrationBuilder.CreateTable(
            name: "refresh_session_tokens",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                predecessor_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_session_tokens", x => x.id);
                table.UniqueConstraint("uq_refresh_session_tokens_id_session", x => new { x.id, x.session_id });
                table.CheckConstraint(
                    "ck_refresh_session_tokens_hash",
                    "token_hash ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint(
                    "ck_refresh_session_tokens_generation",
                    "(generation = 1 AND predecessor_token_id IS NULL) OR (generation > 1 AND predecessor_token_id IS NOT NULL AND predecessor_token_id <> id)");
                table.CheckConstraint(
                    "ck_refresh_session_tokens_expiration",
                    "expires_at > issued_at");
                table.ForeignKey(
                    name: "fk_refresh_session_tokens_session",
                    column: x => x.session_id,
                    principalSchema: "identity",
                    principalTable: "refresh_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_refresh_session_tokens_predecessor_session",
                    columns: x => new { x.predecessor_token_id, x.session_id },
                    principalSchema: "identity",
                    principalTable: "refresh_session_tokens",
                    principalColumns: new[] { "id", "session_id" },
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Append-only rotated refresh token hashes retained for reuse detection.");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_sessions_user_active",
            schema: "identity",
            table: "refresh_sessions",
            column: "user_id",
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_sessions_absolute_expires",
            schema: "identity",
            table: "refresh_sessions",
            column: "absolute_expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_sessions_membership",
            schema: "identity",
            table: "refresh_sessions",
            columns: new[] { "tenant_membership_id", "tenant_id", "user_id" });

        migrationBuilder.CreateIndex(
            name: "uq_refresh_session_tokens_hash",
            schema: "identity",
            table: "refresh_session_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_refresh_session_tokens_session_generation",
            schema: "identity",
            table: "refresh_session_tokens",
            columns: new[] { "session_id", "generation" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_refresh_session_tokens_one_unused_per_session",
            schema: "identity",
            table: "refresh_session_tokens",
            column: "session_id",
            unique: true,
            filter: "used_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "uq_refresh_session_tokens_predecessor",
            schema: "identity",
            table: "refresh_session_tokens",
            columns: new[] { "session_id", "predecessor_token_id" },
            unique: true,
            filter: "predecessor_token_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_session_tokens_expires",
            schema: "identity",
            table: "refresh_session_tokens",
            column: "expires_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "refresh_session_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "refresh_sessions",
            schema: "identity");

        migrationBuilder.DropUniqueConstraint(
            name: "uq_tenant_memberships_id_tenant_user",
            schema: "identity",
            table: "tenant_memberships");

        migrationBuilder.DropCheckConstraint(
            name: "ck_users_authentication_version",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "authentication_version",
            schema: "identity",
            table: "users");
    }
}
