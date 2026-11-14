# Hướng dẫn test từ Tạo Drone đến Upload Finalize

> **V3 scope notice — 2026-11-10.**
> Historical runtime walkthrough: endpoint names/payloads and paid-before-service assumptions describe an earlier implementation. It is not a v3 runbook; use v3-business-acceptance.md for the approved target, and verify actual runtime before executing old examples.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

Tài liệu này mô tả luồng System Manager chuẩn trên Swagger từ lúc tạo mission đến khi hoàn tất upload và chuyển mission sang `READY_FOR_PROCESSING`.

> `POST .../upload/finalize` hoàn tất **upload của mission**. Đây chưa phải là hoàn tất review/publish Farm Base Map. Với Baseline Mapping, phần review candidate và publish map được ghi ở cuối tài liệu.

## Thông tin dùng trong ví dụ

Base URL: `https://localhost:7080`

Farm và mission đang được trao đổi:

```text
farmId    = 5f98ffce-6ac2-4cab-a445-43b7fd35c88b
missionId = 5f37816a-4e1f-47d8-b831-fc7a63f32e28
```

Thay các ID trên nếu bạn chạy seed tạo dữ liệu mới. Lấy `surveyOrderId` và `droneId` từ dữ liệu seed hoặc kết quả API tương ứng. API có kiểm tra System Manager đã được phân công vào farm.

## Quy tắc khi test

- Dùng token của System Manager được phân công vào farm.
- Sau mỗi thao tác làm thay đổi mission, lấy version mới nhất bằng `GET /api/system-manager/farms/{farmId}/missions/{missionId}/version`.
- Thời gian gửi lên API có kiểu `DateTimeOffset`; timestamp nên ở UTC (`Z`). Ngày `serviceDate`/`baselineDate` được hiểu theo múi giờ Việt Nam.
- Dùng `Idempotency-Key` là UUID mới cho một thao tác mới. Khi retry cùng thao tác/payload, giữ nguyên key.
- Không dùng SQL chỉnh trực tiếp appointment/mission trong luồng test API chuẩn: các cập nhật đó bỏ qua lịch sử, audit và xử lý preflight của ứng dụng.

## 0. Tạo drone

Đăng nhập bằng **SystemAdmin**. API đăng ký drone cấp hệ thống:

```http
POST /api/system/drones
```

Ví dụ cho drone dùng Baseline Mapping và Plant Health:

```json
{
  "code": "DR-TEST-BASELINE-001",
  "name": "Drone test Baseline",
  "model": "Matrice 350 RTK",
  "manufacturer": "DJI",
  "specifications": {
    "capabilities": [
      "BASELINE_MAPPING",
      "PLANT_HEALTH"
    ]
  },
  "serialNumber": "SN-TEST-BASELINE-001",
  "registrationNumber": "REG-TEST-BASELINE-001",
  "registrationDate": "2026-10-30",
  "registrationExpiryDate": "2027-10-30",
  "weightKg": 6.5,
  "notes": "Drone mock dùng kiểm thử mission"
}
```

Code, serial number và registration number phải duy nhất. Drone mới được tạo ở trạng thái `Available`. Lưu `id` từ response làm `droneId` cho bước prepare. Nếu cần kiểm tra drone có sẵn trong khoảng bay và hỗ trợ purpose, dùng:

```http
GET /api/system-manager/farms/{farmId}/drones/available?startAt=<UTC>&endAt=<UTC>&purpose=BaselineMapping
```

Các bước dưới đây giả định tenant owner, System Manager đã được phân công vào farm, farm, survey order, appointment đã xác nhận, tiền cọc và các authorization cần thiết đã có. Nếu thiếu các dữ liệu này, prepare có thể trả order chưa đủ điều kiện.

## 1. Prepare mission

```http
POST /api/system-manager/survey-orders/{surveyOrderId}/missions/prepare
Idempotency-Key: <UUID mới>
```

Ví dụ Baseline Mapping:

```json
{
  "droneId": "<DRONE_ID>",
  "baselineDate": "2026-10-30"
}
```

Ở lần prepare đầu, Survey Order còn yêu cầu Baseline Mapping nên chỉ gửi `baselineDate`; không gửi `serviceDate` cùng lúc. Hai loại ngày bị loại trừ theo trạng thái order hiện tại. Ngày phải phù hợp với appointment đã xác nhận và drone phải khả dụng/có capability phù hợp.

Response có mảng `missions`. Lấy `missionId`, `status`, `version` từ mission có `purpose: "BaselineMapping"` (hoặc purpose dịch vụ tương ứng). Prepare tạo mission `Scheduled` khi appointment đã xác nhận và order đủ điều kiện.

Nếu đã có mission, lấy danh sách tại:

```http
GET /api/system-manager/farms/{farmId}/missions
```

Trường `id` trong từng item là Mission ID.

### Prepare mission dịch vụ sau Baseline Mapping

Sau khi baseline flight hoàn tất, Farm Base Map được validate/publish, số trụ và giá được xác nhận, appointment `PAID_SERVICE` được Tenant Owner xác nhận và thanh toán đủ, order mới sẵn sàng cho mission dịch vụ. Gọi lại cùng endpoint prepare với `serviceDate` (không gửi `baselineDate`):

```json
{
  "droneId": "<DRONE_ID>",
  "serviceDate": "<NGAY_BAY_DICH_VU-YYYY-MM-DD>"
}
```

Response có mission dịch vụ với `purpose` như `PlantHealth`; mission BaselineMapping cũ có thể cũng xuất hiện trong danh sách vì endpoint trả toàn bộ mission set của order. Dùng đúng mission ID có purpose dịch vụ khi tiếp tục preflight/start/complete/upload/finalize cho chuyến bay dịch vụ.

## 2. Lấy checklist đang áp dụng

```http
GET /api/system-manager/farms/{farmId}/missions/{missionId}/preflight/checklist
```

Ghi lại `definitionId`, `versionNumber` và toàn bộ `items[].key` từ response. Không tự đặt key khác với response.

Checklist mẫu đã có trong môi trường của bạn:

```json
{
  "definitionId": "c4780f34-06dc-4e5a-ad02-b7168db0c9bd",
  "code": "DRONE_PRE_FLIGHT",
  "versionNumber": 1,
  "items": [
    { "key": "BATTERY_CHECKED", "prompt": "Pin drone đã được kiểm tra và đủ dung lượng?", "required": true },
    { "key": "PROPELLERS_CHECKED", "prompt": "Cánh quạt đã được kiểm tra và lắp chắc chắn?", "required": true },
    { "key": "GPS_SIGNAL_CHECKED", "prompt": "Tín hiệu GPS và vị trí Home Point đã được xác nhận?", "required": true },
    { "key": "FLIGHT_AREA_CLEAR", "prompt": "Khu vực bay an toàn và không có chướng ngại vật?", "required": true }
  ]
}
```

## 3. Gửi kết quả preflight

Lấy version mission mới nhất trước khi gửi:

```http
GET /api/system-manager/farms/{farmId}/missions/{missionId}/version
```

Sau đó gọi:

```http
POST /api/system-manager/farms/{farmId}/missions/{missionId}/preflight
Idempotency-Key: <UUID mới>
```

```json
{
  "expectedMissionVersion": <VERSION_MOI_NHAT>,
  "checklistDefinitionId": "c4780f34-06dc-4e5a-ad02-b7168db0c9bd",
  "checklistVersion": 1,
  "answers": {
    "BATTERY_CHECKED": true,
    "PROPELLERS_CHECKED": true,
    "GPS_SIGNAL_CHECKED": true,
    "FLIGHT_AREA_CLEAR": true
  },
  "suitableForFlight": true,
  "notes": "Đã kiểm tra trước chuyến bay.",
  "deviceCompletedAt": "2026-10-30T06:00:00Z"
}
```

`answers` phải là JSON object. Khi `suitableForFlight` là `true`, không được có câu trả lời `false`. Chỉ xác nhận các điều kiện thực sự đã kiểm tra đạt.

## 4. Bắt đầu chuyến bay

Kiểm tra mission vẫn `Scheduled`, giờ hiện tại đang nằm trong cả lịch mission lẫn appointment đã xác nhận, và preflight còn hiệu lực. Lấy version mới nhất rồi gọi:

```http
POST /api/system-manager/farms/{farmId}/missions/{missionId}/start
```

```json
{
  "expectedMissionVersion": <VERSION_MOI_NHAT>
}
```

Thành công sẽ chuyển mission sang `InFlight`.

## 5. Hoàn thành chuyến bay

Sau khi bay xong, lấy version mới rồi gọi:

```http
POST /api/system-manager/farms/{farmId}/missions/{missionId}/complete-flight
```

```json
{
  "expectedMissionVersion": <VERSION_MOI_NHAT>
}
```

Mission chuyển sang `FlightCompleted`. Ghi lại `StartedAt`/`EndedAt` từ mission response/details; telemetry gửi sau phải nằm trong khoảng này.

## 6. Upload ảnh/video

```http
POST /api/missions/{missionId}/farms/{farmId}/media
Content-Type: multipart/form-data
```

Trong Swagger chọn trường `Files` và thêm ít nhất một file ảnh/video hợp lệ. Endpoint này nhận tối đa 20 file mỗi lần; tổng dung lượng request tối đa 5 GiB. Mission phải đã kết thúc chuyến bay.

## 7. Import telemetry

Telemetry cần tối thiểu 2 điểm có timestamp UTC, nằm trong thời gian `StartedAt` đến `EndedAt`. Mỗi mission chỉ có một telemetry import hiện hành; dùng dữ liệu flight thực tế khi test.

```http
POST /api/missions/{missionId}/farms/{farmId}/telemetry/imports
Idempotency-Key: <UUID mới>
```

```json
{
  "sourceFileName": "flight-log.json",
  "sourceChecksum": "<SHA256_HEX_64_KY_TU>",
  "expectedMissionVersion": <VERSION_MOI_NHAT>,
  "segments": [
    {
      "segmentIndex": 0,
      "points": [
        {
          "sequenceNumber": 1,
          "recordedAt": "<UTC_TRONG_KHOANG_STARTED_AT_END_AT>",
          "longitude": 106.49405,
          "latitude": 10.46165,
          "altitudeM": 30,
          "altitudeReference": null,
          "headingDeg": 0,
          "speedMps": 2,
          "horizontalAccuracyM": 1
        },
        {
          "sequenceNumber": 2,
          "recordedAt": "<UTC_SAU_DIEM_1_VA_KHONG_VUOT_END_AT>",
          "longitude": 106.49410,
          "latitude": 10.46170,
          "altitudeM": 30,
          "altitudeReference": null,
          "headingDeg": 0,
          "speedMps": 2,
          "horizontalAccuracyM": 1
        }
      ]
    }
  ]
}
```

Thay hai timestamp bằng thời điểm bay thực tế. Không gửi nguyên các placeholder có dấu `<...>`. `sourceChecksum` phải là chuỗi SHA-256 hex dài 64 ký tự. Import đầu tiên chuyển mission sang `Uploading`.

## 8. Kiểm tra readiness trước finalize

```http
GET /api/missions/{missionId}/farms/{farmId}/upload/readiness
```

Chỉ gọi finalize khi response có:

- `canFinalize: true`
- `hasActiveUploadSessions: false`
- Có ít nhất một media hợp lệ
- Có ít nhất hai telemetry point
- Không còn `blockers`

Nếu có blocker, xử lý dữ liệu còn thiếu rồi gọi readiness lại.

## 9. Finalize upload mission

Lấy version mission mới nhất, sau đó gọi:

```http
POST /api/missions/{missionId}/farms/{farmId}/upload/finalize
```

```json
{
  "expectedMissionVersion": <VERSION_MOI_NHAT>
}
```

Kết quả mong đợi: `status: "ReadyForProcessing"`. Backend sẽ đưa mission vào hàng đợi xử lý. Đây là trạng thái **upload đã finalize**, không phải trạng thái nghiệp vụ `Completed` của đơn hàng.

## 10. Tiếp tục riêng cho Baseline Mapping (nếu cần publish Farm Base Map)

Sau khi xử lý mapping tạo draft/candidate, dùng các API dưới đây; `draftId` lấy từ response tạo/lấy draft:

```http
GET  /api/system-manager/farms/{farmId}/base-map-drafts?surveyOrderId={surveyOrderId}
GET  /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}
POST /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}/candidates/{candidateId}/review
POST /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}/candidates/review-batch
POST /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}/validate
POST /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}/publish
```

Review candidate theo đúng ID trong response draft. Chỉ publish khi `validate` xác nhận batch hợp lệ. Publish Farm Base Map là bước riêng sau upload finalize; publish map sẽ phát hành bản đồ chính thức và cập nhật Survey Order.

## Các lỗi thường gặp

| Lỗi | Điều cần kiểm tra |
| --- | --- |
| `AppointmentWindowClosed` | Ngày mission nằm ngoài appointment đang `Confirmed`; đổi lịch bằng API và để Tenant Owner xác nhận. |
| `ScheduleWindowClosed` | Giờ hiện tại nằm ngoài `scheduled_at`/`scheduled_end_at` của mission. |
| `PreflightRequired` hoặc `PreflightStale` | Hoàn thành lại checklist sau reschedule hoặc sau khi checklist definition đổi version. |
| `PendingModelChangesWarning` / `400` | Đọc response body/log server; không suy đoán nguyên nhân chỉ từ traceId. |
| `canFinalize: false` | Xử lý từng blocker trong upload readiness; cần media, telemetry và không có session đang upload. |

