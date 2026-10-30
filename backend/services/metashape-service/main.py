import asyncio
import json
import logging
import os
import shutil
import time
import uuid
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager
from typing import Any, Dict, List, Optional

from fastapi import BackgroundTasks, FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, HTMLResponse

# Ultralytics YOLO inference for orchard tree detection
try:
    from ultralytics import YOLO
except Exception:
    YOLO = None

try:
    import torch
    # Tối ưu tải CPU để máy không bao giờ bị đơ/lag
    if not torch.cuda.is_available():
        cpu_threads = max(1, (os.cpu_count() or 4) // 2)
        torch.set_num_threads(cpu_threads)
        print(f"[METASHAPE-INIT] PyTorch running on CPU (clamped to {cpu_threads} threads).")
    else:
        print(f"[METASHAPE-INIT] PyTorch CUDA enabled! Device: {torch.cuda.get_device_name(0)}")
except Exception as e:
    torch = None
    print(f"[METASHAPE-INIT] PyTorch setup note: {e}")


from config import settings
from map_picker import get_map_picker_html
from metashape_workflow import (
    Metashape,
    MetashapeProcessingError,
    extract_geotiff_metadata,
    run_metashape_orthophoto_pipeline,
)
from odm_workflow import (
    ODMProcessingError,
    run_odm_orthophoto_pipeline,
)
from minio_storage import minio_storage
from models import (
    MapBounds,
    MapCorners,
    MetashapeTaskStatusResponse,
    ProcessMetashapeRequest,
    ProcessMetashapeResponse,
    TileItem,
    TileSliceRequest,
    TileSliceResponse,
    TreeItem,
    YoloDetectMissionRequest,
    YoloDetectMissionResponse,
)
from orchard_postprocessor import (
    assign_grid_and_detect_missing,
    deduplicate_trees,
    export_trees_to_csv,
    is_inside_polygon,
    pixel_to_gps,
)
from tiler import slice_geotiff

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] [%(name)s] %(message)s"
)
logger = logging.getLogger("metashape-service")

executor = ThreadPoolExecutor(max_workers=2)

tasks_db: Dict[str, MetashapeTaskStatusResponse] = {}


@asynccontextmanager
async def lifespan(app: FastAPI):
    os.makedirs(settings.temp_dir, exist_ok=True)
    logger.info("Metashape Photogrammetry Service initialized.")
    if Metashape is None:
        logger.warning(
            "Metashape Python module is NOT installed. Processing calls will fail until the Metashape Pro wheel is installed."
        )
    else:
        try:
            logger.info(f"Agisoft Metashape version: {Metashape.__version__}")
        except Exception:
            logger.info("Agisoft Metashape module loaded.")
    yield
    executor.shutdown(wait=False)


app_description = """
# 🚀 AgriDrone Metashape & Tiling Service

### 🗺️ Giao Diện Bản Đồ Trực Quan (Map Picker UI)
👉 **[CLICK VÀO ĐÂY ĐỂ MỞ BẢN ĐỒ CHỌN ĐIỂM GPS TRỰC TIẾP TRÊN ẢNH DRONE](/map-picker)**

**Tính năng nổi bật:**
- 🖼️ **Hiển thị ảnh bản đồ Drone preview (2.5MB)** lồng khớp 100% tọa độ thực tế trên Google Satellite.
- 🔍 **Zoom in / Zoom out & Pan tự do**: Phóng to từng tán cây, bờ ruộng.
- 📍 **Click chuột chọn ranh giới đất (Polygon)**: Tự động tính tọa độ GPS (WGS84 Lat/Lon) chính xác theo GeoTIFF gốc mà không cần gõ tay.
- 📐 **Tự động đo diện tích (ha)** và sinh cấu trúc chuẩn **GeoJSON**.
- ✂️ **Nút 'Bắt đầu Cắt Mảnh Bản Đồ'**: Gửi trực tiếp GeoJSON sang API Windowed Reading `/api/v1/tiling/slice` và hiển thị 9 ảnh chip 640x640 đầu tiên ngay trên màn hình!
"""

app = FastAPI(
    title=settings.app_name,
    version="1.0.0",
    description=app_description,
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
def health():
    return {
        "status": "healthy",
        "metashape_available": Metashape is not None,
        "minio_endpoint": settings.minio_endpoint,
        "minio_bucket": settings.minio_bucket
    }


def get_available_missions() -> List[Dict[str, Any]]:
    """Liệt kê tất cả các chuyến bay đã hoàn thành và có ảnh trực giao trong MinIO hoặc tasks_db."""
    missions = {}
    try:
        objects = minio_storage.client.list_objects(minio_storage.bucket, prefix="tenants/", recursive=True)
        for obj in objects:
            parts = obj.object_name.split("/")
            if len(parts) >= 5 and parts[0] == "tenants" and parts[2] == "media":
                tenant_id = parts[1]
                mission_id = parts[3]
                filename = parts[4]
                if mission_id not in missions:
                    missions[mission_id] = {
                        "mission_id": mission_id,
                        "tenant_id": tenant_id,
                        "has_preview": False,
                        "has_orthophoto": False,
                        "has_metadata": False,
                        "last_modified": str(obj.last_modified) if hasattr(obj, "last_modified") and obj.last_modified else ""
                    }
                if filename == "map_preview.jpg":
                    missions[mission_id]["has_preview"] = True
                elif filename == "orthophoto.tif":
                    missions[mission_id]["has_orthophoto"] = True
                elif filename == "metadata.json":
                    missions[mission_id]["has_metadata"] = True
                if hasattr(obj, "last_modified") and obj.last_modified:
                    curr_mod = str(obj.last_modified)
                    if curr_mod > missions[mission_id].get("last_modified", ""):
                        missions[mission_id]["last_modified"] = curr_mod
    except Exception as e:
        logger.warning(f"Failed to list missions from MinIO: {e}")

    for t_id, task in tasks_db.items():
        if task.mission_id and task.mission_id not in missions and task.status == "COMPLETED":
            missions[task.mission_id] = {
                "mission_id": task.mission_id,
                "tenant_id": "unknown",
                "has_preview": bool(task.preview_url or task.jpg_storage_key),
                "has_orthophoto": bool(task.tif_url or task.tif_storage_key),
                "has_metadata": bool(task.bounds),
                "last_modified": ""
            }

    results = [m for m in missions.values() if m["has_preview"] or m["has_orthophoto"]]
    results.sort(key=lambda x: x.get("last_modified", ""), reverse=True)
    return results


def resolve_mission_metadata(
    mission_id: Optional[str] = None,
    tenant_id: Optional[str] = None,
    min_lat: Optional[float] = None,
    max_lat: Optional[float] = None,
    min_lon: Optional[float] = None,
    max_lon: Optional[float] = None,
) -> Dict[str, Any]:
    available = get_available_missions()

    # 1. Tự động lấy mission mới nhất từ MinIO nếu chưa truyền mission_id
    if not mission_id:
        if available:
            mission_id = available[0]["mission_id"]
            if not tenant_id:
                tenant_id = available[0]["tenant_id"]
        else:
            mission_id = "00000000-0000-0000-0000-000000000000"
            tenant_id = tenant_id or "00000000-0000-0000-0000-000000000000"

    # 2. Tìm tenant_id tương ứng
    if not tenant_id:
        for m in available:
            if m["mission_id"] == mission_id and m.get("tenant_id"):
                tenant_id = m["tenant_id"]
                break
        if not tenant_id:
            try:
                for obj in minio_storage.client.list_objects(minio_storage.bucket, prefix="tenants/", recursive=True):
                    if f"/media/{mission_id}/" in obj.object_name:
                        tenant_id = obj.object_name.split("/")[1]
                        break
            except Exception:
                pass
        tenant_id = tenant_id or "default"

    # 3. Nếu caller đã truyền đủ 4 tọa độ góc
    if min_lat is not None and max_lat is not None and min_lon is not None and max_lon is not None:
        return {
            "mission_id": mission_id,
            "tenant_id": tenant_id,
            "bounds": {
                "min_lat": min_lat,
                "max_lat": max_lat,
                "min_lon": min_lon,
                "max_lon": max_lon,
            },
            "available_missions": available
        }

    # 4. Đọc metadata.json từ MinIO
    meta_key = f"tenants/{tenant_id}/media/{mission_id}/metadata.json"
    try:
        tmp_meta = os.path.join(settings.temp_dir, f"meta_{mission_id}.json")
        minio_storage.download_file(meta_key, tmp_meta)
        with open(tmp_meta, "r", encoding="utf-8") as f:
            data = json.load(f)
        try:
            os.remove(tmp_meta)
        except Exception:
            pass
        if "bounds" in data:
            return {
                "mission_id": mission_id,
                "tenant_id": tenant_id,
                "bounds": data["bounds"],
                "corners": data.get("corners"),
                "available_missions": available
            }
    except Exception:
        pass

    # 5. Kiểm tra trong in-memory tasks_db
    for task in tasks_db.values():
        if task.mission_id == mission_id and task.bounds:
            return {
                "mission_id": mission_id,
                "tenant_id": tenant_id,
                "bounds": {
                    "min_lat": task.bounds.min_lat,
                    "max_lat": task.bounds.max_lat,
                    "min_lon": task.bounds.min_lon,
                    "max_lon": task.bounds.max_lon,
                },
                "corners": task.corners.dict() if task.corners else None,
                "available_missions": available
            }

    # 6. Đọc trực tiếp từ file orthophoto.tif trong MinIO nếu chưa có metadata.json
    tif_key = f"tenants/{tenant_id}/media/{mission_id}/orthophoto.tif"
    try:
        tmp_tif = os.path.join(settings.temp_dir, f"temp_meta_{mission_id}.tif")
        minio_storage.download_file(tif_key, tmp_tif)
        b_dict, c_dict, w, h = extract_geotiff_metadata(tmp_tif)
        try:
            os.remove(tmp_tif)
        except Exception:
            pass

        # Lưu lại metadata.json vào MinIO để lần sau truy xuất ngay lập tức
        try:
            meta_save = {
                "mission_id": mission_id,
                "tenant_id": tenant_id,
                "bounds": b_dict,
                "corners": c_dict,
                "width": w,
                "height": h,
            }
            tmp_save = os.path.join(settings.temp_dir, f"meta_save_{mission_id}.json")
            with open(tmp_save, "w", encoding="utf-8") as f:
                json.dump(meta_save, f)
            minio_storage.upload_file(tmp_save, meta_key, "application/json")
            os.remove(tmp_save)
        except Exception:
            pass

        return {
            "mission_id": mission_id,
            "tenant_id": tenant_id,
            "bounds": b_dict,
            "corners": c_dict,
            "available_missions": available
        }
    except Exception as e:
        logger.warning(f"Could not extract GeoTIFF bounds for {mission_id}: {e}")

    # Fallback mặc định
    return {
        "mission_id": mission_id,
        "tenant_id": tenant_id,
        "bounds": {
            "min_lat": 10.4613764,
            "max_lat": 10.4619687,
            "min_lon": 106.4938479,
            "max_lon": 106.4941861,
        },
        "available_missions": available
    }


@app.get("/api/v1/missions", tags=["Interactive Map Tool"])
def list_available_missions():
    """
    Liệt kê danh sách tất cả các chuyến bay (Mission) đã có bản đồ ghép sẵn trong hệ thống MinIO.
    """
    return get_available_missions()


@app.get("/map-picker", response_class=HTMLResponse, tags=["Interactive Map Tool"])
def map_picker_ui(
    mission_id: Optional[str] = None,
    tenant_id: Optional[str] = None,
    farm_id: Optional[str] = "",
    min_lat: Optional[float] = None,
    max_lat: Optional[float] = None,
    min_lon: Optional[float] = None,
    max_lon: Optional[float] = None,
):
    """
    Giao diện Web tương tác trực quan:
    - Tự động nạp chuyến bay (Mission) mới nhất từ MinIO nếu không truyền mission_id.
    - Hiển thị ảnh Drone JPG nén tối ưu (2.5MB) lồng lên Google Satellite với đúng tọa độ GeoTIFF.
    - Cho phép chọn/đổi nhanh giữa các Mission ngay trên thanh công cụ.
    - Zoom vào và click chuột chọn các đỉnh ranh giới thửa đất (Polygon).
    - Cắt ảnh Dual-scale (640 + 1280) và chạy YOLO nhận diện cây trực tiếp.
    """
    resolved = resolve_mission_metadata(
        mission_id=mission_id,
        tenant_id=tenant_id,
        min_lat=min_lat,
        max_lat=max_lat,
        min_lon=min_lon,
        max_lon=max_lon,
    )
    b = resolved["bounds"]
    html_content = get_map_picker_html(
        mission_id=resolved["mission_id"],
        tenant_id=resolved["tenant_id"],
        farm_id=farm_id or "",
        min_lat=b["min_lat"],
        max_lat=b["max_lat"],
        min_lon=b["min_lon"],
        max_lon=b["max_lon"],
        available_missions=resolved.get("available_missions"),
    )
    return HTMLResponse(content=html_content)


def execute_metashape_job(task_id: str, request: ProcessMetashapeRequest):
    start_time = time.time()
    work_dir = os.path.join(settings.temp_dir, task_id)
    images_dir = os.path.join(work_dir, "images")
    output_dir = os.path.join(work_dir, "output")
    os.makedirs(images_dir, exist_ok=True)
    os.makedirs(output_dir, exist_ok=True)

    task = tasks_db[task_id]
    task.status = "RUNNING"
    task.progress = 5
    task.stage = "Downloading images from MinIO"

    try:
        # 1. Download images from MinIO
        local_images = []
        for idx, img in enumerate(request.images):
            ext = os.path.splitext(img.storage_key)[1] or ".jpg"
            local_img_path = os.path.join(images_dir, f"img_{idx:04d}{ext}")
            minio_storage.download_file(img.storage_key, local_img_path)
            local_images.append(local_img_path)

        if not local_images:
            raise MetashapeProcessingError("No images downloaded from MinIO.")

        output_tif = os.path.join(output_dir, "orthophoto.tif")
        output_jpg = os.path.join(output_dir, "map_preview.jpg")

        def on_progress(pct: int, stg: str):
            task.progress = pct
            task.stage = stg
            task.processing_time_ms = int((time.time() - start_time) * 1000)

        # 2. Run Metashape Workflow
        bounds_dict, corners_dict, orig_w, orig_h = run_metashape_orthophoto_pipeline(
            photo_paths=local_images,
            output_tif_path=output_tif,
            output_jpg_path=output_jpg,
            camera_type=request.camera_type,
            align_accuracy=request.align_accuracy,
            generic_preselection=request.generic_preselection,
            progress_callback=on_progress
        )

        task.stage = "Uploading results to MinIO"
        task.progress = 95

        # 3. Upload GeoTIFF to MinIO
        tif_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/orthophoto.tif"
        tif_size, tif_hash = minio_storage.upload_file(output_tif, tif_key, "image/tiff")

        # 4. Upload Map Preview JPEG to MinIO
        jpg_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/map_preview.jpg"
        jpg_size, jpg_hash = minio_storage.upload_file(output_jpg, jpg_key, "image/jpeg")

        # 4b. Upload metadata.json to MinIO for fast map lookup
        try:
            meta_payload = {
                "mission_id": request.mission_id,
                "tenant_id": request.tenant_id,
                "farm_id": request.farm_id,
                "bounds": bounds_dict,
                "corners": corners_dict,
                "original_width": orig_w,
                "original_height": orig_h,
                "created_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            }
            meta_path = os.path.join(output_dir, "metadata.json")
            with open(meta_path, "w", encoding="utf-8") as f:
                json.dump(meta_payload, f, indent=2)
            meta_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/metadata.json"
            minio_storage.upload_file(meta_path, meta_key, "application/json")
        except Exception as e:
            logger.warning(f"Failed to upload metadata.json: {e}")

        # 5. Generate presigned URLs
        tif_url = minio_storage.get_presigned_url(tif_key)
        preview_url = minio_storage.get_presigned_url(jpg_key)

        task.status = "COMPLETED"
        task.progress = 100
        task.stage = "Completed"
        task.processing_time_ms = int((time.time() - start_time) * 1000)
        task.tif_storage_key = tif_key
        task.jpg_storage_key = jpg_key
        task.tif_url = tif_url
        task.preview_url = preview_url
        task.tif_size_bytes = tif_size
        task.jpg_size_bytes = jpg_size
        task.tif_checksum = tif_hash
        task.jpg_checksum = jpg_hash
        task.original_width = orig_w
        task.original_height = orig_h
        task.bounds = MapBounds(**bounds_dict)
        task.corners = MapCorners(**corners_dict)

        # Optional: Delete source raw images from MinIO to save disk space
        if getattr(request, "delete_source_images", False):
            try:
                logger.info(f"Deleting {len(request.images)} raw source images from MinIO...")
                keys_to_delete = [img.storage_key for img in request.images]
                minio_storage.delete_files(keys_to_delete)
                logger.info("Raw source images successfully deleted from MinIO.")
            except Exception as e:
                logger.warning(f"Failed to delete source images from MinIO: {e}")

        logger.info(f"Task {task_id} completed successfully in {task.processing_time_ms} ms")

    except Exception as ex:
        logger.error(f"Task {task_id} failed: {ex}", exc_info=True)
        task.status = "FAILED"
        task.error_message = str(ex)
        task.processing_time_ms = int((time.time() - start_time) * 1000)

    finally:
        # Clean up temporary files
        try:
            shutil.rmtree(work_dir, ignore_errors=True)
        except Exception:
            pass


@app.post("/api/v1/metashape/process", response_model=ProcessMetashapeResponse)
def process_metashape(request: ProcessMetashapeRequest, background_tasks: BackgroundTasks):
    task_id = str(uuid.uuid4())
    task_record = MetashapeTaskStatusResponse(
        mission_id=request.mission_id,
        task_id=task_id,
        status="QUEUED",
        progress=0,
        stage="Queued for photogrammetry processing"
    )
    tasks_db[task_id] = task_record

    # Submit job to background executor
    executor.submit(execute_metashape_job, task_id, request)

    return ProcessMetashapeResponse(
        mission_id=request.mission_id,
        task_id=task_id,
        status="QUEUED",
        message="Metashape task successfully submitted."
    )


def execute_odm_job(task_id: str, request: ProcessMetashapeRequest):
    start_time = time.time()
    work_dir = os.path.join(settings.temp_dir, f"odm_{task_id}")
    images_dir = os.path.join(work_dir, "images")
    output_dir = os.path.join(work_dir, "output")
    os.makedirs(images_dir, exist_ok=True)
    os.makedirs(output_dir, exist_ok=True)

    task = tasks_db[task_id]
    task.status = "RUNNING"
    task.progress = 5
    task.stage = "Downloading images from MinIO for ODM"

    try:
        # 1. Download images from MinIO
        local_images = []
        for idx, img in enumerate(request.images):
            ext = os.path.splitext(img.storage_key)[1] or ".jpg"
            local_img_path = os.path.join(images_dir, f"img_{idx:04d}{ext}")
            minio_storage.download_file(img.storage_key, local_img_path)
            local_images.append(local_img_path)

        if not local_images:
            raise ODMProcessingError("No images downloaded from MinIO for ODM processing.")

        output_tif = os.path.join(output_dir, "orthophoto.tif")
        output_jpg = os.path.join(output_dir, "map_preview.jpg")

        def on_progress(pct: int, stg: str):
            task.progress = pct
            task.stage = stg
            task.processing_time_ms = int((time.time() - start_time) * 1000)

        # 2. Run OpenDroneMap Pipeline via NodeODM (Maximum quality matching Metashape)
        bounds_dict, corners_dict, orig_w, orig_h = run_odm_orthophoto_pipeline(
            photo_paths=local_images,
            output_tif_path=output_tif,
            output_jpg_path=output_jpg,
            camera_type=request.camera_type,
            node_host=settings.nodeodm_host,
            node_port=settings.nodeodm_port,
            progress_callback=on_progress
        )

        task.stage = "Uploading results to MinIO"
        task.progress = 95

        # 3. Upload GeoTIFF to MinIO
        tif_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/orthophoto.tif"
        tif_size, tif_hash = minio_storage.upload_file(output_tif, tif_key, "image/tiff")

        # 4. Upload Map Preview JPEG to MinIO
        jpg_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/map_preview.jpg"
        jpg_size, jpg_hash = minio_storage.upload_file(output_jpg, jpg_key, "image/jpeg")

        # 4b. Upload metadata.json to MinIO for fast map lookup
        try:
            meta_payload = {
                "mission_id": request.mission_id,
                "tenant_id": request.tenant_id,
                "farm_id": request.farm_id,
                "bounds": bounds_dict,
                "corners": corners_dict,
                "original_width": orig_w,
                "original_height": orig_h,
                "created_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            }
            meta_path = os.path.join(output_dir, "metadata.json")
            with open(meta_path, "w", encoding="utf-8") as f:
                json.dump(meta_payload, f, indent=2)
            meta_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/metadata.json"
            minio_storage.upload_file(meta_path, meta_key, "application/json")
        except Exception as e:
            logger.warning(f"Failed to upload metadata.json: {e}")

        # 5. Generate presigned URLs
        tif_url = minio_storage.get_presigned_url(tif_key)
        preview_url = minio_storage.get_presigned_url(jpg_key)

        task.status = "COMPLETED"
        task.progress = 100
        task.stage = "Completed"
        task.processing_time_ms = int((time.time() - start_time) * 1000)
        task.tif_storage_key = tif_key
        task.jpg_storage_key = jpg_key
        task.tif_url = tif_url
        task.preview_url = preview_url
        task.tif_size_bytes = tif_size
        task.jpg_size_bytes = jpg_size
        task.tif_checksum = tif_hash
        task.jpg_checksum = jpg_hash
        task.original_width = orig_w
        task.original_height = orig_h
        task.bounds = MapBounds(**bounds_dict)
        task.corners = MapCorners(**corners_dict)

        # Optional: Delete source raw images from MinIO to save disk space
        if getattr(request, "delete_source_images", False):
            try:
                logger.info(f"Deleting {len(request.images)} raw source images from MinIO...")
                keys_to_delete = [img.storage_key for img in request.images]
                minio_storage.delete_files(keys_to_delete)
                logger.info("Raw source images successfully deleted from MinIO.")
            except Exception as e:
                logger.warning(f"Failed to delete source images from MinIO: {e}")

        logger.info(f"ODM Task {task_id} completed successfully in {task.processing_time_ms} ms")

    except Exception as ex:
        logger.error(f"ODM Task {task_id} failed: {ex}", exc_info=True)
        task.status = "FAILED"
        task.error_message = str(ex)
        task.processing_time_ms = int((time.time() - start_time) * 1000)

    finally:
        # Clean up temporary files
        try:
            shutil.rmtree(work_dir, ignore_errors=True)
        except Exception:
            pass


@app.post("/api/v1/odm/process", response_model=ProcessMetashapeResponse, tags=["OpenDroneMap (ODM) Processing"])
def process_odm(request: ProcessMetashapeRequest, background_tasks: BackgroundTasks):
    task_id = str(uuid.uuid4())
    task_record = MetashapeTaskStatusResponse(
        mission_id=request.mission_id,
        task_id=task_id,
        status="QUEUED",
        progress=0,
        stage="Queued for OpenDroneMap photogrammetry processing"
    )
    tasks_db[task_id] = task_record

    # Submit job to background executor
    executor.submit(execute_odm_job, task_id, request)

    return ProcessMetashapeResponse(
        mission_id=request.mission_id,
        task_id=task_id,
        status="QUEUED",
        message="OpenDroneMap task successfully submitted."
    )


@app.get("/api/v1/odm/tasks/{task_id}", response_model=MetashapeTaskStatusResponse, tags=["OpenDroneMap (ODM) Processing"])
def get_odm_task_status(task_id: str):
    if task_id not in tasks_db:
        raise HTTPException(status_code=404, detail=f"ODM Task {task_id} not found")
    return tasks_db[task_id]


@app.get("/api/v1/metashape/tasks/{task_id}", response_model=MetashapeTaskStatusResponse)
def get_task_status(task_id: str):
    if task_id not in tasks_db:
        raise HTTPException(status_code=404, detail=f"Task {task_id} not found")
    return tasks_db[task_id]


@app.post("/api/v1/metashape/tasks/{task_id}/cancel")
def cancel_task(task_id: str):
    if task_id not in tasks_db:
        raise HTTPException(status_code=404, detail=f"Task {task_id} not found")
    task = tasks_db[task_id]
    if task.status in ("QUEUED", "RUNNING"):
        task.status = "CANCELED"
        task.stage = "Canceled by user"
        return {"task_id": task_id, "status": "CANCELED"}
    return {"task_id": task_id, "status": task.status, "message": "Cannot cancel completed/failed task."}


@app.post("/api/v1/tiling/slice", response_model=TileSliceResponse)
def slice_orthophoto_tiles(request: TileSliceRequest):
    """
    Slices a GeoTIFF orthomosaic into chips for YOLO detection.
    Default: dual_scale=True, generating BOTH:
      - 640x640 (overlap 64px, prefix tile_640) for close-up fine detail
      - 1280x1280 (overlap 128px, prefix tile_1280) for wide spatial context & row continuity
    Minimizes false negatives (bỏ sót cây) when YOLO runs inference.
    """
    work_dir = os.path.join(settings.temp_dir, f"tiling_{request.mission_id}")
    os.makedirs(work_dir, exist_ok=True)
    local_tif = os.path.join(work_dir, "source.tif")
    tiles_output_dir = os.path.join(work_dir, "tiles")
    os.makedirs(tiles_output_dir, exist_ok=True)

    try:
        # Determine source GeoTIFF
        if request.tif_storage_key:
            minio_storage.download_file(request.tif_storage_key, local_tif)
        else:
            default_tif_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/orthophoto.tif"
            minio_storage.download_file(default_tif_key, local_tif)

        manifest = []
        count_640 = 0
        count_1280 = 0

        if request.dual_scale:
            # Scale 1: 640x640, overlap 64px
            c1, m1 = slice_geotiff(
                tif_path=local_tif,
                output_dir=tiles_output_dir,
                tile_size=640,
                overlap=64,
                tile_prefix="tile_640",
                polygon_geojson=request.polygon_geojson,
                image_format="JPEG"
            )
            count_640 = c1
            manifest.extend(m1)

            # Scale 2: 1280x1280, overlap 128px
            c2, m2 = slice_geotiff(
                tif_path=local_tif,
                output_dir=tiles_output_dir,
                tile_size=1280,
                overlap=128,
                tile_prefix="tile_1280",
                polygon_geojson=request.polygon_geojson,
                image_format="JPEG"
            )
            count_1280 = c2
            manifest.extend(m2)
        else:
            c, m = slice_geotiff(
                tif_path=local_tif,
                output_dir=tiles_output_dir,
                tile_size=request.tile_size,
                overlap=request.overlap,
                tile_prefix=f"tile_{request.tile_size}",
                polygon_geojson=request.polygon_geojson,
                image_format="JPEG"
            )
            if request.tile_size == 640:
                count_640 = c
            elif request.tile_size == 1280:
                count_1280 = c
            manifest.extend(m)

        # Ghi đè file manifest.json tổng hợp chứa TOÀN BỘ cả 640 và 1280 tiles
        manifest_path = os.path.join(tiles_output_dir, "manifest.json")
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2)

        tile_items: list[TileItem] = []
        for item in manifest:
            storage_key = None
            url = None
            if request.upload_to_minio:
                storage_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/tiles/{item['file_name']}"
                minio_storage.upload_file(item["local_path"], storage_key, "image/jpeg")
                url = minio_storage.get_presigned_url(storage_key)
            else:
                # Provide local preview URL for UI without uploading to MinIO
                url = f"/api/v1/tiling/temp-preview/{request.mission_id}/{item['file_name']}"

            tile_items.append(
                TileItem(
                    tile_index=item["tile_index"],
                    tile_size=item.get("tile_size"),
                    col_off=item["col_off"],
                    row_off=item["row_off"],
                    width=item["width"],
                    height=item["height"],
                    file_name=item["file_name"],
                    storage_key=storage_key,
                    url=url,
                    bounds=MapBounds(**item["bounds"]),
                    corners=MapCorners(**item["corners"]),
                    affine_transform=item["affine_transform"]
                )
            )

        return TileSliceResponse(
            mission_id=request.mission_id,
            total_tiles=len(tile_items),
            total_tiles_640=count_640,
            total_tiles_1280=count_1280,
            dual_scale=request.dual_scale,
            tiles=tile_items
        )

    finally:
        # Delete large downloaded GeoTIFF copy to save disk space, but keep tiles for YOLO
        if os.path.exists(local_tif):
            try:
                os.remove(local_tif)
            except Exception:
                pass


@app.get("/api/v1/tiling/temp-preview/{mission_id}/{file_name}", tags=["Interactive Map Tool"])
def get_temp_tile_preview(mission_id: str, file_name: str):
    tile_path = os.path.join(settings.temp_dir, f"tiling_{mission_id}", "tiles", file_name)
    if os.path.exists(tile_path):
        return FileResponse(tile_path, media_type="image/jpeg")
    raise HTTPException(status_code=404, detail="Temp tile not found")




best_model = None
def get_yolo_model():
    global best_model
    if best_model is None:
        if YOLO is None:
            raise RuntimeError("Ultralytics YOLO is not installed.")
        model_path = os.path.join(os.path.dirname(__file__), "best.pt")
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"Model file best.pt not found at {model_path}")
        best_model = YOLO(model_path)
        logger.info(f"Loaded YOLO model successfully from {model_path}")
    return best_model


@app.post("/api/v1/yolo/detect-mission", response_model=YoloDetectMissionResponse, tags=["YOLO Tree Detection"])
def detect_mission_trees(request: YoloDetectMissionRequest):
    """
    Tự động nạp toàn bộ ảnh tiles đã cắt của mission, chạy mô hình best.pt:
    1. Quét toàn bộ thư mục tiles cục bộ (không cần MinIO).
    2. Gom tất cả bounding boxes của tất cả các ảnh lại.
    3. Quy đổi pixel sang tọa độ GPS thực tế bằng affine transform của từng tile.
    4. Lọc trùng lặp (Spatial NMS khoảng cách < 1.2m).
    5. Xoay hệ tọa độ theo góc vườn (PCA), tự động chia số Hàng và Cột.
    6. Phát hiện các vị trí BỎ SÓT cây hoặc CÂY CHẾT (Missing Trees).
    7. Xuất ra 1 file CSV tổng hợp duy nhất trees_{mission_id}.csv.
    """
    model = get_yolo_model()
    tiles_dir = os.path.join(settings.temp_dir, f"tiling_{request.mission_id}", "tiles")
    manifest_path = os.path.join(tiles_dir, "manifest.json")

    if not os.path.exists(tiles_dir) or not os.path.exists(manifest_path):
        raise HTTPException(
            status_code=400,
            detail=f"Tiles or manifest not found for mission {request.mission_id}. Vui lòng chạy cắt ảnh (tiling slice) trước."
        )

    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest_data = json.load(f)

    manifest_map = {m["file_name"]: m["affine_transform"] for m in manifest_data}
    tile_files = [os.path.join(tiles_dir, fn) for fn in manifest_map.keys() if os.path.exists(os.path.join(tiles_dir, fn))]

    if not tile_files:
        raise HTTPException(status_code=400, detail="Không tìm thấy file ảnh tile nào trong thư mục tạm.")

    device = 0 if (torch is not None and torch.cuda.is_available()) else "cpu"
    batch_size = 4 if device == 0 else 2
    logger.info(f"Running YOLO inference on {len(tile_files)} tiles for mission {request.mission_id} (device={device}, batch={batch_size}, stream=True)...")
    if torch is not None and torch.cuda.is_available():
        torch.cuda.empty_cache()

    results = model.predict(
        source=tile_files,
        conf=request.confidence_threshold,
        device=device,
        batch=batch_size,
        stream=True,
        verbose=False
    )

    raw_detections = []
    for r in results:
        tile_name = os.path.basename(r.path)
        if tile_name not in manifest_map:
            continue
        affine = manifest_map[tile_name]
        for box in r.boxes:
            x, y, w, h = box.xywh[0].tolist()
            conf = float(box.conf[0])
            lat, lon = pixel_to_gps(x, y, affine)

            # Lọc chỉ giữ lại các cây nằm BÊN TRONG viền vườn người dùng vẽ
            if request.polygon_geojson and not is_inside_polygon(lat, lon, request.polygon_geojson):
                continue

            raw_detections.append({
                "lat": lat,
                "lon": lon,
                "confidence": conf,
                "source": "YOLO",
                "tile_name": tile_name
            })

    logger.info(f"YOLO detected {len(raw_detections)} raw objects inside polygon across {len(tile_files)} tiles.")

    # 4. Deduplicate
    unique_trees = deduplicate_trees(raw_detections, min_distance_meters=1.2)

    # 5. Grid assignment & missing tree gaps
    anchor = None
    if request.anchor_lat is not None and request.anchor_lon is not None:
        anchor = {"lat": request.anchor_lat, "lon": request.anchor_lon}

    active_trees, missing_trees, suggested_row, suggested_col = assign_grid_and_detect_missing(
        trees=unique_trees,
        row_spacing=request.row_spacing,
        col_spacing=request.col_spacing,
        anchor_point=anchor,
        custom_angle_deg=request.custom_angle_deg,
        polygon_geojson=request.polygon_geojson
    )

    max_row = max((t["row"] for t in active_trees), default=0)
    max_col = max((t["col"] for t in active_trees), default=0)

    # 6. Export single consolidated CSV file (kèm theo ranh giới viền thửa đất Polygon)
    csv_filename = f"trees_{request.mission_id}.csv"
    csv_path = os.path.join(tiles_dir, csv_filename)
    export_trees_to_csv(active_trees, missing_trees, csv_path, polygon_geojson=request.polygon_geojson)

    csv_url = f"/api/v1/yolo/download-csv/{request.mission_id}"

    return YoloDetectMissionResponse(
        mission_id=request.mission_id,
        model_name="best.pt (Dragon-Fruit-class)",
        total_tiles_scanned=len(tile_files),
        total_raw_detections=len(raw_detections),
        total_unique_trees=len(active_trees),
        total_missing_trees=len(missing_trees),
        suggested_row_spacing=suggested_row,
        suggested_col_spacing=suggested_col,
        max_row=max_row,
        max_col=max_col,
        csv_download_url=csv_url,
        active_trees=[TreeItem(**t) for t in active_trees],
        missing_trees=[TreeItem(**t) for t in missing_trees]
    )


@app.get("/api/v1/yolo/download-csv/{mission_id}", tags=["YOLO Tree Detection"])
def download_trees_csv(mission_id: str):
    tiles_dir = os.path.join(settings.temp_dir, f"tiling_{mission_id}", "tiles")
    latest_path = os.path.join(tiles_dir, f"trees_{mission_id}_latest.csv")
    csv_path = os.path.join(tiles_dir, f"trees_{mission_id}.csv")
    actual_path = latest_path if os.path.exists(latest_path) else csv_path
    if os.path.exists(actual_path):
        return FileResponse(actual_path, media_type="text/csv", filename=f"trees_{mission_id}.csv")
    raise HTTPException(status_code=404, detail="Trees CSV file not found for this mission.")


@app.get("/api/v1/yolo/trees/{mission_id}", tags=["YOLO Tree Detection"])
def get_mission_trees(mission_id: str):
    import csv
    tiles_dir = os.path.join(settings.temp_dir, f"tiling_{mission_id}", "tiles")
    latest_path = os.path.join(tiles_dir, f"trees_{mission_id}_latest.csv")
    csv_path = os.path.join(tiles_dir, f"trees_{mission_id}.csv")
    actual_path = latest_path if os.path.exists(latest_path) else csv_path
    if not os.path.exists(actual_path):
        return {"has_trees": False, "active_trees": [], "missing_trees": []}

    active_trees = []
    missing_trees = []
    boundary_points = []
    with open(actual_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for r in reader:
            item = {
                "id": r.get("tree_id", ""),
                "row": int(r.get("row", 1)),
                "col": int(r.get("col", 1)),
                "lat": float(r.get("latitude", 0)),
                "lon": float(r.get("longitude", 0)),
                "confidence": float(r.get("confidence", 1.0)),
                "status": r.get("status", "DETECTED"),
                "source": r.get("source", "YOLO")
            }
            if item["status"] == "MISSING":
                missing_trees.append(item)
            elif item["status"] == "BOUNDARY":
                boundary_points.append(item)
            else:
                active_trees.append(item)

    max_row = max((t["row"] for t in active_trees), default=0)
    max_col = max((t["col"] for t in active_trees), default=0)

    return {
        "has_trees": True,
        "total_unique_trees": len(active_trees),
        "total_missing_trees": len(missing_trees),
        "total_boundary_points": len(boundary_points),
        "max_row": max_row,
        "max_col": max_col,
        "active_trees": active_trees,
        "missing_trees": missing_trees,
        "boundary_points": boundary_points
    }



if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host=settings.host, port=settings.port, reload=True)
