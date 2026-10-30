from minio import Minio
import uuid

client = Minio('localhost:9000', access_key='agridrone_admin', secret_key='agridrone-dragon', secure=False)
bucket = 'agridrone-media'

ortho_tif = r'd:\capstone\AgriDrone-BE\scratch\test_orthophoto.tif'
map_prev = r'd:\capstone\AgriDrone-BE\scratch\map_preview.jpg'

missions = [
    {
        'tenant_id': 'ecfd71e0-19df-4e7a-a493-ba6208495761',
        'farm_id': '74759685-c439-47df-8c07-e7e1c9712d0a',
        'mission_id': '7e5b1b77-4746-45dd-84e5-d5252782c16b'
    },
    {
        'tenant_id': '42d570f5-1424-41ad-82e4-5819d14a0f93',
        'farm_id': 'ea20366f-41ff-40e1-ba96-6e0dabf5b83c',
        'mission_id': '3366b87f-c642-4322-a51f-99f96e390a0e'
    }
]

sql_statements = []

for m in missions:
    tid = m['tenant_id']
    fid = m['farm_id']
    mid = m['mission_id']
    
    tif_key = f'tenants/{tid}/media/{mid}/orthophoto.tif'
    client.fput_object(bucket, tif_key, ortho_tif)
    
    jpg_key = f'tenants/{tid}/media/{mid}/map_preview.jpg'
    client.fput_object(bucket, jpg_key, map_prev)
    
    tif_id = str(uuid.uuid4())
    jpg_id = str(uuid.uuid4())
    
    sql = f"""
        INSERT INTO media.media_assets (
            id, tenant_id, farm_id, provider, storage_key, url, media_type, mime_type, file_size_bytes, metadata, storage_status, created_at, updated_at
        ) VALUES 
        ('{tif_id}', '{tid}', '{fid}', 'minio', '{tif_key}', 'minio://{bucket}/{tif_key}', 'IMAGE', 'image/tiff', 2126042, '{{}}'::jsonb, 'ACTIVE', now(), now()),
        ('{jpg_id}', '{tid}', '{fid}', 'minio', '{jpg_key}', 'minio://{bucket}/{jpg_key}', 'IMAGE', 'image/jpeg', 251737, '{{\"bounds\": {{\"min_lat\": 10.4612343, \"max_lat\": 10.4615936, \"min_lon\": 106.4938255, \"max_lon\": 106.4943916}}, \"corners\": {{\"north_west\": [10.4615936, 106.4938255], \"north_east\": [10.4615936, 106.4943916], \"south_east\": [10.4612343, 106.4943916], \"south_west\": [10.4612343, 106.4938255]}}}}'::jsonb, 'ACTIVE', now(), now());

        INSERT INTO mission.mission_media (mission_id, media_id, media_role, created_at)
        VALUES 
        ('{mid}', '{tif_id}', 'PROCESSED_IMAGE', now()),
        ('{mid}', '{jpg_id}', 'PROCESSED_IMAGE', now());
    """
    sql_statements.append(sql)

with open(r'd:\capstone\AgriDrone-BE\scratch\restore_other_missions.sql', 'w', encoding='utf-8') as f:
    f.write('BEGIN;\n' + '\n'.join(sql_statements) + '\nCOMMIT;\n')

print('Restored MinIO objects for other missions.')

