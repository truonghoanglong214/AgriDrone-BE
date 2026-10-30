BEGIN;

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c787bfc3-3bc8-43bf-b235-f4d017cff4ae', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174984_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174984_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2274405, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c787bfc3-3bc8-43bf-b235-f4d017cff4ae', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'be783b97-79bc-4bbc-83d7-1b1a41efc573', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174985_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174985_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308658, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'be783b97-79bc-4bbc-83d7-1b1a41efc573', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f0132873-d7da-40b4-98c6-f82f79fcc467', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174986_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174986_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2307648, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f0132873-d7da-40b4-98c6-f82f79fcc467', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '0134b971-e5ca-4d42-910d-0235632e77d1', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174987_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174987_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335665, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '0134b971-e5ca-4d42-910d-0235632e77d1', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b5b21f21-d828-4761-9a4e-1215b5e9cb9d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174988_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174988_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2279304, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b5b21f21-d828-4761-9a4e-1215b5e9cb9d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '9d745f62-9c73-4d1f-b63e-b976d8472ddf', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174989_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174989_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2275012, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '9d745f62-9c73-4d1f-b63e-b976d8472ddf', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '74a02dcd-f83b-4451-8984-33ce3267c33f', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174990_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174990_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2310378, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '74a02dcd-f83b-4451-8984-33ce3267c33f', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '384e7474-d397-44a8-9d13-20c94691388f', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174991_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174991_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2283300, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '384e7474-d397-44a8-9d13-20c94691388f', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3af23970-2347-433e-8776-d9c2290ac6a1', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174992_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174992_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316602, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3af23970-2347-433e-8776-d9c2290ac6a1', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '16a73b52-0295-4b83-8a7f-50dd12bb1087', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174993_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174993_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2300994, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '16a73b52-0295-4b83-8a7f-50dd12bb1087', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c6c0c8d4-97f2-4a3b-99e0-c3a9083e53bd', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174994_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174994_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327919, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c6c0c8d4-97f2-4a3b-99e0-c3a9083e53bd', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f1a4de0b-78df-4f2c-a8ea-d4b1d392bb96', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174995_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174995_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2310065, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f1a4de0b-78df-4f2c-a8ea-d4b1d392bb96', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'fbe06982-830b-43b4-b5d9-abe09f1856fa', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174996_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174996_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2305201, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'fbe06982-830b-43b4-b5d9-abe09f1856fa', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e569eae5-f11a-40ea-9bec-4d2a51175a2b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174997_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174997_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321903, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e569eae5-f11a-40ea-9bec-4d2a51175a2b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '0e99cea0-0f53-4caf-9e4f-85d46a95319c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174998_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174998_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308725, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '0e99cea0-0f53-4caf-9e4f-85d46a95319c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd9d982d6-d5aa-4e9e-82ac-150dff4f04aa', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174999_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0174999_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2297770, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd9d982d6-d5aa-4e9e-82ac-150dff4f04aa', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3af66f7c-23bf-4706-ab4a-20e45f1b31be', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175000_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175000_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316858, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3af66f7c-23bf-4706-ab4a-20e45f1b31be', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '1c792a8e-308a-4698-a9ac-bc9e8d10b333', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175001_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175001_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2294210, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '1c792a8e-308a-4698-a9ac-bc9e8d10b333', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '844e93b1-5a79-4801-8388-a6161f7ca4d2', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175002_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175002_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308177, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '844e93b1-5a79-4801-8388-a6161f7ca4d2', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a3392a6b-bf79-4f85-8398-391167f6e152', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175003_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175003_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2318108, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a3392a6b-bf79-4f85-8398-391167f6e152', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '247b1e9d-2e75-499c-88c1-d471f7b941f7', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175004_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175004_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2298822, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '247b1e9d-2e75-499c-88c1-d471f7b941f7', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '845f2bf8-fa5c-46db-ade6-82be6d6c1957', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175005_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175005_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2314892, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '845f2bf8-fa5c-46db-ade6-82be6d6c1957', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '297ff949-b4aa-4322-b130-2e9519abd83a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175006_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175006_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311857, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '297ff949-b4aa-4322-b130-2e9519abd83a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '18156d6f-3d4d-4287-b84b-8f8031c5003a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175007_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175007_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316209, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '18156d6f-3d4d-4287-b84b-8f8031c5003a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '9cc7a007-8ab9-4fac-9775-6d3e69b9dcf8', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175008_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175008_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319050, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '9cc7a007-8ab9-4fac-9775-6d3e69b9dcf8', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'fd627668-62e5-4925-ae39-7179d97bcf1b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175009_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175009_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311207, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'fd627668-62e5-4925-ae39-7179d97bcf1b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b47b7fe1-917c-4422-a195-47205663b148', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175010_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175010_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2299265, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b47b7fe1-917c-4422-a195-47205663b148', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '24cfb32c-4f2f-4f23-875a-51f1a962ec05', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175011_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175011_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311970, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '24cfb32c-4f2f-4f23-875a-51f1a962ec05', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '2d0309c2-fa19-4df5-b33d-d9d25e4bbd46', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175012_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175012_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2328824, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '2d0309c2-fa19-4df5-b33d-d9d25e4bbd46', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '128e7b7c-2b14-4348-b655-fa0f9c53a7d4', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175013_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175013_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2299412, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '128e7b7c-2b14-4348-b655-fa0f9c53a7d4', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '36c4b6b4-b463-4653-8a2f-11b85a6098c1', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175014_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175014_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319258, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '36c4b6b4-b463-4653-8a2f-11b85a6098c1', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '391c0a28-083c-4409-ba98-1cb223dbc0c5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175015_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175015_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327203, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '391c0a28-083c-4409-ba98-1cb223dbc0c5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b991b12d-4077-4384-9d0a-066fdf5d00ef', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175016_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175016_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2307027, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b991b12d-4077-4384-9d0a-066fdf5d00ef', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '72325d37-8be6-471d-ad3f-5314f8c6dacb', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175017_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175017_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336661, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '72325d37-8be6-471d-ad3f-5314f8c6dacb', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '80139617-b46f-4821-b91a-6c6bfe4a3523', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175018_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175018_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2345160, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '80139617-b46f-4821-b91a-6c6bfe4a3523', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '49280cb5-3530-4a64-85f1-4b54cdc9290f', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175019_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175019_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2354655, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '49280cb5-3530-4a64-85f1-4b54cdc9290f', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '80da7f16-0f92-4c45-a5b8-d6bbc60109a0', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175020_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175020_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327770, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '80da7f16-0f92-4c45-a5b8-d6bbc60109a0', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd5572035-9276-4240-84a9-3df2910f657b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175021_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175021_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2331022, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd5572035-9276-4240-84a9-3df2910f657b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '2f6c13ed-4158-478d-8dec-6320f39fa7bc', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175022_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175022_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2320558, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '2f6c13ed-4158-478d-8dec-6320f39fa7bc', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3a724fcf-9986-4055-824b-717e8e1a496b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175023_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175023_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327309, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3a724fcf-9986-4055-824b-717e8e1a496b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5193aac6-3dc1-4f52-b746-8be229552dce', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175024_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175024_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2302849, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5193aac6-3dc1-4f52-b746-8be229552dce', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f3481ebb-a0ed-4742-9395-3104ae143d3c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175025_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175025_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2279704, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f3481ebb-a0ed-4742-9395-3104ae143d3c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e0c5a0e8-ac07-4af4-958f-1ef6564235da', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175026_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175026_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2301643, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e0c5a0e8-ac07-4af4-958f-1ef6564235da', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c286fc9e-6639-47c0-a94b-6fc96e0a56b6', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175027_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175027_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308209, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c286fc9e-6639-47c0-a94b-6fc96e0a56b6', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '95779a07-b0bd-48e4-ae8e-18326a5c5db0', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175028_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175028_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321987, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '95779a07-b0bd-48e4-ae8e-18326a5c5db0', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'ee646bf5-7d45-41f0-a2ba-27fd657e2704', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175029_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175029_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2303945, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'ee646bf5-7d45-41f0-a2ba-27fd657e2704', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5e096e00-1cc5-44de-bcd5-03152eda5d4e', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175030_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175030_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311200, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5e096e00-1cc5-44de-bcd5-03152eda5d4e', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '4df71c0a-65b5-4e0a-9ce0-c95aaba49563', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175031_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175031_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2310013, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '4df71c0a-65b5-4e0a-9ce0-c95aaba49563', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '74f2617c-29e1-47b2-b35a-039bf2f0413b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175032_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175032_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319984, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '74f2617c-29e1-47b2-b35a-039bf2f0413b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '2a796430-ecad-4348-854b-1a63d0ba1ca3', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175033_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175033_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2329261, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '2a796430-ecad-4348-854b-1a63d0ba1ca3', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'dff28d85-9f24-4537-9f90-aa5469922a46', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175034_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175034_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2337676, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'dff28d85-9f24-4537-9f90-aa5469922a46', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '94f04059-543b-4a37-9b3c-363d25dc2072', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175035_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175035_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316590, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '94f04059-543b-4a37-9b3c-363d25dc2072', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'cbb626d5-4282-48ab-9325-2e3cd91f5170', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175036_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175036_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335614, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'cbb626d5-4282-48ab-9325-2e3cd91f5170', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '7138b41c-a669-4b1f-8e18-8ca432cfeb05', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175037_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175037_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2345098, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '7138b41c-a669-4b1f-8e18-8ca432cfeb05', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'fd21ce29-eb72-4a99-8aa8-a451145ad85b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175038_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175038_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324488, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'fd21ce29-eb72-4a99-8aa8-a451145ad85b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5a457c38-da8c-4cd7-8a9f-99644b2136ac', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175039_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175039_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2340435, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5a457c38-da8c-4cd7-8a9f-99644b2136ac', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '94e5d322-f410-4336-a306-ed5bb1a3daa9', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175040_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175040_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2345567, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '94e5d322-f410-4336-a306-ed5bb1a3daa9', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '51b62b6f-d1d2-420f-96db-339dd163cf5d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175041_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175041_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2323456, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '51b62b6f-d1d2-420f-96db-339dd163cf5d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '624352c7-557e-4a73-b93f-2c1466802554', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175042_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175042_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324217, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '624352c7-557e-4a73-b93f-2c1466802554', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '98bfc9c3-e007-4ee3-b0de-8a9d0ee9656c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175043_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175043_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2325948, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '98bfc9c3-e007-4ee3-b0de-8a9d0ee9656c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f2089ab6-516e-4c8c-8361-4048eb64954b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175044_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175044_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2328720, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f2089ab6-516e-4c8c-8361-4048eb64954b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'eb84dbb5-3f83-40b9-8678-2d5398330c66', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175045_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175045_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2310448, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'eb84dbb5-3f83-40b9-8678-2d5398330c66', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b43e5074-2385-4544-96b8-3427d91dfc17', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175046_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175046_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336085, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b43e5074-2385-4544-96b8-3427d91dfc17', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e9b898b9-df20-40c3-b829-74d02b64b5d2', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175047_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175047_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2318604, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e9b898b9-df20-40c3-b829-74d02b64b5d2', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '80601497-2aa8-4320-b5b7-f42872726d54', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175048_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175048_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2307597, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '80601497-2aa8-4320-b5b7-f42872726d54', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '90811827-4bf1-4297-be56-863ae0c93a8d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175049_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175049_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327251, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '90811827-4bf1-4297-be56-863ae0c93a8d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '2ed9154b-b87b-4dc9-baf3-85dd991a987c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175050_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175050_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2322713, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '2ed9154b-b87b-4dc9-baf3-85dd991a987c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '469aa5a7-11b3-4c34-9602-80ccc9bc7715', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175051_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175051_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2323391, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '469aa5a7-11b3-4c34-9602-80ccc9bc7715', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b0d14d04-5476-4397-88b2-9a2ad2470ffd', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175052_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175052_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2340741, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b0d14d04-5476-4397-88b2-9a2ad2470ffd', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'dd277cb3-244f-4660-8e17-a6dfa65fef77', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175053_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175053_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335403, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'dd277cb3-244f-4660-8e17-a6dfa65fef77', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a799cf43-f7ee-4635-a900-344f8b9db826', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175054_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175054_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2309824, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a799cf43-f7ee-4635-a900-344f8b9db826', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '1acffb11-aff9-44c6-8c38-36b69219b100', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175055_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175055_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2326784, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '1acffb11-aff9-44c6-8c38-36b69219b100', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5e0c91ec-a0bb-4048-8cd3-ef1376751aa5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175056_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175056_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2302816, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5e0c91ec-a0bb-4048-8cd3-ef1376751aa5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '28979216-c164-4c0d-8530-fc2288319728', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175057_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175057_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2303562, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '28979216-c164-4c0d-8530-fc2288319728', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '1bf54a09-58a0-443f-a38e-a5b0e52e301a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175058_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175058_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2298845, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '1bf54a09-58a0-443f-a38e-a5b0e52e301a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '75e91ff3-93b2-4b13-848f-014f45d477d4', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175059_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175059_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2330511, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '75e91ff3-93b2-4b13-848f-014f45d477d4', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd5d0d7f0-4f6f-4c17-af82-ef379eeb00df', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175060_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175060_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2318240, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd5d0d7f0-4f6f-4c17-af82-ef379eeb00df', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '30255cbe-be67-4656-a1b5-280ec73968ba', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175061_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175061_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2326988, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '30255cbe-be67-4656-a1b5-280ec73968ba', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f51a63ab-89a0-44be-89f9-90c3a4a52519', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175062_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175062_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2295114, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f51a63ab-89a0-44be-89f9-90c3a4a52519', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e4eb18a2-4754-4c56-baea-fbe69446144a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175063_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175063_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2323582, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e4eb18a2-4754-4c56-baea-fbe69446144a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '9a87575b-2d08-46bc-a802-363de2777ec0', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175064_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175064_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336778, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '9a87575b-2d08-46bc-a802-363de2777ec0', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'cf8746cf-fe34-43eb-89e7-89aedb339c57', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175065_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175065_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2276124, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'cf8746cf-fe34-43eb-89e7-89aedb339c57', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'cf5e03a0-028a-427b-8f15-4ac6ff7bd0dc', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175066_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175066_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2320953, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'cf5e03a0-028a-427b-8f15-4ac6ff7bd0dc', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a5204748-cd4a-4454-aff3-5af24d4757ab', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175067_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175067_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2312384, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a5204748-cd4a-4454-aff3-5af24d4757ab', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c920d5da-6bd7-4479-af9c-3f82c308a807', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175068_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175068_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2303346, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c920d5da-6bd7-4479-af9c-3f82c308a807', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f32d820b-4f1f-49e3-b229-7b7c5b122843', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175069_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175069_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2317511, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f32d820b-4f1f-49e3-b229-7b7c5b122843', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '4d119d88-1c18-44d7-a628-7613be8a6230', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175070_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175070_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2332957, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '4d119d88-1c18-44d7-a628-7613be8a6230', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '67087d98-f9e8-4b87-9f6c-0db9a1206c57', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175071_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175071_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2299872, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '67087d98-f9e8-4b87-9f6c-0db9a1206c57', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e1142586-7f3e-4d33-a1a0-903086f0a7ff', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175072_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175072_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2323373, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e1142586-7f3e-4d33-a1a0-903086f0a7ff', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f9ab96db-b002-4182-9129-54e36a83235a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175073_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175073_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2318664, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f9ab96db-b002-4182-9129-54e36a83235a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b50edd1f-d1a9-4fd1-8c4f-71dec98329b0', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175074_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175074_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319602, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b50edd1f-d1a9-4fd1-8c4f-71dec98329b0', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c4ef977f-5e1f-4644-a7ff-7053bb152076', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175075_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175075_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2332518, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c4ef977f-5e1f-4644-a7ff-7053bb152076', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'ef3d4506-2aac-4839-9d2b-0384e87b56b0', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175076_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175076_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336269, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'ef3d4506-2aac-4839-9d2b-0384e87b56b0', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '381c527f-6fcf-4a47-bc99-7dc04a21835f', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175077_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175077_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2306262, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '381c527f-6fcf-4a47-bc99-7dc04a21835f', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f931fab8-ffc0-4985-a6e0-a9f7460ef682', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175078_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175078_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2314699, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f931fab8-ffc0-4985-a6e0-a9f7460ef682', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'df28a169-d8aa-4be4-a767-765b2d3fef3c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175079_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175079_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2306488, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'df28a169-d8aa-4be4-a767-765b2d3fef3c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'df522537-340b-4e8c-a2e0-9d48ad1a1b6a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175080_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175080_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336940, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'df522537-340b-4e8c-a2e0-9d48ad1a1b6a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'bf3bdf87-cdad-4759-981b-def2b183d8ae', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175081_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175081_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2333501, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'bf3bdf87-cdad-4759-981b-def2b183d8ae', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '8351d691-9f5a-4837-8782-051dfbfeff82', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175082_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175082_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316655, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '8351d691-9f5a-4837-8782-051dfbfeff82', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '7c00d780-cd91-473f-8f75-9bc2cd261436', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175083_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175083_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2339003, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '7c00d780-cd91-473f-8f75-9bc2cd261436', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '72c4e07e-3c9c-4968-adfe-fe537934cd4e', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175084_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175084_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324476, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '72c4e07e-3c9c-4968-adfe-fe537934cd4e', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '301fcb1e-1acc-4b8d-804d-20576f2762dc', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175085_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175085_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2317848, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '301fcb1e-1acc-4b8d-804d-20576f2762dc', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a7f624bf-d7f4-4343-9cc3-a0f8c0fd8b16', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175086_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175086_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2339327, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a7f624bf-d7f4-4343-9cc3-a0f8c0fd8b16', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b694a1d9-9353-435c-98dc-64d5581ef93e', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175087_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175087_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2301884, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b694a1d9-9353-435c-98dc-64d5581ef93e', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd76ab279-80bd-48a4-ad93-124c75cf4ce4', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175088_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175088_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324503, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd76ab279-80bd-48a4-ad93-124c75cf4ce4', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5d7b3072-8f25-4c78-88e6-b34fe1f65b7c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175089_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175089_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324586, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5d7b3072-8f25-4c78-88e6-b34fe1f65b7c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '2bcdcb9a-490e-4989-885e-9c995574b79c', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175090_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175090_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2334050, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '2bcdcb9a-490e-4989-885e-9c995574b79c', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '44c68d3b-c5ce-4328-8b82-60d6f16b47ca', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175091_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175091_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2351115, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '44c68d3b-c5ce-4328-8b82-60d6f16b47ca', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '4733ee8d-1cf5-4c84-b2a4-811417b86657', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175092_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175092_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2358158, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '4733ee8d-1cf5-4c84-b2a4-811417b86657', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'adc0189a-b790-4401-a17a-f734564ba653', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175093_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175093_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2361899, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'adc0189a-b790-4401-a17a-f734564ba653', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'fd452928-7fae-45e0-98ef-89353aa394c6', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175094_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175094_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335920, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'fd452928-7fae-45e0-98ef-89353aa394c6', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd8e278fb-3163-431c-b408-c1b6ce05a531', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175095_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175095_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2336936, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd8e278fb-3163-431c-b408-c1b6ce05a531', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3ba49d05-2407-4d99-9b40-f99c4feadf08', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175096_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175096_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321419, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3ba49d05-2407-4d99-9b40-f99c4feadf08', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b7af31a2-1f3c-4071-9b05-8a19f15e141d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175097_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175097_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2338066, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b7af31a2-1f3c-4071-9b05-8a19f15e141d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '13eba74a-bc03-4755-8250-e78c487a3272', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175098_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175098_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324973, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '13eba74a-bc03-4755-8250-e78c487a3272', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '041a757b-469a-4580-bfdf-98a9c2169495', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175099_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175099_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2330775, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '041a757b-469a-4580-bfdf-98a9c2169495', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '41bc0750-e0d8-4bfe-a7ae-c6c498522880', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175100_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175100_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2294813, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '41bc0750-e0d8-4bfe-a7ae-c6c498522880', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '80567ae2-c135-49e5-a0be-04209cc7dd16', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175101_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175101_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2306041, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '80567ae2-c135-49e5-a0be-04209cc7dd16', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd1a20c57-dee8-488a-a66a-82bab6a2adcf', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175102_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175102_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335753, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd1a20c57-dee8-488a-a66a-82bab6a2adcf', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5c9c2f63-893a-45ff-a300-e912f50cb5b5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175103_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175103_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2288891, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5c9c2f63-893a-45ff-a300-e912f50cb5b5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'adb85665-b3e6-4cf6-827e-ba35ab631099', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175104_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175104_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2339096, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'adb85665-b3e6-4cf6-827e-ba35ab631099', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '8eeb94bc-3795-43f8-90fd-d7b87ee7dd17', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175105_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175105_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311545, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '8eeb94bc-3795-43f8-90fd-d7b87ee7dd17', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a2a391fc-941a-40a5-b1b8-74af18805738', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175106_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175106_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327866, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a2a391fc-941a-40a5-b1b8-74af18805738', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'def2c8be-3fb7-436a-8a7b-dbf44b602d6a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175107_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175107_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327539, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'def2c8be-3fb7-436a-8a7b-dbf44b602d6a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c8aba35a-2928-4df9-a68a-8130b7643f80', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175108_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175108_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2331198, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c8aba35a-2928-4df9-a68a-8130b7643f80', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'b5d7cd34-aa7b-427f-b6a4-dde5137a1fc8', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175109_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175109_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2330202, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'b5d7cd34-aa7b-427f-b6a4-dde5137a1fc8', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3326d026-9e64-4b21-b828-3f2a0c9a358a', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175110_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175110_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2342534, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3326d026-9e64-4b21-b828-3f2a0c9a358a', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5cdaa483-0ad3-4967-b6df-abb8ccf8cfc9', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175111_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175111_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2326416, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5cdaa483-0ad3-4967-b6df-abb8ccf8cfc9', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f32e5180-5269-4fd7-96f9-45463c7d3e2b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175112_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175112_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2325235, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f32e5180-5269-4fd7-96f9-45463c7d3e2b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f1464595-0450-4f58-a9dd-63731c0f9d7d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175113_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175113_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2327558, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f1464595-0450-4f58-a9dd-63731c0f9d7d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '151250dc-1e8d-4305-8543-74786f838891', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175114_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175114_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321772, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '151250dc-1e8d-4305-8543-74786f838891', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'e98ea52b-bb8b-46cc-a11c-2a3b402a97d1', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175115_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175115_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2333150, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'e98ea52b-bb8b-46cc-a11c-2a3b402a97d1', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '71a0c678-c78e-401d-ab7a-355862b9cbeb', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175116_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175116_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321413, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '71a0c678-c78e-401d-ab7a-355862b9cbeb', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '1567918e-c046-430e-baa3-2dec065d1564', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175117_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175117_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2317498, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '1567918e-c046-430e-baa3-2dec065d1564', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '0cba70a8-2779-4813-9118-49d93985b515', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175118_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175118_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2331466, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '0cba70a8-2779-4813-9118-49d93985b515', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '7a1d084f-0a57-427a-9bec-e9e4684076bd', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175119_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175119_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2329142, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '7a1d084f-0a57-427a-9bec-e9e4684076bd', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '6efb685c-453b-498c-babf-ecf2292b3dc5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175120_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175120_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2317426, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '6efb685c-453b-498c-babf-ecf2292b3dc5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'bf15f6e3-7e11-4b69-bfa7-a8fab8118af5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175121_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175121_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2309208, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'bf15f6e3-7e11-4b69-bfa7-a8fab8118af5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '6f18487a-9e18-42d4-a11e-16fb9ea8da83', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175122_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175122_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2332175, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '6f18487a-9e18-42d4-a11e-16fb9ea8da83', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3e7e0bd0-b382-4295-a430-42ca0b02c446', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175123_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175123_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2334726, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3e7e0bd0-b382-4295-a430-42ca0b02c446', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '92cdf83f-0540-4bd7-9ce7-905c01b13671', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175124_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175124_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2333497, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '92cdf83f-0540-4bd7-9ce7-905c01b13671', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '40c76bb0-20a0-4383-ad71-0e3766fd8a50', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175125_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175125_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2328215, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '40c76bb0-20a0-4383-ad71-0e3766fd8a50', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '7998c6ad-072a-41a3-9446-833e908148a2', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175126_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175126_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2329320, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '7998c6ad-072a-41a3-9446-833e908148a2', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a0926a27-d12e-4ae2-84ad-cb16098387f8', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175127_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175127_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321926, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a0926a27-d12e-4ae2-84ad-cb16098387f8', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '04226fdb-4977-47b1-8535-c730673ec6d3', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175128_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175128_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2316211, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '04226fdb-4977-47b1-8535-c730673ec6d3', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a0669dcd-9e23-4282-b38a-1f8c039238ae', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175129_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175129_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2335923, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a0669dcd-9e23-4282-b38a-1f8c039238ae', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '3f6d5a29-cc14-4321-9911-5850acb98e66', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175130_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175130_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2334421, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '3f6d5a29-cc14-4321-9911-5850acb98e66', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '96e09dd2-cff8-4131-9cd6-0e4592fa2cca', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175131_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175131_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2320371, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '96e09dd2-cff8-4131-9cd6-0e4592fa2cca', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'add46202-177c-4923-b6d2-99e7712ca967', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175132_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175132_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2307033, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'add46202-177c-4923-b6d2-99e7712ca967', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a0786bf8-c1b3-4e57-a16c-7deab1d7dc7e', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175133_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175133_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2332095, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a0786bf8-c1b3-4e57-a16c-7deab1d7dc7e', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '5481a782-e529-41e8-b276-bba7c6566e5d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175134_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175134_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2311295, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '5481a782-e529-41e8-b276-bba7c6566e5d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '338a755e-b16c-4da0-90f6-0ef576bc42e7', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175135_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175135_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308159, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '338a755e-b16c-4da0-90f6-0ef576bc42e7', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '943c2f51-ed74-4b37-b3d1-1f503346b870', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175136_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175136_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2321021, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '943c2f51-ed74-4b37-b3d1-1f503346b870', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '82b40773-22cb-464d-a7f8-b0b36d3fa6bf', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175137_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175137_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2292690, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '82b40773-22cb-464d-a7f8-b0b36d3fa6bf', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c4c7b9dd-ac0f-49ee-9c5b-d23bccb44d61', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175138_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175138_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2300042, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c4c7b9dd-ac0f-49ee-9c5b-d23bccb44d61', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'ba57978c-908f-428a-8441-943386a47e2d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175139_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175139_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2314414, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'ba57978c-908f-428a-8441-943386a47e2d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '268a2194-222a-4c6d-bf2c-37c6cd73e4e2', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175140_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175140_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2322813, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '268a2194-222a-4c6d-bf2c-37c6cd73e4e2', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '931902d1-575d-42ac-83d2-1eb9edcedafa', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175141_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175141_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319322, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '931902d1-575d-42ac-83d2-1eb9edcedafa', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f061c965-f162-4cee-b697-42b2d1b6c27d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175142_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175142_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2324180, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f061c965-f162-4cee-b697-42b2d1b6c27d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'a531149f-d364-40eb-b2f7-f66963fbb1e6', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175143_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175143_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2300990, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'a531149f-d364-40eb-b2f7-f66963fbb1e6', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd645935e-1289-4750-8b91-47b0fc01790d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175144_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175144_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2350120, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd645935e-1289-4750-8b91-47b0fc01790d', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd18cabe7-71b3-42d3-8e20-0f2d2958ccd6', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175145_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175145_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2297856, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd18cabe7-71b3-42d3-8e20-0f2d2958ccd6', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'f1ec8c5e-86a4-4909-9c54-f6921402b37b', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175146_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175146_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2319915, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'f1ec8c5e-86a4-4909-9c54-f6921402b37b', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '078be77d-c1b8-4f1b-96e4-c856448e2ce5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175147_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175147_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2290613, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '078be77d-c1b8-4f1b-96e4-c856448e2ce5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'c424c0d6-1534-4ad9-8b35-fe50c0051d89', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175148_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175148_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2325943, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'c424c0d6-1534-4ad9-8b35-fe50c0051d89', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '09d19ba6-8ba0-4a91-b633-c5d75e6cc2c5', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175149_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175149_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2295465, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '09d19ba6-8ba0-4a91-b633-c5d75e6cc2c5', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '6b28eab8-a164-4afd-b5d0-a1e608a47d26', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175150_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175150_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2343776, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '6b28eab8-a164-4afd-b5d0-a1e608a47d26', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            'd7dc9882-31ba-48b2-958a-87bcc7e4bb8e', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175151_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175151_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2312313, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', 'd7dc9882-31ba-48b2-958a-87bcc7e4bb8e', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '79e7ad11-de25-4f14-9ac7-ca387659fe95', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175152_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175152_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2308679, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '79e7ad11-de25-4f14-9ac7-ca387659fe95', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '7e66a21a-dc99-4c82-851a-478857296fbb', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175153_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175153_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2286348, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '7e66a21a-dc99-4c82-851a-478857296fbb', 'RAW_IMAGE', now());
    

        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES (
            '158cdcf8-125f-450f-9047-4df672b8660d', 'cf637409-fb85-4dfd-8fd3-ae3ea799d635', '7d6e85c0-17f6-43fd-9a93-0d99b9a8de1d', 'minio',
            'tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175154_1791879976229.JPG', 'minio://agridrone-media/tenants/cf637409-fb85-4dfd-8fd3-ae3ea799d635/media/8d2ba93d-1421-448d-be83-a1034e8c4aab/G0175154_1791879976229.JPG',
            'IMAGE', 'image/jpeg', 2322502, '{}'::jsonb, 'ACTIVE', now(), now()
        );
        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES ('8d2ba93d-1421-448d-be83-a1034e8c4aab', '158cdcf8-125f-450f-9047-4df672b8660d', 'RAW_IMAGE', now());
    
COMMIT;
