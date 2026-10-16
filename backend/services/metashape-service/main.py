import asyncio
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

from config import settings
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


app = FastAPI(
    title=settings.app_name,
    version="1.0.0",
    description="FastAPI service for automated Agisoft Metashape photogrammetry and GeoTIFF tiling.",
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
    Slices a GeoTIFF orthomosaic into 640x640 chips with overlap for YOLO detection.
    Supports polygon boundary filtering to only generate chips within the farm.
    """
    work_dir = os.path.join(settings.temp_dir, f"tiling_{request.mission_id}_{uuid.uuid4().hex[:8]}")
    os.makedirs(work_dir, exist_ok=True)
    local_tif = os.path.join(work_dir, "source.tif")
    tiles_output_dir = os.path.join(work_dir, "tiles")

    try:
        # Determine source GeoTIFF
        if request.tif_storage_key:
            minio_storage.download_file(request.tif_storage_key, local_tif)
        else:
            default_tif_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/orthophoto.tif"
            minio_storage.download_file(default_tif_key, local_tif)

        # Execute Windowed Reading slicing
        total_tiles, manifest = slice_geotiff(
            tif_path=local_tif,
            output_dir=tiles_output_dir,
            tile_size=request.tile_size,
            overlap=request.overlap,
            polygon_geojson=request.polygon_geojson,
            image_format="JPEG"
        )

        tile_items: list[TileItem] = []
        for item in manifest:
            storage_key = None
            url = None
            if request.upload_to_minio:
                storage_key = f"tenants/{request.tenant_id}/media/{request.mission_id}/tiles/{item['file_name']}"
                minio_storage.upload_file(item["local_path"], storage_key, "image/jpeg")
                url = minio_storage.get_presigned_url(storage_key)

            tile_items.append(
                TileItem(
                    tile_index=item["tile_index"],
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
            total_tiles=total_tiles,
            tile_size=request.tile_size,
            overlap=request.overlap,
            tiles=tile_items
        )

    finally:
        shutil.rmtree(work_dir, ignore_errors=True)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host=settings.host, port=settings.port, reload=True)
