# AgriDrone Metashape Photogrammetry & Tiling Service

FastAPI Microservice tự động hóa xử lý ảnh drone GoPro 5 (Equidistant Fisheye) bằng **Agisoft Metashape Pro** và cắt GeoTIFF thành các patch nhỏ phục vụ nhận diện cây bằng **YOLO**.

---

## 1. Cấu hình & Quy trình Metashape Pro

Dịch vụ thực thi quy trình tối ưu cho camera fisheye GoPro 5:

1. **Khởi tạo & Nạp ảnh**:
   - Tạo Project Document và Chunk mới trong Metashape.
   - Nạp toàn bộ ảnh chụp từ drone đã được gắn GPS.
2. **Cấu hình Sensor**:
   - Camera Type: `Equidistant Fisheye` (`sensor.type = Metashape.Sensor.Type.Fisheye`).
3. **Align Photos**:
   - Accuracy: `Medium` (`downscale=2`).
   - Generic Preselection: `True`.
   - Reference Preselection: `True` (sử dụng GPS EXIF).
   - Keypoint limit: 40,000 | Tiepoint limit: 4,000.
4. **Build 3D Mesh Model**:
   - Surface Type: `Height Field`.
   - Source Data: `Tie Points` (`Metashape.DataSource.TiePointsData`).
   - Face count: `Medium`.
5. **Build Orthomosaic**:
   - Projection: `WGS 84 / EPSG::4326`.
   - Surface: `Mesh` (`ModelData`).
   - Blending Mode: `Mosaic` (`MosaicBlending`).
6. **Xuất kết quả**:
   - **GeoTIFF (`.tif`)**: LZW compression (`TiffCompressionLZW`), chuẩn tọa độ nhúng `EPSG:4326`.
   - **Flat Image (`.jpg`)**: Chất lượng 100% (`jpeg_quality=100`), Mosaic blending mode.
7. **Trích xuất tọa độ GPS & Upload MinIO**:
   - Đọc trực tiếp từ GeoTIFF xuất ra: trích xuất 4 góc GPS (`north_west`, `north_east`, `south_east`, `south_west`), Bounding Box (`bounds`), kích thước ảnh (`width`, `height`), SHA256 checksum.
   - Upload trực tiếp lên MinIO theo chuẩn đường dẫn:
     - `tenants/{tenant_id}/media/{mission_id}/orthophoto.tif`
     - `tenants/{tenant_id}/media/{mission_id}/map_preview.jpg`
   - Tạo presigned URLs trả về cho client / .NET backend.

---

## 2. Module Cắt Ảnh GeoTIFF (Tiling / Windowed Reading) cho YOLO

Do file GeoTIFF bản đồ có thể nặng 700MB - vài GB, YOLO không thể nạp trực tiếp toàn bộ ảnh vào RAM/VRAM. Module `tiler.py` giải quyết triệt để vấn đề này:

- **Windowed Reading (`rasterio.windows.Window`)**: Chỉ nạp từng ô 640x640 (hoặc 1024x1024) vào bộ nhớ RAM (~1.2MB mỗi ô, tổng RAM server luôn < 200MB).
- **Hỗ trợ 2 chế độ**:
  1. *Grid Tiling toàn bộ ảnh*: Quét đều theo lưới với bước nhảy `stride = tile_size - overlap` (chống mất cành cây ở mép cắt).
  2. *Polygon Masking (Lọc theo vườn)*: Nếu truyền vào GeoJSON viền đa giác vườn, thuật toán kiểm tra giao thoa (`intersects`) và chỉ xuất các ô nằm trong vườn -> Tiết kiệm 80% thời gian chạy YOLO và dung lượng ổ cứng.
- **Tọa độ Affine Transform cho từng mảnh**:
  Mỗi tile xuất ra kèm ma trận chuyển đổi tọa độ GPS (`affine_transform`) và 4 góc GPS:
  ```json
  {
    "tile_index": 0,
    "file_name": "tile_0000_r0_c0.jpg",
    "col_off": 0,
    "row_off": 0,
    "bounds": { "min_lat": 10.4613, "max_lat": 10.4615, "min_lon": 106.4938, "max_lon": 106.4941 },
    "corners": { "north_west": [10.4615, 106.4938], ... },
    "affine_transform": [0.0499, 0.0, 663497.8, 0.0, -0.0499, 1156835.6]
  }
  ```
  Nhờ đó, khi YOLO phát hiện cây tại pixel `(x, y)` trên tile, backend ngay lập tức quy đổi ra tọa độ GPS thực tế `(Latitude, Longitude)`.

---

## 3. API Endpoints

| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/health` | Kiểm tra tình trạng service, MinIO và module Metashape |
| `POST` | `/api/v1/metashape/process` | Tiếp nhận yêu cầu ghép ảnh, tải ảnh từ MinIO, chạy ngầm Metashape |
| `GET` | `/api/v1/metashape/tasks/{task_id}` | Lấy tiến độ (0-100%), stage và kết quả (URL, keys, bounds, corners) |
| `POST` | `/api/v1/metashape/tasks/{task_id}/cancel` | Hủy tiến trình xử lý |
| `POST` | `/api/v1/tiling/slice` | Cắt GeoTIFF thành các mảnh nhỏ cho YOLO kèm tọa độ GPS |

---

## 4. Chạy dịch vụ

### Chạy trực tiếp (Local Python):
```bash
cd backend/services/metashape-service
pip install -r requirements.txt
python main.py
```

### Chạy qua Docker Compose:
Dịch vụ đã được cấu hình sẵn trong file `compose.yaml`:
```bash
docker compose up -d metashape-service
```

### Chạy CLI cắt ảnh độc lập:
```bash
python backend/services/metashape-service/tiler.py \
  --input scratch/test_orthophoto.tif \
  --output-dir scratch/output_tiles \
  --tile-size 640 \
  --overlap 64 \
  --polygon /path/to/farm_boundary.geojson
```
