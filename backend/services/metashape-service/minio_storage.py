import hashlib
import os
from datetime import timedelta
from typing import Optional
from minio import Minio
from minio.error import S3Error
from config import settings

class MinioStorage:
    def __init__(self):
        self.client = Minio(
            endpoint=settings.minio_endpoint,
            access_key=settings.minio_access_key,
            secret_key=settings.minio_secret_key,
            secure=settings.minio_secure
        )
        self.bucket = settings.minio_bucket
        self._bucket_checked = False

    def _ensure_bucket(self):
        if self._bucket_checked:
            return
        try:
            if not self.client.bucket_exists(self.bucket):
                self.client.make_bucket(self.bucket)
            self._bucket_checked = True
        except Exception:
            pass

    def download_file(self, object_name: str, local_path: str):
        # Strip bucket prefix if passed e.g. minio://bucket/key or s3://bucket/key
        clean_key = object_name
        if "://" in clean_key:
            parts = clean_key.split("://", 1)[1]
            if "/" in parts:
                clean_key = parts.split("/", 1)[1]
        
        os.makedirs(os.path.dirname(os.path.abspath(local_path)), exist_ok=True)
        self.client.fget_object(self.bucket, clean_key, local_path)

    def upload_file(self, local_path: str, object_name: str, content_type: str = "application/octet-stream") -> tuple[int, str]:
        """Uploads a file to MinIO and returns (size_bytes, sha256_checksum)."""
        file_size = os.path.getsize(local_path)
        sha256 = hashlib.sha256()
        with open(local_path, "rb") as f:
            for chunk in iter(lambda: f.read(1024 * 1024), b""):
                sha256.update(chunk)
        checksum = sha256.hexdigest()

        self.client.fput_object(
            bucket_name=self.bucket,
            object_name=object_name,
            file_path=local_path,
            content_type=content_type
        )
        return file_size, checksum

    def get_presigned_url(self, object_name: str, expires_hours: int = 2) -> Optional[str]:
        try:
            return self.client.presigned_get_object(
                bucket_name=self.bucket,
                object_name=object_name,
                expires=timedelta(hours=expires_hours)
            )
        except Exception:
            return None

    def delete_file(self, object_name: str):
        clean_key = object_name
        if "://" in clean_key:
            parts = clean_key.split("://", 1)[1]
            if "/" in parts:
                clean_key = parts.split("/", 1)[1]
        try:
            self.client.remove_object(self.bucket, clean_key)
        except Exception:
            pass

    def delete_files(self, object_names: list[str]):
        for obj in object_names:
            self.delete_file(obj)

minio_storage = MinioStorage()
