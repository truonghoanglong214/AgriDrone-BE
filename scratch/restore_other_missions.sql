BEGIN;

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES 
        ('eceb1c85-00c3-4954-9cc7-097f3a6506b2', 'ecfd71e0-19df-4e7a-a493-ba6208495761', '74759685-c439-47df-8c07-e7e1c9712d0a', 'minio', 'tenants/ecfd71e0-19df-4e7a-a493-ba6208495761/media/7e5b1b77-4746-45dd-84e5-d5252782c16b/orthophoto.tif', 'minio://agridrone-media/tenants/ecfd71e0-19df-4e7a-a493-ba6208495761/media/7e5b1b77-4746-45dd-84e5-d5252782c16b/orthophoto.tif', 'IMAGE', 'image/tiff', 2126042, '{}'::jsonb, 'ACTIVE', now(), now()),
        ('e6f7b710-69d9-4bbb-b365-c3935adbe6e4', 'ecfd71e0-19df-4e7a-a493-ba6208495761', '74759685-c439-47df-8c07-e7e1c9712d0a', 'minio', 'tenants/ecfd71e0-19df-4e7a-a493-ba6208495761/media/7e5b1b77-4746-45dd-84e5-d5252782c16b/map_preview.jpg', 'minio://agridrone-media/tenants/ecfd71e0-19df-4e7a-a493-ba6208495761/media/7e5b1b77-4746-45dd-84e5-d5252782c16b/map_preview.jpg', 'IMAGE', 'image/jpeg', 251737, '{"bounds": {"min_lat": 10.4612343, "max_lat": 10.4615936, "min_lon": 106.4938255, "max_lon": 106.4943916}, "corners": {"north_west": [10.4615936, 106.4938255], "north_east": [10.4615936, 106.4943916], "south_east": [10.4612343, 106.4943916], "south_west": [10.4612343, 106.4938255]}}'::jsonb, 'ACTIVE', now(), now());

        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES 
        ('7e5b1b77-4746-45dd-84e5-d5252782c16b', 'eceb1c85-00c3-4954-9cc7-097f3a6506b2', 'PROCESSED_IMAGE', now()),
        ('7e5b1b77-4746-45dd-84e5-d5252782c16b', 'e6f7b710-69d9-4bbb-b365-c3935adbe6e4', 'PROCESSED_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES 
        ('1ad8e1f1-15ce-49c3-b8f1-32129b1d1ba7', '42d570f5-1424-41ad-82e4-5819d14a0f93', 'ea20366f-41ff-40e1-ba96-6e0dabf5b83c', 'minio', 'tenants/42d570f5-1424-41ad-82e4-5819d14a0f93/media/3366b87f-c642-4322-a51f-99f96e390a0e/orthophoto.tif', 'minio://agridrone-media/tenants/42d570f5-1424-41ad-82e4-5819d14a0f93/media/3366b87f-c642-4322-a51f-99f96e390a0e/orthophoto.tif', 'IMAGE', 'image/tiff', 2126042, '{}'::jsonb, 'ACTIVE', now(), now()),
        ('c3099c8b-f01b-4739-b1bb-a645aa22bfff', '42d570f5-1424-41ad-82e4-5819d14a0f93', 'ea20366f-41ff-40e1-ba96-6e0dabf5b83c', 'minio', 'tenants/42d570f5-1424-41ad-82e4-5819d14a0f93/media/3366b87f-c642-4322-a51f-99f96e390a0e/map_preview.jpg', 'minio://agridrone-media/tenants/42d570f5-1424-41ad-82e4-5819d14a0f93/media/3366b87f-c642-4322-a51f-99f96e390a0e/map_preview.jpg', 'IMAGE', 'image/jpeg', 251737, '{"bounds": {"min_lat": 10.4612343, "max_lat": 10.4615936, "min_lon": 106.4938255, "max_lon": 106.4943916}, "corners": {"north_west": [10.4615936, 106.4938255], "north_east": [10.4615936, 106.4943916], "south_east": [10.4612343, 106.4943916], "south_west": [10.4612343, 106.4938255]}}'::jsonb, 'ACTIVE', now(), now());

        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES 
        ('3366b87f-c642-4322-a51f-99f96e390a0e', '1ad8e1f1-15ce-49c3-b8f1-32129b1d1ba7', 'PROCESSED_IMAGE', now()),
        ('3366b87f-c642-4322-a51f-99f96e390a0e', 'c3099c8b-f01b-4739-b1bb-a645aa22bfff', 'PROCESSED_IMAGE', now());
    
COMMIT;
