using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

// Must run before Step8: main may already have created a different table with this name.
[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261111040000_StageLegacyDepositPoliciesForMerge")]
public sealed class StageLegacyDepositPoliciesForMerge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DO $merge$
        BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'survey' AND table_name = 'survey_deposit_policies'
                         AND column_name = 'amount_vnd') THEN
                IF to_regclass('survey.legacy_survey_deposit_policies_merge') IS NOT NULL THEN
                    RAISE EXCEPTION 'Legacy deposit merge staging table already exists; inspect both histories before retrying.';
                END IF;
                ALTER TABLE survey.survey_deposit_policies RENAME TO legacy_survey_deposit_policies_merge;
                ALTER TABLE survey.legacy_survey_deposit_policies_merge
                    RENAME CONSTRAINT pk_survey_deposit_policies TO pk_legacy_survey_deposit_policies_merge;
                ALTER INDEX IF EXISTS survey.uq_survey_deposit_policies_version
                    RENAME TO uq_legacy_survey_deposit_policies_merge_version;
                ALTER INDEX IF EXISTS survey.uq_survey_deposit_policies_current
                    RENAME TO uq_legacy_survey_deposit_policies_merge_current;
            END IF;
        END;
        $merge$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DO $merge$
        BEGIN
            IF to_regclass('survey.legacy_survey_deposit_policies_merge') IS NOT NULL THEN
                IF to_regclass('survey.survey_deposit_policies') IS NOT NULL THEN
                    RAISE EXCEPTION 'Cannot restore legacy deposits while the original table name is occupied.';
                END IF;
                ALTER TABLE survey.legacy_survey_deposit_policies_merge RENAME TO survey_deposit_policies;
                ALTER TABLE survey.survey_deposit_policies
                    RENAME CONSTRAINT pk_legacy_survey_deposit_policies_merge TO pk_survey_deposit_policies;
                ALTER INDEX IF EXISTS survey.uq_legacy_survey_deposit_policies_merge_version
                    RENAME TO uq_survey_deposit_policies_version;
                ALTER INDEX IF EXISTS survey.uq_legacy_survey_deposit_policies_merge_current
                    RENAME TO uq_survey_deposit_policies_current;
            END IF;
        END;
        $merge$;
        """);
}
