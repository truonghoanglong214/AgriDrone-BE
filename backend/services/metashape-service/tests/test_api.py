import pytest
from fastapi.testclient import TestClient
from main import app, tasks_db
from models import MetashapeTaskStatusResponse

client = TestClient(app)

def test_health_endpoint():
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert "metashape_available" in data
    assert "minio_endpoint" in data

def test_process_submission_endpoint():
    payload = {
        "mission_id": "11111111-1111-1111-1111-111111111111",
        "tenant_id": "22222222-2222-2222-2222-222222222222",
        "farm_id": "33333333-3333-3333-3333-333333333333",
        "images": [],
        "camera_type": "equidistant_fisheye",
        "align_accuracy": "medium"
    }
    response = client.post("/api/v1/metashape/process", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "task_id" in data
    assert data["status"] == "QUEUED"
    assert data["mission_id"] == payload["mission_id"]

    task_id = data["task_id"]
    status_resp = client.get(f"/api/v1/metashape/tasks/{task_id}")
    assert status_resp.status_code == 200
    assert status_resp.json()["task_id"] == task_id

def test_get_nonexistent_task():
    response = client.get("/api/v1/metashape/tasks/non-existent-task-id")
    assert response.status_code == 404

def test_cancel_task():
    task_id = "test-cancel-id"
    tasks_db[task_id] = MetashapeTaskStatusResponse(
        task_id=task_id,
        status="QUEUED",
        progress=0,
        stage="Queued"
    )

    response = client.post(f"/api/v1/metashape/tasks/{task_id}/cancel")
    assert response.status_code == 200
    assert response.json()["status"] == "CANCELED"
    assert tasks_db[task_id].status == "CANCELED"


def test_disease_detections_not_found():
    response = client.get("/api/v1/disease/detections/non-existent-mission")
    assert response.status_code == 200
    data = response.json()
    assert data["has_detections"] is False
    assert data["detections"] == []


def test_disease_csv_not_found():
    response = client.get("/api/v1/disease/download-csv/non-existent-mission")
    assert response.status_code == 404


def test_disease_crop_not_found():
    response = client.get("/api/v1/disease/crops/non-existent-mission/crop_0001.jpg")
    assert response.status_code == 404


def test_disease_detect_missing_images():
    response = client.post("/api/v1/disease/detect-mission", json={
        "mission_id": "non-existent-mission-id",
        "confidence_threshold": 0.25
    })
    assert response.status_code == 400

