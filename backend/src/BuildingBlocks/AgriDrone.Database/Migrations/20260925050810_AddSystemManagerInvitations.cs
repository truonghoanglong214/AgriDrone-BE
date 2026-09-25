using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemManagerInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_manager_invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 320, nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_manager_invitations", x => x.id);
                    table.CheckConstraint("ck_system_manager_invitations_expiration", "expires_at > created_at");
                    table.ForeignKey(
                        name: "fk_system_manager_invitations_accepted_by_user",
                        column: x => x.accepted_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_system_manager_invitations_invited_by_user",
                        column: x => x.invited_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Single-use invitations for System Manager accounts.");

            migrationBuilder.CreateIndex(
                name: "IX_system_manager_invitations_accepted_by_user_id",
                schema: "identity",
                table: "system_manager_invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_system_manager_invitations_invited_by_user_id",
                schema: "identity",
                table: "system_manager_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_manager_invitations_status_expiration",
                schema: "identity",
                table: "system_manager_invitations",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_system_manager_invitations_pending_email",
                schema: "identity",
                table: "system_manager_invitations",
                column: "email",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "uq_system_manager_invitations_token_hash",
                schema: "identity",
                table: "system_manager_invitations",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_manager_invitations",
                schema: "identity");
        }
    }
}
