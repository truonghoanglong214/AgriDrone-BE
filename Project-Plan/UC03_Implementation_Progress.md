# BE2-UC03 — Mission Media & Telemetry

## 1. Thông tin chung

- **Dự án**: AgriDrone — Smart Dragon Fruit Farm Management System Using Drone and AI
- **Mã đồ án**: FA26SE218
- **Use case**: BE2-UC03 — Mission Media & Telemetry
- **Ngày cập nhật**: 09/09/2026
- **Nhánh triển khai**: `ThanhND`
- **Trạng thái code nghiệp vụ**: `Hoàn thành theo scope đã chốt`
- **Tiến độ code không tính test**: `100%`
- **Trạng thái Definition of Done**: `Chưa hoàn thành kiểm thử riêng UC03 và smoke test đầu-cuối`

Tài liệu này phản ánh trạng thái source và database local đã được kiểm tra ngày 09/09/2026. Việc code nghiệp vụ đạt 100% không đồng nghĩa UC03 đã đạt đầy đủ Definition of Done, vì vẫn còn các bài kiểm thử riêng và smoke test thực tế cần thực hiện.

## 2. Phạm vi nghiệp vụ đã chốt

Drone không kết nối trực tiếp với Backend. Dữ liệu được lưu trên thẻ nhớ và được người dùng chuyển sang máy tính sau chuyến bay.

Luồng UC03:

```text
Drone thực hiện chuyến bay
  -> Drone lưu ảnh/video/log vào thẻ nhớ
  -> Người dùng sao chép dữ liệu sang máy tính
  -> Frontend yêu cầu Backend tạo upload session
  -> Frontend PUT media trực tiếp lên MinIO
  -> Frontend yêu cầu Backend complete upload
  -> Backend xác minh object, size, MIME và SHA256
  -> Backend tạo MediaAsset và MissionMedia
  -> Frontend gửi telemetry JSON đã chuẩn hóa
  -> Backend validate và lưu telemetry nguyên tử
  -> Backend dựng actual flight route SRID 4326
  -> Frontend yêu cầu finalize
  -> Backend tự kiểm tra dữ liệu bắt buộc
  -> Mission chuyển sang ReadyForProcessing
```

UC03 không bao gồm:

- Drone truyền dữ liệu trực tiếp về Backend.
- Parser log riêng của DJI hoặc hãng Drone khác.
- Thay đổi Farm/Zone hoặc truy vấn trực tiếp database của Farm module.
- AI processing, plant mapping hoặc health review sau khi Mission sẵn sàng.

## 3. Kiến trúc lưu trữ

| Storage | Dữ liệu | Mục đích |
|---|---|---|
| MinIO | Ảnh, video và raw log nếu cần | Lưu binary object dung lượng lớn |
| PostgreSQL | Upload session, media metadata, MissionMedia, telemetry import và telemetry point | Ràng buộc nghiệp vụ, truy vấn, idempotency, concurrency và audit |
| Redis | Không lưu media hoặc telemetry UC03 | Chỉ dùng cho caching/hạ tầng dùng chung nếu cần |

Media không được lưu trực tiếp trong PostgreSQL và không đi xuyên qua Backend API. Backend cấp presigned PUT URL để Frontend upload trực tiếp lên MinIO.

## 4. Trạng thái MinIO

MinIO local đã được thiết lập bằng Docker:

```text
Container: agridrone-minio
API port: 9000
Console port: 9001
Bucket: agridrone-media
Bucket access: PRIVATE
Persistent volume: /home/thanh/agridrone/minio-data -> /data
```

Các kiểm tra đã xác nhận:

```text
GET http://localhost:9000/minio/health/live -> HTTP 200
GET http://localhost:5080/health/minio      -> HTTP 200 Healthy
```

Credential chỉ nằm trong cấu hình development local và không được ghi vào tài liệu hoặc commit.

## 5. API UC03 đã triển khai

### 5.1. Tạo upload session

```http
POST /api/farms/{farmId}/missions/{missionId}/media/upload-sessions
```

Chức năng:

- Kiểm tra Mission theo Tenant/Farm/Mission scope.
- Kiểm tra Mission version.
- Tạo stable object key và presigned PUT URL.
- Tạo `MediaUploadSession`.
- Chuyển Mission từ `FlightCompleted` hoặc `UploadFailed` sang `Uploading` khi cần.
- Bảo vệ idempotency bằng `OperationId`.
- Ghi audit tạo upload session và thay đổi trạng thái Mission.

### 5.2. Frontend PUT trực tiếp lên MinIO

Frontend sử dụng presigned URL nhận từ Backend để upload binary file. Backend không buffer toàn bộ ảnh/video trong RAM.

### 5.3. Complete upload session

```http
POST /api/farms/{farmId}/missions/{missionId}/media/upload-sessions/{uploadSessionId}/complete
```

Chức năng:

- Kiểm tra session đúng Tenant/Farm/Mission.
- Kiểm tra object tồn tại trong MinIO.
- So sánh dung lượng thực tế.
- So sánh MIME type.
- Đọc object và tính SHA256 phía Backend.
- So sánh checksum thực tế với checksum khai báo.
- Tạo `MediaAsset` và liên kết `MissionMedia`.
- Chuyển session sang `Completed`, `Failed` hoặc `Expired`.
- Chuyển Mission sang `UploadFailed` khi verification thất bại.
- Hỗ trợ retry completion đã thành công.
- Ghi audit thành công, thất bại, hết hạn và Mission upload failure.

### 5.4. Import telemetry

```http
POST /api/farms/{farmId}/missions/{missionId}/telemetry/imports
```

Chức năng:

- Nhận telemetry JSON chuẩn hóa.
- Validate toàn batch trước khi lưu.
- Giới hạn tối đa 50.000 points cho một request.
- Kiểm tra sequence và timestamp tăng nghiêm ngặt.
- Kiểm tra timestamp nằm trong khoảng `StartedAt` đến `EndedAt` của Mission.
- Kiểm tra longitude, latitude, altitude, heading, speed và accuracy.
- Tạo `MissionTelemetryPoint` bằng domain factory.
- Lưu telemetry import và toàn bộ points trong cùng transaction.
- Bảo vệ business idempotency bằng `OperationId` và checksum.
- Ngăn một Mission bị import telemetry nhiều lần.
- Dựng actual flight route theo thứ tự sequence với SRID 4326.
- Ghi audit `IMPORT_MISSION_TELEMETRY` mà không lưu toàn bộ payload.

### 5.5. Finalize Mission upload

```http
POST /api/farms/{farmId}/missions/{missionId}/upload/finalize
```

Backend không nhận cờ `isReady` từ client. Handler tự truy vấn và kiểm tra readiness.

Điều kiện finalize:

- Mission đang ở `Uploading`.
- `ExpectedMissionVersion` khớp version hiện tại.
- Không còn upload session `Verifying`.
- Không còn session `Pending` chưa hết hạn.
- Mapping Mission có ít nhất một `RawImage` đã xác minh.
- HealthInspection Mission có ít nhất một `RawImage` hoặc `RawVideo` đã xác minh.
- Có telemetry import hợp lệ.
- Có ít nhất hai telemetry points.
- Số points khai báo trong import khớp số points thực tế.
- Có actual flight route không rỗng, ít nhất hai points và SRID 4326.

Khi đủ điều kiện:

```text
MissionStatus: Uploading -> ReadyForProcessing
ProcessingStatus: Uploaded
```

Nếu thiếu dữ liệu, Backend trả conflict và giữ Mission ở `Uploading`. Lỗi verification nghiêm trọng đã được xử lý ở bước complete upload và chuyển Mission sang `UploadFailed`.

## 6. Authorization và tenant isolation

Tất cả API tenant UC03:

- Yêu cầu người dùng đăng nhập.
- Lấy `TenantId` từ JWT/current tenant context.
- Không nhận `TenantId` trong Swagger request body.
- Kiểm tra resource authorization bằng `FarmAccessTarget`.
- Sử dụng policy `AccessAuthorizationPolicies.FarmManage`.
- Repository/query luôn lọc Tenant/Farm/Mission để tránh truy cập chéo tenant.

`FarmManage` là resource-based policy nên controller gọi `IAuthorizationService.AuthorizeAsync()` cùng `FarmAccessTarget`, thay vì chỉ đặt policy attribute mà không cung cấp resource.

## 7. Idempotency và concurrency

### 7.1. Media upload

- `OperationId` đại diện cho một lần tạo upload session.
- Cùng operation và cùng payload trả lại session cũ.
- Cùng operation nhưng payload khác trả conflict.
- Unique constraint bảo vệ `(TenantId, MissionId, OperationId)`.
- `MediaUploadSession.Version` dùng PostgreSQL row version.

### 7.2. Telemetry import

- `OperationId` đại diện cho một lần import telemetry.
- Cùng operation, file name, checksum và point count trả lại kết quả cũ.
- Cùng operation nhưng payload metadata khác trả conflict.
- Mỗi Mission chỉ được có một telemetry import trong scope hiện tại.
- Unique index và transaction bảo vệ import đồng thời.

### 7.3. Mission

- Client gửi `ExpectedMissionVersion` khi tạo upload, import telemetry và finalize.
- Handler kiểm tra optimistic concurrency trước khi thay đổi Mission.
- `MissionConcurrencyException` được map thành conflict có thể retry.

## 8. Audit đã triển khai

Các action UC03 đang được ghi audit:

```text
CREATE_UPLOAD_SESSION
START_UPLOADING
COMPLETE_UPLOAD_VERIFICATION
FAIL_UPLOAD_VERIFICATION
EXPIRE_UPLOAD_SESSION
FAIL_MISSION_UPLOAD
IMPORT_MISSION_TELEMETRY
FINALIZE_MISSION_UPLOAD
```

Audit không chứa access key, secret key, presigned URL, toàn bộ telemetry payload hoặc binary media. Audit và thay đổi nghiệp vụ được lưu trong cùng Unit of Work/transaction.

## 9. Thành phần đã triển khai

### 9.1. Domain

```text
Domain/Media/MediaUploadSession.cs
Domain/Media/MediaUploadSessionStatus.cs
Domain/Media/MediaAsset.cs
Domain/Media/MissionMedia.cs
Domain/Telemetry/MissionTelemetryPoint.cs
Domain/Telemetry/MissionTelemetryImport.cs
Domain/Telemetry/TelemetryRouteFactory.cs
Domain/Telemetry/AltitudeReference.cs
Domain/Missions/DroneMission.cs
```

### 9.2. Application

```text
Application/Abstractions/Media/
Application/Abstractions/Telemetry/
Application/Features/Media/CreateUploadSession/
Application/Features/Media/CompleteUploadSession/
Application/Features/Media/FinalizeMissionUpload/
Application/Features/Telemetry/ImportTelemetry/
```

Folder telemetry đã được chuẩn hóa theo feature-based structure. File lỗi telemetry đã được đổi đúng thành `ImportTelemetryError.cs`.

### 9.3. Infrastructure và integrations

```text
Infrastructure/Repositories/MediaUploadSessionRepository.cs
Infrastructure/Repositories/MissionMediaRepository.cs
Infrastructure/Repositories/MissionTelemetryRepository.cs
Infrastructure/Queries/MissionUploadReadinessQueries.cs
Infrastructure/Persistence/Configurations/MediaUploadSessionConfiguration.cs
Infrastructure/Persistence/Configurations/MissionTelemetryImportConfiguration.cs
Integrations/AgriDrone.Integrations.Media/MinioObjectStorage.cs
Integrations/AgriDrone.Integrations.Media/Sha256ChecksumCalculator.cs
```

### 9.4. API

```text
Controllers/MissionMediaController.cs
Controllers/MissionTelemetryController.cs
Controllers/MissionUploadController.cs
Contracts/Missions/*MissionMediaUploadSession*.cs
Contracts/Missions/*MissionTelemetry*.cs
Contracts/Missions/FinalizeMissionUploadRequest.cs
Contracts/Missions/FinalizeMissionUploadResponse.cs
```

API DTO chỉ nằm trong API layer. Controller chỉ xác thực, phân quyền, map request/response và gửi command qua MediatR.

## 10. Database migrations

Hai migration UC03 đã được tạo và apply vào database local:

```text
20260907145933_AddMediaUploadSessions
20260909152909_AddMissionTelemetryImports
```

Migration telemetry bao gồm:

- Bảng `mission.mission_telemetry_imports`.
- Check constraint cho point count, checksum và time range.
- Foreign key bảo vệ Mission/Farm và Mission/Tenant.
- Unique index cho Mission và import operation.
- PostgreSQL enum label `RELATIVE_TO_TAKEOFF` trong `system.altitude_reference`.

Do PostgreSQL không hỗ trợ xóa enum label an toàn, `Down()` của migration telemetry chỉ xóa bảng import và chủ động giữ lại `RELATIVE_TO_TAKEOFF`.

EF đã được kiểm tra bằng `has-pending-model-changes` và trả:

```text
No changes have been made to the model since the last migration.
```

## 11. Kết quả xác minh source hiện tại

Build đã chạy:

```text
dotnet build backend/src/AgriDrone.Api/AgriDrone.Api.csproj --no-restore -v:minimal
```

Kết quả:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Unit test hiện có đã chạy:

```text
dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj --no-build -v:minimal
```

Kết quả:

```text
Passed: 75
Failed: 0
Skipped: 0
Total: 75
```

`git diff --check` không phát hiện lỗi whitespace. Chỉ có cảnh báo chuyển đổi line ending LF/CRLF ở một số file đã tồn tại.

Lưu ý: 75 tests là test nền hiện có. Kết quả này chứng minh code mới không làm hỏng test cũ, nhưng chưa thay thế test riêng cho UC03.

## 12. Phần còn lại để đạt Definition of Done

Code nghiệp vụ theo scope đã chốt đã hoàn thành. Các công việc còn lại thuộc kiểm thử và production hardening:

### 12.1. Unit tests UC03

- Domain transition của upload session.
- Telemetry point factory và route factory.
- Validator create/complete/import/finalize.
- Handler happy path, failure, idempotency và concurrency.
- Finalize thiếu media, thiếu telemetry, route sai hoặc còn active session.

### 12.2. Persistence/integration tests

- Unique constraints của upload và telemetry import.
- Transaction rollback khi telemetry batch lỗi.
- Mission/Farm/Tenant foreign keys.
- PostgreSQL geometry Point/LineString SRID 4326.
- MinIO presigned PUT, stat, read, checksum và private bucket.

### 12.3. API authorization tests

- `401` khi chưa đăng nhập.
- `403` khi không có `FarmManage`.
- Không truy cập chéo tenant/farm/mission.
- Request tenant user không nhận `TenantId` từ người dùng.

### 12.4. Smoke test đầu-cuối

```text
Mission FlightCompleted
  -> CreateUploadSession
  -> PUT media lên MinIO
  -> CompleteUploadSession
  -> ImportTelemetry
  -> FinalizeMissionUpload
  -> Mission ReadyForProcessing
  -> kiểm tra MediaAsset/MissionMedia/Telemetry/FlightRoute/Audit trong PostgreSQL
```

### 12.5. Production hardening sau MVP

- Cleanup session hết hạn và object mồ côi.
- Abort upload session nếu nghiệp vụ cần.
- Giới hạn request body/rate limit cho telemetry lớn.
- Batch insert hoặc PostgreSQL COPY nếu dữ liệu thực tế vượt khả năng EF `AddRange`.
- Parser adapter khi có log mẫu thật từ Drone.

## 13. Definition of Done

| Tiêu chí | Trạng thái |
|---|---|
| MinIO và private bucket hoạt động | Hoàn thành |
| Health check MinIO | Hoàn thành |
| CreateUploadSession và idempotency | Đã triển khai |
| Direct PUT lên MinIO | Đã hỗ trợ |
| Complete verification size/MIME/SHA256 | Đã triển khai |
| MediaAsset và MissionMedia | Đã triển khai |
| Telemetry validation và atomic persistence | Đã triển khai |
| Telemetry business idempotency | Đã triển khai |
| Actual flight route SRID 4326 | Đã triển khai |
| Finalize tự kiểm tra mandatory input | Đã triển khai |
| ReadyForProcessing/UploadFailed transitions | Đã triển khai |
| Tenant/Farm/Mission isolation và FarmManage | Đã triển khai |
| Audit và optimistic concurrency | Đã triển khai |
| Database migrations local | Hoàn thành |
| Build pass | Hoàn thành |
| Test nền hiện có | 75/75 pass |
| Test riêng UC03 | Chưa thực hiện đầy đủ |
| Smoke test PostgreSQL + MinIO | Chưa thực hiện |

## 14. Kết luận tiến độ

Tại thời điểm cập nhật:

```text
Code nghiệp vụ UC03, không tính test: 100%
Definition of Done toàn bộ UC03: chưa hoàn thành
```

Không còn blocker về source compilation hoặc database migration. Bước tiếp theo là kiểm thử API theo đúng thứ tự vận hành thực tế, ưu tiên smoke test happy path trước, sau đó bổ sung unit/integration/authorization tests cho các nhánh lỗi và concurrency.
