import logging
import os
import shutil
import time
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional, Tuple

import rasterio
from rasterio.warp import transform_bounds
from PIL import Image

# Vô hiệu hóa giới hạn kích thước ảnh của PIL để hỗ trợ ảnh GeoTIFF kích thước lớn (>100 triệu pixel)
Image.MAX_IMAGE_PIXELS = None

logger = logging.getLogger("odm-workflow")

class ODMProcessingError(Exception):
    """Custom exception raised when OpenDroneMap pipeline encounters an error."""
    pass


def extract_geotiff_metadata(tif_path: str) -> Tuple[Dict, Dict, int, int]:
    """
    Reads the generated GeoTIFF using rasterio to extract EPSG:4326 corners, bbox, and dimensions.
    Matches metashape_workflow.extract_geotiff_metadata 1:1.
    """
    with rasterio.open(tif_path) as src:
        bounds = src.bounds
        src_crs = src.crs
        orig_w = src.width
        orig_h = src.height

        if src_crs and src_crs.to_string() != "EPSG:4326":
            min_lon, min_lat, max_lon, max_lat = transform_bounds(
                src_crs, "EPSG:4326", bounds.left, bounds.bottom, bounds.right, bounds.top
            )
        else:
            min_lon, min_lat, max_lon, max_lat = bounds.left, bounds.bottom, bounds.right, bounds.top

        corners = {
            "north_west": [round(max_lat, 7), round(min_lon, 7)],
            "north_east": [round(max_lat, 7), round(max_lon, 7)],
            "south_east": [round(min_lat, 7), round(max_lon, 7)],
            "south_west": [round(min_lat, 7), round(min_lon, 7)],
        }

        bbox = {
            "min_lat": round(min_lat, 7),
            "max_lat": round(max_lat, 7),
            "min_lon": round(min_lon, 7),
            "max_lon": round(max_lon, 7),
        }

        return bbox, corners, orig_w, orig_h


def optimize_display_jpeg(jpg_path: str, max_dimension: int = 4096, quality: int = 85) -> Tuple[int, int]:
    """
    Optimizes the exported JPEG for web display (4K, ~3MB).
    Matches metashape_workflow.optimize_display_jpeg 1:1.
    """
    Image.MAX_IMAGE_PIXELS = None
    with Image.open(jpg_path) as img:
        orig_w, orig_h = img.size
        target_w, target_h = orig_w, orig_h

        if max_dimension and (orig_w > max_dimension or orig_h > max_dimension):
            scale = max_dimension / max(orig_w, orig_h)
            target_w = int(orig_w * scale)
            target_h = int(orig_h * scale)
            img = img.resize((target_w, target_h), Image.Resampling.LANCZOS)

        img.save(jpg_path, "JPEG", quality=quality, optimize=True)
        final_size = os.path.getsize(jpg_path)
        logger.info(f"Optimized display JPEG: {target_w}x{target_h}, size={final_size / (1024*1024):.2f} MB")
        return target_w, target_h


def run_odm_orthophoto_pipeline(
    photo_paths: List[str],
    output_tif_path: str,
    output_jpg_path: str,
    camera_type: str = "frame",
    node_host: str = "localhost",
    node_port: int = 3000,
    progress_callback: Optional[Callable[[int, str], None]] = None
) -> Tuple[Dict, Dict, int, int]:
    """
    Executes the OpenDroneMap (ODM) pipeline via PyODM + NodeODM with MAXIMUM QUALITY settings,
    mapped directly to match `metashape_workflow.py` output (full GSD resolution, LZW compression, 3D Mesh).

    ODM Parameters:
    - camera-lens: 'perspective' (default for linear/frame) or 'fisheye' (when camera_type is equidistant_fisheye/wide)
    - feature-quality: 'high'
    - min-num-features: 12000
    - pc-quality: 'high'
    - use-3dmesh: True
    - mesh-octree-depth: 11
    - fast-orthophoto: False (ensures dense depthmap and true 3D projection)
    - orthophoto-resolution: 0.1 (native sensor GSD)
    - orthophoto-compression: 'LZW' (lossless GeoTIFF matching Metashape)
    """
    try:
        from pyodm import Node
        from pyodm.types import TaskStatus
    except ImportError:
        raise ODMProcessingError(
            "Thư viện pyodm chưa được cài đặt. Hãy chạy: pip install pyodm"
        )

    def report(percent: int, stage_name: str):
        logger.info(f"[{percent}%] [ODM] {stage_name}")
        if progress_callback:
            progress_callback(percent, stage_name)

    if not photo_paths:
        raise ODMProcessingError("Không có ảnh drone nào được cung cấp.")

    for p in photo_paths:
        if not os.path.exists(p):
            raise ODMProcessingError(f"File ảnh không tồn tại: {p}")

    os.makedirs(os.path.dirname(os.path.abspath(output_tif_path)), exist_ok=True)
    os.makedirs(os.path.dirname(os.path.abspath(output_jpg_path)), exist_ok=True)

    try:
        report(5, f"Connecting to NodeODM at http://{node_host}:{node_port}")
        node = Node(node_host, node_port)

        # Kiểm tra kết nối tới NodeODM
        try:
            info = node.info()
            logger.info(f"Connected to NodeODM engine: {getattr(info, 'engine', 'ODM')}, version: {getattr(info, 'version', 'unknown')}")
        except Exception as conn_err:
            raise ODMProcessingError(
                f"Không thể kết nối tới NodeODM tại {node_host}:{node_port}. "
                f"Hãy đảm bảo Docker container đang chạy: `docker run -d -p {node_port}:3000 opendronemap/nodeodm`"
            ) from conn_err

        report(10, f"Uploading {len(photo_paths)} photos to NodeODM...")

        # Ánh xạ Camera Type sang ODM lens model:
        # - Mặc định cho góc quay Linear là: "perspective"
        # - Nếu chọn góc Wide / Fisheye là: "fisheye"
        lens_type = "perspective"
        if camera_type and any(k in camera_type.lower() for k in ["fisheye", "wide"]):
            lens_type = "fisheye"
        elif camera_type and any(k in camera_type.lower() for k in ["linear", "frame", "perspective"]):
            lens_type = "perspective"

        logger.info(f"Using camera projection model: '{lens_type}' (derived from camera_type='{camera_type}')")

        # CẤU HÌNH TƯƠNG ĐƯƠNG METASHAPE HEIGHTFIELD TỪ TIE POINTS (15 - 20 PHÚT):
        # - Bỏ tính Depthmaps (OpenMVS)
        # - Bỏ dựng đám mây điểm dày đặc (Dense Point Cloud)
        # - Bỏ dán vân ảnh 3D (texrecon / buildTexture)
        # - Dựng mặt phẳng độ cao 2.5D DSM từ Tie Points và chiếu Orthophoto trực tiếp (fast-orthophoto)
        odm_options = {
            # 1. Camera Projection Model
            "camera-lens": lens_type,

            # 2. Feature Detection & Alignment (High chất lượng cao tương đương Metashape Medium/High)
            "feature-quality": "high",
            "min-num-features": 8000,
            
            # 3. Chế độ tương đương Metashape HeightField từ Tie Points:
            "fast-orthophoto": True,   # Dựng DSM 2.5D từ điểm thưa, bỏ qua hoàn toàn Depthmaps, Dense Cloud & Texturing
            "use-3dmesh": False,       # Không dựng Full 3D đa giác
            "pc-quality": "lowest",    # Bỏ qua khâu tăng mật độ điểm tốn hàng tiếng

            # 4. Orthophoto Resolution (GSD gốc tuyệt đối của máy ảnh drone)
            "orthophoto-resolution": 0.1,

            # 5. Compression (Lossless GeoTIFF tương đương Metashape TiffCompressionLZW)
            "orthophoto-compression": "LZW",

            # 6. Seamless mosaic blending (Pha trộn màu đa dải, khử vết cắt biên ảnh)
            "orthophoto-no-mrs": False,
        }

        task = node.create_task(files=photo_paths, options=odm_options)
        report(20, f"Task created successfully (UUID: {task.uuid}). Processing pipeline started...")

        def on_odm_status(t_info):
            if t_info.status == TaskStatus.RUNNING:
                scaled_pct = 20 + int(t_info.progress * 0.65)
                report(scaled_pct, f"ODM Photogrammetry Processing... ({t_info.progress:.1f}%)")
            elif t_info.status == TaskStatus.QUEUED:
                report(15, "ODM Task is queued in NodeODM waiting for execution slot...")

        # Chờ NodeODM hoàn thành với cơ chế callback và tự động retry
        task.wait_for_completion(status_callback=on_odm_status, interval=3)

        report(88, "Downloading high-resolution orthophoto assets from NodeODM...")
        temp_download_dir = os.path.join(os.path.dirname(os.path.abspath(output_tif_path)), "odm_temp_assets")
        os.makedirs(temp_download_dir, exist_ok=True)
        task.download_assets(temp_download_dir)

        # Tìm file GeoTIFF kết quả trong assets tải về
        downloaded_tif = None
        for candidate in [
            os.path.join(temp_download_dir, "odm_orthophoto", "odm_orthophoto.tif"),
            os.path.join(temp_download_dir, "orthophoto.tif"),
        ]:
            if os.path.exists(candidate):
                downloaded_tif = candidate
                break

        if not downloaded_tif:
            tif_files = [str(f) for f in Path(temp_download_dir).rglob("*.tif") if "dsm" not in f.name.lower() and "dtm" not in f.name.lower()]
            if tif_files:
                downloaded_tif = tif_files[0]

        if not downloaded_tif or not os.path.exists(downloaded_tif):
            raise ODMProcessingError(f"Tải thành công nhưng không tìm thấy odm_orthophoto.tif trong {temp_download_dir}")

        tif_file_size_mb = os.path.getsize(downloaded_tif) / (1024 * 1024)
        logger.info(f"Downloaded GeoTIFF file size: {tif_file_size_mb:.2f} MB")

        # Copy ra đường dẫn đích
        shutil.copy2(downloaded_tif, output_tif_path)

        report(92, "Generating web preview JPEG from high-resolution GeoTIFF...")
        with Image.open(output_tif_path) as im:
            im.thumbnail((4096, 4096), Image.Resampling.LANCZOS)
            rgb_im = im.convert("RGB")
            rgb_im.save(output_jpg_path, "JPEG", quality=85)

        report(96, "Extracting bounds and 4 GPS corner coordinates via rasterio...")
        bounds, corners, orig_w, orig_h = extract_geotiff_metadata(output_tif_path)

        # Dọn dẹp thư mục tạm trên ổ cứng
        shutil.rmtree(temp_download_dir, ignore_errors=True)

        # Giải phóng dung lượng trong Docker container NodeODM (xóa task data)
        try:
            task.remove()
            logger.info(f"NodeODM task {task.uuid} cleaned up from container storage.")
        except Exception as cleanup_err:
            logger.warning(f"Failed to cleanup NodeODM task {task.uuid}: {cleanup_err}")

        report(100, f"ODM pipeline completed successfully! GeoTIFF: {tif_file_size_mb:.2f} MB ({orig_w}x{orig_h} px)")
        return bounds, corners, orig_w, orig_h

    except Exception as ex:
        logger.error(f"Error during OpenDroneMap processing: {ex}", exc_info=True)
        raise ODMProcessingError(f"OpenDroneMap processing failed: {str(ex)}") from ex
