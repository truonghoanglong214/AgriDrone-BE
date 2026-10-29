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
    assert req.camera_type == "frame"
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


def test_disease_detection_models():
    from models import (
        DiseaseDetectionItem,
        DiseaseDetectMissionRequest,
        DiseaseDetectMissionResponse,
        ImageItem,
    )

    req = DiseaseDetectMissionRequest(
        mission_id="m-disease-1",
        images=[ImageItem(storage_key="test/key.jpg", url="http://minio/key.jpg")]
    )
    assert req.mission_id == "m-disease-1"
    assert req.confidence_threshold == 0.25
    assert len(req.images) == 1

    item = DiseaseDetectionItem(
        id="disease_0001",
        class_id=1,
        class_name="canker",
        lat=10.4614,
        lon=106.4941,
        confidence=0.89,
        bbox_x=120.5,
        bbox_y=340.2,
        bbox_w=45.0,
        bbox_h=50.0,
        image_name="img_0000.jpg",
        crop_image_url="/api/v1/disease/crops/m-disease-1/crop_0001_canker.jpg"
    )
    resp = DiseaseDetectMissionResponse(
        mission_id="m-disease-1",
        model_name="best_disease.pt",
        total_images_scanned=1,
        total_detections=1,
        disease_summary={"canker": 1},
        csv_download_url="/api/v1/disease/download-csv/m-disease-1",
        detections=[item]
    )
    data = resp.model_dump()
    assert data["total_detections"] == 1
    assert data["total_images_scanned"] == 1
    assert data["disease_summary"]["canker"] == 1
    assert data["detections"][0]["class_name"] == "canker"
    assert data["detections"][0]["crop_image_url"] == "/api/v1/disease/crops/m-disease-1/crop_0001_canker.jpg"

