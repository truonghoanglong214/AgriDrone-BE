DO $$
DECLARE
    v_mission_id uuid := '8d2ba93d-1421-448d-be83-a1034e8c4aab';
    v_tenant_id uuid := 'cf637409-fb85-4dfd-8fd3-ae3ea799d635';
    v_farm_id uuid := '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d';
    v_tif_id uuid := gen_random_uuid();
    v_jpg_id uuid := gen_random_uuid();
BEGIN
    INSERT INTO media.media_assets (
        id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, checksum, metadata, storage_status, created_at, updated_at
    ) VALUES (
        v_tif_id, v_tenant_id, v_farm_id, 'minio',
        'tenants/' || v_tenant_id || '/media/' || v_mission_id || '/orthophoto.tif',
        'minio://agridrone-media/tenants/' || v_tenant_id || '/media/' || v_mission_id || '/orthophoto.tif',
        'IMAGE', 'image/tiff', 2126042, 'e05f6e392576b5cf16629e46a7ce3286f345ae3f97fb5625bfd3369a2fc4bf5b', '{}'::jsonb, 'ACTIVE', now(), now()
    );

    INSERT INTO media.media_assets (
        id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, width_px, height_px, checksum, metadata, storage_status, created_at, updated_at
    ) VALUES (
        v_jpg_id, v_tenant_id, v_farm_id, 'minio',
        'tenants/' || v_tenant_id || '/media/' || v_mission_id || '/map_preview.jpg',
        'minio://agridrone-media/tenants/' || v_tenant_id || '/media/' || v_mission_id || '/map_preview.jpg',
        'IMAGE', 'image/jpeg', 251737, 1236, 789, '6a27e77747e928236353d20a0bfa286392095fa73507d3fc4b7fe4e12c4bbfef',
        '{"bounds": {"min_lat": 10.4612343, "max_lat": 10.4615936, "min_lon": 106.4938255, "max_lon": 106.4943916}, "corners": {"north_west": [10.4615936, 106.4938255], "north_east": [10.4615936, 106.4943916], "south_east": [10.4612343, 106.4943916], "south_west": [10.4612343, 106.4938255]}, "preview_width": 1236, "preview_height": 789, "original_width": 1236, "original_height": 789}'::jsonb,
        'ACTIVE', now(), now()
    );

    INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
    VALUES 
        (v_mission_id, v_tif_id, 'PROCESSED_IMAGE', now()),
        (v_mission_id, v_jpg_id, 'PROCESSED_IMAGE', now());
END $$;
