from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field, field_validator

class ImageItem(BaseModel):
    storage_key: str
    url: Optional[str] = None

class ProcessMetashapeRequest(BaseModel):
    mission_id: str
    tenant_id: str
    farm_id: str
    images: List[ImageItem] = Field(default_factory=list)
    camera_type: str = "frame"
    align_accuracy: str = "medium"
    generic_preselection: bool = True
    delete_source_images: bool = True

    @field_validator("camera_type", mode="before")
    @classmethod
    def default_camera_type(cls, v):
        return v or "frame"

    @field_validator("align_accuracy", mode="before")
    @classmethod
    def default_align_accuracy(cls, v):
        return v or "medium"

    @field_validator("generic_preselection", mode="before")
    @classmethod
    def default_generic_preselection(cls, v):
        return True if v is None else v

class ProcessMetashapeResponse(BaseModel):
    mission_id: str
    task_id: str
    status: str
    message: str

class MapCoordinate(BaseModel):
    latitude: float
    longitude: float

class MapCorners(BaseModel):
    north_west: List[float] # [lat, lon]
    north_east: List[float]
    south_east: List[float]
    south_west: List[float]

class MapBounds(BaseModel):
    min_lat: float
    max_lat: float
    min_lon: float
    max_lon: float

class MetashapeTaskStatusResponse(BaseModel):
    mission_id: Optional[str] = None
    task_id: str
    status: str
    progress: int = 0
    stage: Optional[str] = None
    processing_time_ms: int = 0
    error_message: Optional[str] = None
    tif_storage_key: Optional[str] = None
    jpg_storage_key: Optional[str] = None
    tif_url: Optional[str] = None
    preview_url: Optional[str] = None
    bounds: Optional[MapBounds] = None
    corners: Optional[MapCorners] = None
    original_width: Optional[int] = None
    original_height: Optional[int] = None
    tif_size_bytes: Optional[int] = None
    jpg_size_bytes: Optional[int] = None
    tif_checksum: Optional[str] = None
    jpg_checksum: Optional[str] = None

class TileItem(BaseModel):
    tile_index: int
    tile_size: Optional[int] = None
    col_off: int
    row_off: int
    width: int
    height: int
    file_name: str
    storage_key: Optional[str] = None
    url: Optional[str] = None
    bounds: MapBounds
    corners: MapCorners
    affine_transform: List[float]

class TileSliceRequest(BaseModel):
    mission_id: str
    tenant_id: str
    tif_storage_key: Optional[str] = None
    dual_scale: bool = True
    tile_size: int = 640
    overlap: int = 64
    polygon_geojson: Optional[Dict[str, Any]] = None
    upload_to_minio: bool = False

class TileSliceResponse(BaseModel):
    mission_id: str
    total_tiles: int
    total_tiles_640: Optional[int] = 0
    total_tiles_1280: Optional[int] = 0
    dual_scale: bool = True
    tiles: List[TileItem] = Field(default_factory=list)


class TreeItem(BaseModel):
    id: str
    row: int
    col: int
    lat: float
    lon: float
    confidence: float = 1.0
    status: str
    source: str
    is_anchor: Optional[bool] = False


class YoloDetectMissionRequest(BaseModel):
    mission_id: str
    tenant_id: str
    confidence_threshold: float = 0.25
    row_spacing: float = 2.8
    col_spacing: float = 2.7
    anchor_lat: Optional[float] = None
    anchor_lon: Optional[float] = None
    custom_angle_deg: Optional[float] = None
    polygon_geojson: Optional[Dict[str, Any]] = None


class YoloDetectMissionResponse(BaseModel):
    mission_id: str
    model_name: str
    total_tiles_scanned: int
    total_raw_detections: int
    total_unique_trees: int
    total_missing_trees: int
    suggested_row_spacing: Optional[float] = None
    suggested_col_spacing: Optional[float] = None
    max_row: int
    max_col: int
    csv_download_url: Optional[str] = None
    active_trees: List[TreeItem] = Field(default_factory=list)
    missing_trees: List[TreeItem] = Field(default_factory=list)


# ─── Disease Detection Models ─────────────────────────────────────────────────

class DiseaseDetectionItem(BaseModel):
    """A single disease detection instance with GPS coordinates, cropped image and annotated image."""
    id: str
    class_id: int
    class_name: str
    lat: float
    lon: float
    confidence: float
    bbox_x: float
    bbox_y: float
    bbox_w: float
    bbox_h: float
    image_name: str
    crop_image_url: Optional[str] = None
    annotated_image_url: Optional[str] = None


class DiseaseDetectMissionRequest(BaseModel):
    """Request to run disease detection directly on raw geotagged mission images."""
    mission_id: str
    tenant_id: str = "tenant-001"
    confidence_threshold: float = 0.25
    polygon_geojson: Optional[Dict[str, Any]] = None
    images: Optional[List[ImageItem]] = None


class DiseaseDetectMissionResponse(BaseModel):
    """Response containing all disease detections for a mission."""
    mission_id: str
    model_name: str
    total_images_scanned: int
    total_detections: int
    disease_summary: Dict[str, int] = Field(default_factory=dict)
    csv_download_url: Optional[str] = None
    annotated_image_urls: List[str] = Field(default_factory=list)
    detections: List[DiseaseDetectionItem] = Field(default_factory=list)



