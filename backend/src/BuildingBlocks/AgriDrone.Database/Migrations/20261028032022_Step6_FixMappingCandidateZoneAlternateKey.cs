using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step6_FixMappingCandidateZoneAlternateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mapping_candidates_zone",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropIndex(
                name: "IX_mapping_candidates_zone_id",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_mapping_candidate_zones_zone_id",
                schema: "farm",
                table: "mapping_candidate_zones");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_mapping_candidate_zones_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidate_zones",
                columns: new[] { "batch_id", "zone_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mapping_candidates_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "zone_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_mapping_candidates_candidate_zone",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "zone_id" },
                principalSchema: "farm",
                principalTable: "mapping_candidate_zones",
                principalColumns: new[] { "batch_id", "zone_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mapping_candidates_candidate_zone",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropIndex(
                name: "IX_mapping_candidates_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_mapping_candidate_zones_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidate_zones");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_mapping_candidate_zones_zone_id",
                schema: "farm",
                table: "mapping_candidate_zones",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_mapping_candidates_zone_id",
                schema: "farm",
                table: "mapping_candidates",
                column: "zone_id");

            migrationBuilder.AddForeignKey(
                name: "fk_mapping_candidates_zone",
                schema: "farm",
                table: "mapping_candidates",
                column: "zone_id",
                principalSchema: "farm",
                principalTable: "mapping_candidate_zones",
                principalColumn: "zone_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
