import asyncio
import json
import logging
import os
import shutil
import time
import uuid
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager
from typing import Dict

from fastapi import BackgroundTasks, FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, HTMLResponse

# Ultralytics YOLO inference for orchard tree detection
try:
    from ultralytics import YOLO
except Exception:
    YOLO = None


from config import settings
from map_picker import get_map_picker_html
from metashape_workflow import (
    Metashape,
    MetashapeProcessingError,
    extract_geotiff_metadata,
    run_metashape_orthophoto_pipeline,
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


@app.get("/map-picker", response_class=HTMLResponse, tags=["Interactive Map Tool"])
def map_picker_ui(
    mission_id: str = "8d2ba93d-1421-448d-be83-a1034e8c4aab",
    tenant_id: str = "cf637409-fb85-4dfd-8fd3-ae3ea799d635",
    min_lat: float = 10.4611274,
    max_lat: float = 10.4617541,
    min_lon: float = 106.4936806,
    max_lon: float = 106.4945788,
):
    """
    Giao diện Web tương tác trực quan:
    - Hiển thị ảnh Drone JPG nén tối ưu (2.5MB) lồng lên Google Satellite.
    - Zoom vào và click chuột chọn các đỉnh ranh giới thửa đất (Polygon).
    - Tự động bắt tọa độ GPS từ ảnh và sinh GeoJSON.
    - Cho phép ấn 'Cắt Mảnh' trực tiếp để test endpoint YOLO Tiling.
    """
    html_content = get_map_picker_html(
        mission_id=mission_id,
        tenant_id=tenant_id,
        min_lat=min_lat,
        max_lat=max_lat,
        min_lon=min_lon,
        max_lon=max_lon,
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

    logger.info(f"Running YOLO inference on {len(tile_files)} tiles for mission {request.mission_id}...")
    results = model.predict(source=tile_files, conf=request.confidence_threshold, verbose=False)

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

    # 6. Export single consolidated CSV file
    csv_filename = f"trees_{request.mission_id}.csv"
    csv_path = os.path.join(tiles_dir, csv_filename)
    export_trees_to_csv(active_trees, missing_trees, csv_path)

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
            else:
                active_trees.append(item)

    max_row = max((t["row"] for t in active_trees), default=0)
    max_col = max((t["col"] for t in active_trees), default=0)

    return {
        "has_trees": True,
        "total_unique_trees": len(active_trees),
        "total_missing_trees": len(missing_trees),
        "max_row": max_row,
        "max_col": max_col,
        "active_trees": active_trees,
        "missing_trees": missing_trees
    }



if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host=settings.host, port=settings.port, reload=True)
