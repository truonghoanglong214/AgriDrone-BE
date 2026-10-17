from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field

class ImageItem(BaseModel):
    storage_key: str
    url: Optional[str] = None

class ProcessMetashapeRequest(BaseModel):
    mission_id: str
    tenant_id: str
    farm_id: str
    images: List[ImageItem] = Field(default_factory=list)
    camera_type: str = "equidistant_fisheye"
    align_accuracy: str = "medium"
    generic_preselection: bool = True
    delete_source_images: bool = True

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
    tenant_id: str = "tenant-001"
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

