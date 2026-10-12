using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase9PaidAppointmentPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_event_id",
                schema: "survey",
                table: "payment_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_reference",
                schema: "survey",
                table: "payment_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE survey.survey_payments
                SET provider_reference = 'legacy:' || id::text
                WHERE provider_reference IS NULL
                  AND status <> 'PENDING'::system.survey_payment_status;

                UPDATE survey.payment_events AS payment_event
                SET provider_reference = COALESCE(
                        NULLIF(payment_event.payload ->> 'providerReference', ''),
                        payment.provider_reference,
                        'legacy-payment:' || payment_event.survey_payment_id::text),
                    provider_event_id = COALESCE(
                        NULLIF(payment_event.payload ->> 'eventId', ''),
                        'legacy-event:' || payment_event.id::text)
                FROM survey.survey_payments AS payment
                WHERE payment.id = payment_event.survey_payment_id;

                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.payment_events
                        WHERE provider_reference IS NULL
                           OR provider_event_id IS NULL)
                    THEN
                        RAISE EXCEPTION
                            'Phase 9D migration blocked: payment event identity backfill is incomplete.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "provider_event_id",
                schema: "survey",
                table: "payment_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "provider_reference",
                schema: "survey",
                table: "payment_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments",
                sql: "status = 'PENDING'::system.survey_payment_status OR provider_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_payment_events_provider_reference_event",
                schema: "survey",
                table: "payment_events",
                columns: new[] { "provider", "provider_reference", "provider_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropIndex(
                name: "uq_payment_events_provider_reference_event",
                schema: "survey",
                table: "payment_events");

            migrationBuilder.DropColumn(
                name: "provider_event_id",
                schema: "survey",
                table: "payment_events");

            migrationBuilder.DropColumn(
                name: "provider_reference",
                schema: "survey",
                table: "payment_events");
        }
    }
}
