START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    ALTER TABLE survey.survey_orders ADD booked_price_per_pole numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    ALTER TABLE survey.survey_orders ADD booked_service_price_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    ALTER TABLE survey.survey_orders ADD booked_service_type system.survey_service_type;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    ALTER TABLE survey.survey_orders ADD post_flight_settlement boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    ALTER TABLE survey.survey_orders ADD service_reviewed_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM system.__ef_migrations_history WHERE "MigrationId" = '20261113075731_RegistrationV26PostFlightSettlement') THEN
    INSERT INTO system.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20261113075731_RegistrationV26PostFlightSettlement', '10.0.10');
    END IF;
END $EF$;
COMMIT;

