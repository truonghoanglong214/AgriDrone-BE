import logging
import os
from typing import Any, Callable, Dict, List, Optional, Tuple
import rasterio
from rasterio.warp import transform_bounds

logger = logging.getLogger("metashape-workflow")

try:
    import Metashape 
except ImportError:
    Metashape: Any = None  

class MetashapeProcessingError(Exception):
    pass

def run_metashape_orthophoto_pipeline(
    photo_paths: List[str],
    output_tif_path: str,
    output_jpg_path: str,
    camera_type: str = "equidistant_fisheye",
    align_accuracy: str = "medium",
    generic_preselection: bool = True,
    progress_callback: Optional[Callable[[int, str], None]] = None
) -> Tuple[Dict, Dict, int, int]:
    """
    Executes the automated Agisoft Metashape Pro workflow for GoPro 5 / Fisheye cameras:
    1. Add photos to new Chunk.
    2. Set camera type to Equidistant Fisheye.
    3. Align photos (Medium accuracy, Generic preselection).
    4. Build Mesh Model (Height Field, Tie Points data).
    5. Build Orthomosaic (EPSG::4326, Mesh surface, Mosaic blending).
    6. Export GeoTIFF (.tif) with LZW compression and embedded EPSG:4326 metadata.
    7. Export flat preview image (.jpg) with 100% quality and Mosaic blending.
    8. Extract 4 GPS corners and bounding box from the resulting GeoTIFF.
    """
    if Metashape is None:
        raise MetashapeProcessingError(
            "Agisoft Metashape module is not installed. Please install the Metashape Pro Python wheel."
        )

    def report(percent: int, stage_name: str):
        logger.info(f"[{percent}%] {stage_name}")
        if progress_callback:
            progress_callback(percent, stage_name)

    if not photo_paths:
        raise MetashapeProcessingError("No input photos provided for photogrammetry processing.")

    for p in photo_paths:
        if not os.path.exists(p):
            raise MetashapeProcessingError(f"Image file does not exist: {p}")

    os.makedirs(os.path.dirname(os.path.abspath(output_tif_path)), exist_ok=True)
    os.makedirs(os.path.dirname(os.path.abspath(output_jpg_path)), exist_ok=True)

    try:
        report(5, "Initializing Metashape project and chunk")
        work_dir = os.path.dirname(os.path.abspath(output_tif_path))
        project_psx = os.path.join(work_dir, "project.psx")
        doc = Metashape.Document()
        doc.save(project_psx)
        chunk = doc.addChunk()
        doc.save()

        report(10, f"Adding {len(photo_paths)} photos to chunk")
        chunk.addPhotos(photo_paths)

        if len(chunk.cameras) == 0:
            raise MetashapeProcessingError("Failed to add cameras to Metashape chunk.")

        report(15, "Configuring camera sensor type (Equidistant Fisheye)")
        for sensor in chunk.sensors:
            sensor.type = Metashape.Sensor.Type.Fisheye

        # Accuracy mapping for matchPhotos (downscale)
        accuracy_map = {
            "highest": 0,
            "high": 1,
            "medium": 2,
            "low": 4,
            "lowest": 8
        }
        downscale = accuracy_map.get(align_accuracy.lower(), 2)

        report(25, "Matching photos (Feature detection & Preselection)")
        chunk.matchPhotos(
            downscale=downscale,
            generic_preselection=generic_preselection,
            reference_preselection=True,
            filter_mask=False,
            keypoint_limit=40000,
            tiepoint_limit=4000
        )

        report(45, "Aligning cameras")
        chunk.alignCameras()

        aligned_count = sum(1 for c in chunk.cameras if c.transform)
        logger.info(f"Aligned cameras: {aligned_count} / {len(chunk.cameras)}")
        if aligned_count < 2:
            raise MetashapeProcessingError(
                f"Insufficient cameras aligned ({aligned_count}/{len(chunk.cameras)}). Alignment failed."
            )

        report(60, "Building 3D Mesh Model (Height Field from Tie Points)")
        # Determine Tie Points data source across Metashape versions
        tie_points_source = getattr(
            Metashape.DataSource, "TiePointsData",
            getattr(Metashape.DataSource, "PointCloudData", None)
        )
        if tie_points_source is None:
            tie_points_source = Metashape.DataSource.TiePointsData

        chunk.buildModel(
            surface_type=Metashape.SurfaceType.HeightField,
            interpolation=Metashape.Interpolation.EnabledInterpolation,
            face_count=Metashape.FaceCount.MediumFaceCount,
            source_data=tie_points_source
        )
        doc.save()

        report(75, "Building Orthomosaic (Projection: EPSG::4326, Surface: Mesh)")
        projection = Metashape.OrthoProjection()
        projection.crs = Metashape.CoordinateSystem("EPSG::4326")
        chunk.buildOrthomosaic(
            surface_data=Metashape.DataSource.ModelData,
            blending_mode=Metashape.BlendingMode.MosaicBlending,
            projection=projection
        )
        doc.save()

        report(85, "Exporting GeoTIFF (.tif) with LZW compression and EPSG:4326")
        tif_compression = Metashape.ImageCompression()
        tif_compression.tiff_compression = Metashape.ImageCompression.TiffCompressionLZW
        tif_compression.tiff_big = True

        chunk.exportRaster(
            path=output_tif_path,
            image_format=Metashape.ImageFormatTIFF,
            image_compression=tif_compression,
            projection=projection,
            save_alpha=True
        )

        report(92, "Exporting flat image (.jpg) with 100% quality and Mosaic blending")
        jpg_compression = Metashape.ImageCompression()
        jpg_compression.jpeg_quality = 100
        chunk.exportRaster(
            path=output_jpg_path,
            image_format=Metashape.ImageFormatJPEG,
            image_compression=jpg_compression,
            projection=projection,
            save_alpha=False
        )

        report(96, "Extracting bounds and 4 corner GPS coordinates")
        bounds, corners, orig_w, orig_h = extract_geotiff_metadata(output_tif_path)

        report(100, "Metashape processing completed successfully")
        return bounds, corners, orig_w, orig_h

    except Exception as ex:
        logger.error(f"Error during Metashape processing: {ex}", exc_info=True)
        raise MetashapeProcessingError(f"Metashape processing failed: {str(ex)}") from ex


def extract_geotiff_metadata(tif_path: str) -> Tuple[Dict, Dict, int, int]:
    """Reads the generated GeoTIFF using rasterio to extract EPSG:4326 corners, bbox, and dimensions."""
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
