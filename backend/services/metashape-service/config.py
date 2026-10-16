import os
from pydantic import BaseModel

class Settings(BaseModel):
    app_name: str = "AgriDrone Metashape Service"
    host: str = os.getenv("METASHAPE_SERVICE_HOST", "0.0.0.0")
    port: int = int(os.getenv("METASHAPE_SERVICE_PORT", "8000"))
    
    # MinIO / S3 Configuration
    minio_endpoint: str = os.getenv("MINIO_ENDPOINT", "localhost:9000")
    minio_access_key: str = os.getenv("MINIO_ROOT_USER", "agridrone_admin")
    minio_secret_key: str = os.getenv("MINIO_ROOT_PASSWORD", "agridrone-dragon")
    minio_bucket: str = os.getenv("MINIO_BUCKET", "agridrone-media")
    minio_secure: bool = os.getenv("MINIO_SECURE", "false").lower() in ("true", "1", "yes")

    # Storage temp directory (located on D: drive in project directory with plenty of space)
    temp_dir: str = os.getenv(
        "TEMP_DIR",
        os.path.abspath(os.path.join(os.path.dirname(__file__), "temp"))
    )

settings = Settings()
