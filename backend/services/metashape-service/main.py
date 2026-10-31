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
    YoloRecalculateGridRequest,
    DiseaseDetectionItem,
    DiseaseDetectMissionRequest,
    DiseaseDetectMissionResponse,
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


@app.post("/api/v1/yolo/recalculate-grid", response_model=YoloDetectMissionResponse, tags=["YOLO Tree Detection"])
def recalculate_tree_grid(request: YoloRecalculateGridRequest):
    """
    Tính toán lại vị trí hàng và cột cho danh sách cây hiện có mà không cần quét lại YOLO:
    1. Xác định hướng chuẩn của hàng cây qua Neighbor Angle Voting (peak ~69°).
    2. Phân cụm hàng 1D projection trên trục v_across (cây cùng luống không bao giờ nhảy hàng).
    3. Cây neo mốc (Anchor Tree) luôn luôn được gán Hàng 1 - Cột 1 (is_anchor = True).
    4. Quét khoảng trống thực tế để phát hiện cây khuyết (Missing Trees).
    5. Cập nhật lại file CSV trees_{mission_id}.csv.
    """
    if not request.active_trees:
        raise HTTPException(status_code=400, detail="Danh sách cây rỗng, không thể tính toán lại.")

    anchor = None
    if request.anchor_lat is not None and request.anchor_lon is not None:
        anchor = {"lat": request.anchor_lat, "lon": request.anchor_lon}

    active_trees, missing_trees, suggested_row, suggested_col = assign_grid_and_detect_missing(
        trees=request.active_trees,
        row_spacing=request.row_spacing,
        col_spacing=request.col_spacing,
        anchor_point=anchor,
        custom_angle_deg=request.custom_angle_deg,
        polygon_geojson=request.polygon_geojson
    )

    max_row = max((t["row"] for t in active_trees), default=0)
    max_col = max((t["col"] for t in active_trees), default=0)

    tiles_dir = os.path.join(settings.temp_dir, f"tiling_{request.mission_id}", "tiles")
    if os.path.exists(tiles_dir):
        csv_path = os.path.join(tiles_dir, f"trees_{request.mission_id}.csv")
        export_trees_to_csv(active_trees, missing_trees, csv_path, polygon_geojson=request.polygon_geojson)

    csv_url = f"/api/v1/yolo/download-csv/{request.mission_id}"

    return YoloDetectMissionResponse(
        mission_id=request.mission_id,
        model_name="recalculated",
        total_tiles_scanned=0,
        total_raw_detections=len(active_trees),
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
    """Tải file CSV kết quả YOLO của mission (tự động nhận diện trees CSV hoặc diseases CSV)."""
    # 1. Kiểm tra trees CSV (Mission tạo bản đồ / định danh cây)
    tiles_dir = os.path.join(settings.temp_dir, f"tiling_{mission_id}", "tiles")
    latest_trees = os.path.join(tiles_dir, f"trees_{mission_id}_latest.csv")
    csv_trees = os.path.join(tiles_dir, f"trees_{mission_id}.csv")
    actual_trees = latest_trees if os.path.exists(latest_trees) else csv_trees
    if os.path.exists(actual_trees):
        return FileResponse(actual_trees, media_type="text/csv", filename=f"trees_{mission_id}.csv")

    # 2. Kiểm tra diseases CSV (Mission quét bệnh cây)
    disease_dir = os.path.join(settings.temp_dir, f"disease_{mission_id}")
    latest_disease = os.path.join(disease_dir, f"diseases_{mission_id}_latest.csv")
    csv_disease = os.path.join(disease_dir, f"diseases_{mission_id}.csv")
    actual_disease = latest_disease if os.path.exists(latest_disease) else csv_disease
    if os.path.exists(actual_disease):
        return FileResponse(actual_disease, media_type="text/csv", filename=f"diseases_{mission_id}.csv")

    raise HTTPException(status_code=404, detail="YOLO CSV file not found for this mission.")


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





# ═══════════════════════════════════════════════════════════════════════════════
# ───  Disease Detection Pipeline (best_disease.pt & BBox Crops & Plots)  ─────
# ═══════════════════════════════════════════════════════════════════════════════

disease_model = None

def get_disease_model():
    """Load the disease detection YOLO model (best_disease.pt).
    Falls back to best.pt if best_disease.pt is not found so the API
    can still be tested during development."""
    global disease_model
    if disease_model is None:
        if YOLO is None:
            raise RuntimeError("Ultralytics YOLO is not installed.")
        service_dir = os.path.dirname(__file__)
        primary_path = os.path.join(service_dir, "best_disease.pt")
        weights_path = os.path.join(service_dir, "weights", "best_disease.pt")
        fallback_path = os.path.join(service_dir, "best.pt")
        if os.path.exists(primary_path):
            model_path = primary_path
        elif os.path.exists(weights_path):
            model_path = weights_path
        elif os.path.exists(fallback_path):
            logger.warning(
                "best_disease.pt not found, falling back to best.pt for disease detection."
            )
            model_path = fallback_path
        else:
            raise FileNotFoundError(
                f"Neither best_disease.pt nor best.pt found at {service_dir}"
            )
        disease_model = YOLO(model_path)
        logger.info(f"Loaded Disease YOLO model from {model_path}")
    return disease_model


def extract_exif_gps(image_path: str):
    """Extract decimal (latitude, longitude) from JPEG EXIF GPS metadata."""
    try:
        import piexif
        exif_dict = piexif.load(image_path)
        gps = exif_dict.get("GPS")
        if not gps or piexif.GPSIFD.GPSLatitude not in gps or piexif.GPSIFD.GPSLongitude not in gps:
            return None

        lat_data = gps[piexif.GPSIFD.GPSLatitude]
        lat_ref = gps.get(piexif.GPSIFD.GPSLatitudeRef, b'N')
        if isinstance(lat_ref, bytes):
            lat_ref = lat_ref.decode('latin1', errors='ignore')

        lon_data = gps[piexif.GPSIFD.GPSLongitude]
        lon_ref = gps.get(piexif.GPSIFD.GPSLongitudeRef, b'E')
        if isinstance(lon_ref, bytes):
            lon_ref = lon_ref.decode('latin1', errors='ignore')

        lat = (lat_data[0][0] / lat_data[0][1] +
               lat_data[1][0] / (lat_data[1][1] * 60) +
               lat_data[2][0] / (lat_data[2][1] * 3600))
        if str(lat_ref).strip().upper() == 'S':
            lat = -lat

        lon = (lon_data[0][0] / lon_data[0][1] +
               lon_data[1][0] / (lon_data[1][1] * 60) +
               lon_data[2][0] / (lon_data[2][1] * 3600))
        if str(lon_ref).strip().upper() == 'W':
            lon = -lon

        return lat, lon
    except Exception as e:
        logger.warning(f"Could not extract EXIF GPS from {image_path}: {e}")
        return None


@app.post("/api/v1/disease/detect-mission", response_model=DiseaseDetectMissionResponse, tags=["Disease Detection"])
def detect_mission_diseases(request: DiseaseDetectMissionRequest):
    """
    Nhận diện bệnh trên cây trồng bằng mô hình best_disease.pt:
    - Quét trực tiếp các ảnh thô đã gắn GPS (raw images từ MinIO).
    - Tự động trích xuất tọa độ GPS từ EXIF từng ảnh.
    - Chạy mô hình YOLO phát hiện các loại bệnh.
    - Tạo ảnh đã vẽ boundary viền bao quanh vết bệnh (annotated images).
    - Cắt (crop) ảnh vùng cây bệnh lưu vào thư mục ảnh tạm phục vụ xem bằng chứng.
    - Xuất file CSV: diseases_{mission_id}.csv.
    """
    import csv as csv_module
    import cv2
    from PIL import Image

    model = get_disease_model()
    class_names = model.names if hasattr(model, "names") else {}

    disease_dir = os.path.join(settings.temp_dir, f"disease_{request.mission_id}")
    images_dir = os.path.join(disease_dir, "images")
    crops_dir = os.path.join(disease_dir, "crops")
    annotated_dir = os.path.join(disease_dir, "annotated")
    os.makedirs(images_dir, exist_ok=True)
    os.makedirs(crops_dir, exist_ok=True)
    os.makedirs(annotated_dir, exist_ok=True)

    local_image_items: list[tuple[str, str, Optional[tuple[float, float]]]] = []

    # 1. Thu thập ảnh: từ request.images (MinIO hoặc disk)
    if request.images and len(request.images) > 0:
        logger.info(f"Preparing {len(request.images)} raw images for Disease detection (Mission {request.mission_id})...")
        for idx, img in enumerate(request.images):
            ext = os.path.splitext(img.storage_key)[1] or ".jpg"
            img_filename = f"img_{idx:04d}{ext}"
            local_path = os.path.join(images_dir, img_filename)

            if os.path.isabs(img.storage_key) and os.path.exists(img.storage_key):
                if not os.path.exists(local_path):
                    shutil.copy(img.storage_key, local_path)
            elif not os.path.exists(local_path) or os.path.getsize(local_path) == 0:
                try:
                    minio_storage.download_file(img.storage_key, local_path)
                except Exception as ex:
                    logger.warning(f"Failed to download image {img.storage_key} from MinIO: {ex}")
                    continue

            gps_coords = extract_exif_gps(local_path)
            local_image_items.append((local_path, img_filename, gps_coords))

    # Fallback: kiểm tra nếu ảnh đã có trong thư mục images
    elif os.path.exists(images_dir):
        for fn in sorted(os.listdir(images_dir)):
            if fn.lower().endswith((".jpg", ".jpeg", ".png")):
                local_path = os.path.join(images_dir, fn)
                gps_coords = extract_exif_gps(local_path)
                local_image_items.append((local_path, fn, gps_coords))

    if not local_image_items:
        raise HTTPException(
            status_code=400,
            detail=f"Không tìm thấy ảnh để nhận diện bệnh cho mission {request.mission_id}. "
                   f"Vui lòng cung cấp danh sách ảnh thô đã gắn GPS (images)."
        )

    total_scanned = len(local_image_items)
    image_paths = [item[0] for item in local_image_items]

    device = 0 if (torch is not None and torch.cuda.is_available()) else "cpu"
    batch_size = 4 if device == 0 else 2
    logger.info(f"Running Disease YOLO inference on {total_scanned} images for mission {request.mission_id} (device={device}, batch={batch_size})...")
    if torch is not None and torch.cuda.is_available():
        torch.cuda.empty_cache()

    results = model.predict(
        source=image_paths,
        conf=request.confidence_threshold,
        device=device,
        batch=batch_size,
        stream=True,
        verbose=False
    )

    detections: list[DiseaseDetectionItem] = []
    disease_counter: dict[str, int] = {}
    annotated_image_urls: list[str] = []
    det_idx = 0

    for (img_path, img_filename, gps_coords), r in zip(local_image_items, results):
        img_lat = gps_coords[0] if gps_coords else 0.0
        img_lon = gps_coords[1] if gps_coords else 0.0

        if request.polygon_geojson and gps_coords and not is_inside_polygon(
            img_lat, img_lon, request.polygon_geojson
        ):
            continue

        annotated_url = None
        pil_img = None
        if len(r.boxes) > 0:
            # 1. Tạo và lưu ảnh vẽ sẵn khung boundary bao quanh các vết bệnh (Annotated Image)
            try:
                annotated_frame = r.plot()
                annotated_filename = f"annotated_{img_filename}"
                annotated_path = os.path.join(annotated_dir, annotated_filename)
                cv2.imwrite(annotated_path, annotated_frame)
                annotated_url = f"/api/v1/disease/annotated/{request.mission_id}/{annotated_filename}"
                annotated_image_urls.append(annotated_url)
            except Exception as ex:
                logger.warning(f"Failed to plot annotated image for {img_filename}: {ex}")

            # 2. Chuẩn bị crop ảnh
            try:
                pil_img = Image.open(img_path)
                im_w, im_h = pil_img.size
            except Exception:
                pil_img = None

        for box in r.boxes:
            x, y, w, h = box.xywh[0].tolist()
            conf = float(box.conf[0])
            cls_id = int(box.cls[0])
            cls_name = class_names.get(cls_id, f"class_{cls_id}")

            det_idx += 1
            crop_filename = f"crop_{det_idx:04d}_{cls_name}.jpg"
            crop_path = os.path.join(crops_dir, crop_filename)
            crop_url = f"/api/v1/disease/crops/{request.mission_id}/{crop_filename}"

            # Crop vùng ảnh cây bị bệnh lưu file tạm
            if pil_img is not None:
                try:
                    xmin, ymin, xmax, ymax = box.xyxy[0].tolist()
                    pad_x = (xmax - xmin) * 0.1
                    pad_y = (ymax - ymin) * 0.1
                    c_xmin = max(0, int(xmin - pad_x))
                    c_ymin = max(0, int(ymin - pad_y))
                    c_xmax = min(im_w, int(xmax + pad_x))
                    c_ymax = min(im_h, int(ymax + pad_y))
                    cropped = pil_img.crop((c_xmin, c_ymin, c_xmax, c_ymax))
                    cropped.convert("RGB").save(crop_path, "JPEG", quality=95)
                except Exception as ex:
                    logger.warning(f"Failed to crop image {crop_path}: {ex}")

            detections.append(
                DiseaseDetectionItem(
                    id=f"disease_{det_idx:04d}",
                    class_id=cls_id,
                    class_name=cls_name,
                    lat=img_lat,
                    lon=img_lon,
                    confidence=round(conf, 4),
                    bbox_x=round(x, 2),
                    bbox_y=round(y, 2),
                    bbox_w=round(w, 2),
                    bbox_h=round(h, 2),
                    image_name=img_filename,
                    crop_image_url=crop_url if os.path.exists(crop_path) else None,
                    annotated_image_url=annotated_url,
                )
            )
            disease_counter[cls_name] = disease_counter.get(cls_name, 0) + 1

    logger.info(
        f"Disease detection complete: {len(detections)} detections across {total_scanned} images. Summary: {disease_counter}"
    )

    # Xuất file CSV
    csv_filename = f"diseases_{request.mission_id}.csv"
    actual_csv_path = os.path.join(disease_dir, csv_filename)
    try:
        f_csv = open(actual_csv_path, mode="w", newline="", encoding="utf-8")
    except PermissionError:
        actual_csv_path = os.path.join(disease_dir, f"diseases_{request.mission_id}_latest.csv")
        f_csv = open(actual_csv_path, mode="w", newline="", encoding="utf-8")

    with f_csv:
        writer = csv_module.writer(f_csv)
        writer.writerow([
            "detection_id", "class_id", "class_name",
            "latitude", "longitude", "confidence",
            "bbox_x", "bbox_y", "bbox_w", "bbox_h",
            "image_name", "crop_image_url", "annotated_image_url"
        ])
        for d in detections:
            writer.writerow([
                d.id, d.class_id, d.class_name,
                d.lat, d.lon, d.confidence,
                d.bbox_x, d.bbox_y, d.bbox_w, d.bbox_h,
                d.image_name, d.crop_image_url or "",
                d.annotated_image_url or ""
            ])

    logger.info(f"Exported disease CSV to {actual_csv_path}")

    csv_url = f"/api/v1/yolo/download-csv/{request.mission_id}"
    model_file = os.path.basename(model.ckpt_path) if hasattr(model, "ckpt_path") else "best_disease.pt"

    return DiseaseDetectMissionResponse(
        mission_id=request.mission_id,
        model_name=f"{model_file} (Disease-Detection)",
        total_images_scanned=total_scanned,
        total_detections=len(detections),
        disease_summary=disease_counter,
        csv_download_url=csv_url,
        annotated_image_urls=annotated_image_urls,
        detections=detections,
    )


@app.get("/api/v1/disease/annotated/{mission_id}/{filename}", tags=["Disease Detection"])
def get_disease_annotated_image(mission_id: str, filename: str):
    """Phục vụ ảnh gốc đã vẽ sẵn bounding box các vết bệnh (annotated image)."""
    annotated_path = os.path.join(settings.temp_dir, f"disease_{mission_id}", "annotated", filename)
    if os.path.exists(annotated_path):
        return FileResponse(annotated_path, media_type="image/jpeg", filename=filename)
    raise HTTPException(status_code=404, detail="Annotated image not found.")


@app.get("/api/v1/disease/crops/{mission_id}/{filename}", tags=["Disease Detection"])
def get_disease_crop(mission_id: str, filename: str):
    """Phục vụ ảnh crop các vết bệnh đã phát hiện."""
    crop_path = os.path.join(settings.temp_dir, f"disease_{mission_id}", "crops", filename)
    if os.path.exists(crop_path):
        return FileResponse(crop_path, media_type="image/jpeg", filename=filename)
    raise HTTPException(status_code=404, detail="Crop image not found.")


@app.get("/api/v1/disease/download-csv/{mission_id}", tags=["Disease Detection"])
def download_diseases_csv(mission_id: str):
    """Tải file CSV chứa toàn bộ vết bệnh cây đã nhận diện cho mission."""
    disease_dir = os.path.join(settings.temp_dir, f"disease_{mission_id}")
    latest_path = os.path.join(disease_dir, f"diseases_{mission_id}_latest.csv")
    csv_path = os.path.join(disease_dir, f"diseases_{mission_id}.csv")
    actual_path = latest_path if os.path.exists(latest_path) else csv_path
    if os.path.exists(actual_path):
        return FileResponse(actual_path, media_type="text/csv", filename=f"diseases_{mission_id}.csv")
    raise HTTPException(status_code=404, detail="Diseases CSV file not found for this mission.")


@app.get("/api/v1/disease/detections/{mission_id}", tags=["Disease Detection"])
def get_mission_disease_detections(mission_id: str):
    """Đọc toàn bộ kết quả phát hiện bệnh cây từ file CSV của mission."""
    import csv as csv_module
    disease_dir = os.path.join(settings.temp_dir, f"disease_{mission_id}")
    latest_path = os.path.join(disease_dir, f"diseases_{mission_id}_latest.csv")
    csv_path = os.path.join(disease_dir, f"diseases_{mission_id}.csv")
    actual_path = latest_path if os.path.exists(latest_path) else csv_path
    if not os.path.exists(actual_path):
        return {"has_detections": False, "total_detections": 0, "detections": []}

    detections = []
    with open(actual_path, "r", encoding="utf-8") as f:
        reader = csv_module.DictReader(f)
        for r in reader:
            detections.append({
                "id": r.get("detection_id", ""),
                "class_id": int(r.get("class_id", 0)),
                "class_name": r.get("class_name", ""),
                "lat": float(r.get("latitude", 0)),
                "lon": float(r.get("longitude", 0)),
                "confidence": float(r.get("confidence", 0)),
                "bbox_x": float(r.get("bbox_x", 0)),
                "bbox_y": float(r.get("bbox_y", 0)),
                "bbox_w": float(r.get("bbox_w", 0)),
                "bbox_h": float(r.get("bbox_h", 0)),
                "image_name": r.get("image_name", ""),
                "crop_image_url": r.get("crop_image_url", ""),
                "annotated_image_url": r.get("annotated_image_url", "")
            })

    return {
        "has_detections": True,
        "total_detections": len(detections),
        "detections": detections
    }






@app.get("/api/v1/disease/gallery/{mission_id}", response_class=HTMLResponse, tags=["Disease Detection"])
def view_disease_gallery(mission_id: str):
    """Giao diện web trực quan hiển thị toàn bộ 61 vết bệnh, ảnh crop và ảnh vẽ boundary."""
    import csv as csv_module
    disease_dir = os.path.join(settings.temp_dir, f"disease_{mission_id}")
    csv_path = os.path.join(disease_dir, f"diseases_{mission_id}.csv")
    if not os.path.exists(csv_path):
        return HTMLResponse(f"<h3>Chưa có dữ liệu nhận diện bệnh cho mission {mission_id}</h3>", status_code=404)

    rows = []
    with open(csv_path, "r", encoding="utf-8") as f:
        reader = csv_module.DictReader(f)
        for r in reader:
            rows.append(r)

    summary = {}
    for r in rows:
        c = r.get("class_name", "Unknown")
        summary[c] = summary.get(c, 0) + 1

    cards_html = ""
    for r in rows:
        cls = r.get("class_name", "")
        conf = float(r.get("confidence", 0)) * 100
        lat = r.get("latitude", "")
        lon = r.get("longitude", "")
        crop_url = r.get("crop_image_url", "")
        annotated_url = r.get("annotated_image_url", "")
        det_id = r.get("detection_id", "")
        img_name = r.get("image_name", "")

        is_healthy = "Healthy" in cls
        badge_color = "#10b981" if is_healthy else "#ef4444"

        cards_html += f"""
        <div style="background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.06);border:1px solid #e5e7eb;display:flex;flex-direction:column;">
            <div style="position:relative;background:#f3f4f6;text-align:center;">
                <img src="{crop_url}" alt="{cls}" style="height:190px;width:100%;object-fit:cover;cursor:pointer;" onclick="window.open('{crop_url}','_blank')"/>
                <span style="position:absolute;top:8px;left:8px;background:{badge_color};color:#fff;font-size:11px;font-weight:700;padding:3px 9px;border-radius:999px;">{cls}</span>
                <span style="position:absolute;top:8px;right:8px;background:rgba(0,0,0,0.65);color:#fff;font-size:11px;font-weight:600;padding:3px 8px;border-radius:999px;">{conf:.1f}%</span>
            </div>
            <div style="padding:14px;font-size:13px;color:#374151;display:flex;flex-direction:column;gap:6px;flex:1;">
                <div style="font-weight:700;color:#111827;font-size:14px;">#{det_id} · <span style="font-weight:400;color:#6b7280;font-size:13px;">{img_name}</span></div>
                <div style="font-size:12px;color:#4b5563;">📍 Tọa độ GPS: <b>{lat[:9]}, {lon[:9]}</b></div>
                <div style="margin-top:auto;padding-top:10px;display:flex;gap:8px;">
                    <a href="{annotated_url}" target="_blank" style="flex:1;text-align:center;background:#2563eb;color:#fff;text-decoration:none;padding:7px 0;border-radius:6px;font-size:12px;font-weight:600;">Xem ảnh khoanh viền</a>
                    <a href="https://www.google.com/maps?q={lat},{lon}" target="_blank" style="background:#f3f4f6;color:#374151;text-decoration:none;padding:7px 12px;border-radius:6px;font-size:12px;font-weight:500;">Bản đồ</a>
                </div>
            </div>
        </div>
        """

    summary_pills = "".join(f"<span style='background:#fff;padding:6px 12px;border-radius:20px;font-size:13px;border:1px solid #d1d5db;box-shadow:0 1px 2px rgba(0,0,0,0.05);'><b>{k}</b>: <span style=\'color:#2563eb;font-weight:700;\'>{v}</span></span>" for k, v in summary.items())

    html = f"""<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Kết Quả Nhận Diện Bệnh - Mission {mission_id}</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background:#f8fafc; margin:0; padding:28px; color:#111827; }}
        .header {{ max-width:1380px; margin:0 auto 24px; }}
        .grid {{ max-width:1380px; margin:0 auto; display:grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap:20px; }}
    </style>
</head>
<body>
    <div class="header">
        <h1 style="margin:0 0 6px 0;font-size:24px;color:#0f172a;">🌿 Báo Cáo Nhận Diện Cây Bệnh (Mission: {mission_id})</h1>
        <p style="margin:0 0 16px 0;color:#64748b;font-size:14px;">Đã quét xong toàn bộ <b>216 ảnh</b> thô đã gắn GPS. Phát hiện <b>{len(rows)} vết bệnh</b>.</p>
        <div style="display:flex;flex-wrap:wrap;align-items:center;gap:8px;">
            {summary_pills}
            <a href="/api/v1/yolo/download-csv/{mission_id}" style="margin-left:auto;background:#059669;color:#fff;text-decoration:none;padding:7px 16px;border-radius:6px;font-size:13px;font-weight:600;">📥 Tải CSV</a>
        </div>
    </div>
    <div class="grid">
        {cards_html}
    </div>
</body>
</html>"""
    return HTMLResponse(content=html)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host=settings.host, port=settings.port, reload=True)
