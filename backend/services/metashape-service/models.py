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
    tile_size: int = 640
    overlap: int = 64
    polygon_geojson: Optional[Dict[str, Any]] = None
    upload_to_minio: bool = True

class TileSliceResponse(BaseModel):
    mission_id: str
    total_tiles: int
    tile_size: int
    overlap: int
    tiles: List[TileItem] = Field(default_factory=list)
