import pytest
from models import (
    MapBounds,
    MapCorners,
    MetashapeTaskStatusResponse,
    ProcessMetashapeRequest,
    TileItem,
    TileSliceRequest,
)

def test_process_metashape_request_defaults():
    req = ProcessMetashapeRequest(
        mission_id="11111111-1111-1111-1111-111111111111",
        tenant_id="22222222-2222-2222-2222-222222222222",
        farm_id="33333333-3333-3333-3333-333333333333"
    )
    assert req.camera_type == "equidistant_fisheye"
    assert req.align_accuracy == "medium"
    assert req.generic_preselection is True
    assert req.images == []

def test_metashape_task_status_response_serialization():
    bounds = MapBounds(min_lat=10.1, max_lat=10.2, min_lon=106.1, max_lon=106.2)
    corners = MapCorners(
        north_west=[10.2, 106.1],
        north_east=[10.2, 106.2],
        south_east=[10.1, 106.2],
        south_west=[10.1, 106.1]
    )
    resp = MetashapeTaskStatusResponse(
        task_id="abc-123",
        status="COMPLETED",
        progress=100,
        bounds=bounds,
        corners=corners,
        tif_storage_key="tenants/1/orthophoto.tif",
        jpg_storage_key="tenants/1/map_preview.jpg",
        original_width=8000,
        original_height=6000
    )
    data = resp.model_dump()
    assert data["status"] == "COMPLETED"
    assert data["bounds"]["min_lat"] == 10.1
    assert data["corners"]["north_west"] == [10.2, 106.1]

def test_tile_slice_request():
    req = TileSliceRequest(
        mission_id="m1",
        tenant_id="t1",
        tile_size=1024,
        overlap=128
    )
    assert req.tile_size == 1024
    assert req.overlap == 128
    assert req.polygon_geojson is None
