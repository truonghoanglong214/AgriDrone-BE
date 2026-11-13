using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

// Runs after all Step8 FKs are created and before main's payment-policy migrations.
[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261111150000_SeparateLegacyAndV3DepositPolicies")]
public sealed class SeparateLegacyAndV3DepositPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DO $merge$
        BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'survey' AND table_name = 'survey_deposit_policies'
                         AND column_name = 'amount') THEN
                IF to_regclass('survey.v3_survey_deposit_policies') IS NOT NULL THEN
                    RAISE EXCEPTION 'Both V3 deposit tables exist; reconcile their immutable identities before retrying.';
                END IF;
                -- PostgreSQL preserves rows and retargets existing snapshot/bundle/self FKs on rename.
                ALTER TABLE survey.survey_deposit_policies RENAME TO v3_survey_deposit_policies;
                ALTER TABLE survey.v3_survey_deposit_policies
                    RENAME CONSTRAINT pk_survey_deposit_policies TO pk_v3_survey_deposit_policies;
                ALTER INDEX survey.uq_survey_deposit_policies_version RENAME TO uq_v3_survey_deposit_policies_version;
                ALTER INDEX survey.ix_survey_deposit_policies_effective RENAME TO ix_v3_survey_deposit_policies_effective;
                ALTER INDEX survey."IX_survey_deposit_policies_predecessor_version_id"
                    RENAME TO "IX_v3_survey_deposit_policies_predecessor_version_id";
            END IF;
            IF to_regclass('survey.v3_survey_deposit_policies') IS NULL THEN
                RAISE EXCEPTION 'V3 deposit schema is missing; apply the Step8 foundation before continuing.';
            END IF;
            IF to_regclass('survey.legacy_survey_deposit_policies_merge') IS NOT NULL THEN
                IF to_regclass('survey.survey_deposit_policies') IS NOT NULL THEN
                    RAISE EXCEPTION 'Cannot restore staged legacy deposits: original table name is occupied.';
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

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DO $merge$
        BEGIN
            IF to_regclass('survey.survey_deposit_policies') IS NOT NULL THEN
                RAISE EXCEPTION 'Roll back main payment-policy migrations before restoring the Step8 table name.';
            END IF;
            ALTER TABLE survey.v3_survey_deposit_policies RENAME TO survey_deposit_policies;
            ALTER TABLE survey.survey_deposit_policies
                RENAME CONSTRAINT pk_v3_survey_deposit_policies TO pk_survey_deposit_policies;
            ALTER INDEX survey.uq_v3_survey_deposit_policies_version RENAME TO uq_survey_deposit_policies_version;
            ALTER INDEX survey.ix_v3_survey_deposit_policies_effective RENAME TO ix_survey_deposit_policies_effective;
            ALTER INDEX survey."IX_v3_survey_deposit_policies_predecessor_version_id"
                RENAME TO "IX_survey_deposit_policies_predecessor_version_id";
        END;
        $merge$;
        """);
}
