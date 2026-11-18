ALTER TABLE plant.plant_change_events
ADD COLUMN IF NOT EXISTS farm_base_map_version_id UUID;

DO $do$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_plant_change_events_farm_base_map'
    ) THEN
        ALTER TABLE plant.plant_change_events
        ADD CONSTRAINT fk_plant_change_events_farm_base_map
        FOREIGN KEY (farm_base_map_version_id)
        REFERENCES farm.farm_base_map_versions(id)
        ON DELETE SET NULL;
    END IF;
END $do$;

CREATE INDEX IF NOT EXISTS ix_plant_change_events_farm_base_map
ON plant.plant_change_events(farm_base_map_version_id);

