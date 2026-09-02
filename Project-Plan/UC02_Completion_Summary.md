# BE2-UC02 — Mission Lifecycle

## 1. Thông tin chung

- **Dự án**: AgriDrone — Smart Dragon Fruit Farm Management System Using Drone and AI
- **Mã đồ án**: FA26SE218
- **Use case**: BE2-UC02 — Mission Lifecycle
- **Ngày kiểm tra**: 01/09/2026
- **Ngày tái kiểm tra gần nhất**: 01/09/2026
- **Trạng thái**: `Done theo phạm vi đã thống nhất`
- **Phạm vi đánh giá**: Domain, Application, API, phân quyền, cô lập dữ liệu, persistence, DI, migration và build. Theo yêu cầu của người dùng, không viết hoặc chạy unit test, integration test hay contract test. API end-to-end UC02 chưa được xác nhận thành công trên dữ liệu thật do môi trường local chưa có đủ Farm/Zone/Farm Manager và tiến trình debug bị gián đoạn bởi RabbitMQ.

## 2. Nghiệp vụ UC02

UC02 quản lý toàn bộ vòng đời của một chuyến bay Drone (`DroneMission`) từ lúc Farm Manager tạo kế hoạch đến khi kết quả được duyệt hoàn tất.

Mission luôn thuộc đúng một Tenant, một Farm và một Zone. Khi tạo Mission, hệ thống kiểm tra Farm/Zone qua contract của BE1 thay vì đọc trực tiếp database của module Farms.

Vòng đời đầy đủ:

```text
Draft
  -> Scheduled
  -> InFlight
  -> FlightCompleted
  -> Uploading
  -> ReadyForProcessing
  -> Processing
  -> AwaitingReview
  -> Completed
```

Các nhánh lỗi và hủy:

```text
Draft/Scheduled -> Cancelled
InFlight        -> FlightFailed
Uploading       -> UploadFailed -> retry Uploading
Processing      -> ProcessingFailed -> retry Processing
```

Ba mốc sau được tách riêng về mặt nghiệp vụ:

```text
FlightCompleted != AI job Completed != Mission Completed
```

Vì vậy, AI xử lý xong không tự động làm Mission chuyển thẳng sang `Completed`. Mission còn phải qua `AwaitingReview` và được xác nhận theo luồng review.

## 3. Quy tắc nghiệp vụ đã triển khai

- Mission được tạo ở trạng thái `Draft`.
- Mission chỉ được lập cho Farm và Zone đang hoạt động, thuộc đúng Tenant/Farm hiện tại.
- Mission `HealthInspection` bắt buộc có bản đồ Zone đã được BE1 publish và xác nhận.
- Mỗi Mission chỉ gắn với một Zone.
- Sau khi Mission đã `InFlight`, không thể thay đổi Zone hoặc loại Mission.
- Chỉ các transition hợp lệ từ trạng thái hiện tại mới được chấp nhận.
- Thao tác gửi lại đúng trạng thái được xử lý theo hướng idempotent, không ghi audit trùng.
- Lịch Mission dùng khoảng thời gian nửa kín `[StartAt, EndAt)`.
- Một Drone không được có hai Mission `Scheduled` hoặc `InFlight` giao lịch.
- Mission code không được trùng trong cùng Farm.
- Dữ liệu Mission được lọc đồng thời theo `TenantId`, `FarmId` và `MissionId` để tránh truy cập chéo Farm.

## 4. API và phân quyền

Controller:

```text
backend/src/AgriDrone.Api/Controllers/MissionsController.cs
```

Các API UC02:

| API | Mục đích |
|---|---|
| `POST /api/farms/{farmId}/missions` | Tạo Mission ở trạng thái Draft |
| `PATCH /api/farms/{farmId}/missions/{missionId}/schedule` | Lập hoặc cập nhật lịch khi Mission còn cho phép |
| `PATCH /api/farms/{farmId}/missions/{missionId}/status` | Thực hiện transition do Farm Manager điều khiển |
| `GET /api/farms/{farmId}/missions/{missionId}` | Xem chi tiết Mission |

Tất cả endpoint dùng resource-based authorization `FarmManager` với `FarmAccessTarget(TenantId, FarmId)`. `TenantId` lấy từ tenant context; `FarmId` lấy từ route và tiếp tục được dùng trong query/repository.

API transition dành cho người dùng hiện hỗ trợ các hành động thuộc giai đoạn bay:

```text
InFlight
FlightCompleted
FlightFailed
Cancelled
```

Các transition Uploading, Processing và Review đã có trong Domain để các use case media, AI và review tiếp theo gọi đúng state machine; chúng không được mở thành API tùy ý cho Farm Manager trong UC02.

## 5. Tích hợp với BE1

Contract dùng để kiểm tra Farm, Zone và published map:

```text
backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Farms/IMissionPlanningReferenceQuery.cs
```

Implementation thuộc module Farms:

```text
backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Queries/MissionPlanningReferenceQuery.cs
```

Đăng ký DI:

```text
backend/src/Modules/AgriDrone.Modules.Farms/DependencyInjection.cs
```

Cách này giữ quyền sở hữu dữ liệu Farm/Zone ở BE1 và chỉ công bố dữ liệu cần thiết cho UC02 qua integration contract.

## 6. Domain và state machine

Các file chính:

```text
backend/src/Modules/AgriDrone.Modules.Missions/Domain/Missions/
├── DroneMission.cs
├── MissionStatus.cs
├── MissionType.cs
├── MissionHealthReviewState.cs
├── ProcessingStatus.cs
└── IDroneMissionRepository.cs
```

`DroneMission.cs` là nơi bảo vệ state machine. Các method domain chính gồm:

```text
Schedule
StartFlight
CompleteFlight
FailFlight
Cancel
StartUploading
FailUploading
MarkReadyForProcessing
StartProcessing
FailProcessing
RetryProcessing
MarkAwaitingReview
ApplyPublishedZoneMap
ApplyHealthReviewState
```

`MissionStatus.cs` có đủ 13 trạng thái:

```text
Draft, Scheduled, InFlight, FlightCompleted,
Uploading, ReadyForProcessing, Processing, AwaitingReview,
Completed, Cancelled, FlightFailed, UploadFailed, ProcessingFailed
```

## 7. Application

Các feature UC02:

```text
backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Missions/
├── CreateMission/
│   ├── CreateMissionCommand.cs
│   ├── CreateMissionCommandHandler.cs
│   └── CreateMissionCommandValidator.cs
├── ScheduleMission/
│   ├── ScheduleMissionCommand.cs
│   ├── ScheduleMissionCommandHandler.cs
│   └── ScheduleMissionCommandValidator.cs
├── TransitionMission/
│   ├── TransitionMissionCommand.cs
│   ├── TransitionMissionCommandHandler.cs
│   └── TransitionMissionCommandValidator.cs
├── GetMissionDetails/
│   ├── GetMissionDetailsQuery.cs
│   ├── GetMissionDetailsQueryHandler.cs
│   └── GetMissionDetailsQueryValidator.cs
├── MissionResponse.cs
└── MissionResponseMapper.cs
```

Application abstractions và lỗi persistence:

```text
backend/src/Modules/AgriDrone.Modules.Missions/Application/Abstractions/Missions/
├── IMissionQueries.cs
├── IMissionsUnitOfWork.cs
├── MissionErrors.cs
├── MissionConcurrencyException.cs
├── MissionScheduleConflictException.cs
└── MissionCodeConflictException.cs
```

Các handler thực hiện validation, kiểm tra reference BE1, gọi domain method, ghi audit dùng chung và lưu qua Unit of Work.

## 8. API contracts

```text
backend/src/AgriDrone.Api/Contracts/Missions/
├── CreateMissionRequest.cs
├── ScheduleMissionRequest.cs
└── TransitionMissionRequest.cs
```

Request DTO chỉ nằm ở API. Application nhận command/query riêng, không phụ thuộc DTO của HTTP layer.

## 9. Persistence, concurrency và DI

Các file chính:

```text
backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/
├── Persistence/MissionsDbContext.cs
├── Persistence/Configurations/DroneMissionConfiguration.cs
├── Repositories/DroneMissionRepository.cs
└── Queries/MissionQueries.cs

backend/src/Modules/AgriDrone.Modules.Missions/DependencyInjection.cs
```

UC02 dùng optimistic concurrency thông qua cột `Version` được map bằng `IsRowVersion()`. Khi hai request cùng cập nhật một Mission, `MissionsDbContext` chuyển lỗi concurrency của EF Core thành `MissionConcurrencyException` để Application trả lỗi nghiệp vụ phù hợp.

Database còn bảo vệ:

- Mission code unique trong phạm vi Farm.
- Thời gian kết thúc phải lớn hơn thời gian bắt đầu.
- Không cho lịch `Scheduled`/`InFlight` của cùng Drone bị giao nhau.
- Các trường source map/preflight bắt buộc theo đúng trạng thái và loại Mission.

DI đã đăng ký `IDroneMissionRepository`, `IMissionQueries` và contract tra cứu BE1. Lỗi khởi động trước đây liên quan `IFarmQueries` không còn là blocker; log runtime xác nhận application đã khởi động thành công.

## 10. Audit dùng chung với BE1

UC02 không tạo audit entity hoặc audit table riêng cho Mission/Drone.

Các command tạo, lập lịch và chuyển trạng thái ghi vào cơ chế audit dùng chung của hệ thống, cùng hướng triển khai với BE1/UC01. Các file audit riêng cũ của module Missions đã được loại bỏ và bảng cũ được migration xóa sau khi chuyển dữ liệu cần giữ sang shared audit.

## 11. Migration và trạng thái database

Migration UC02:

```text
backend/src/BuildingBlocks/AgriDrone.Database/Migrations/
├── 20260901054425_CompleteMissionLifecycleUc02AndMigrateDroneAudit.cs
├── 20260901054425_CompleteMissionLifecycleUc02AndMigrateDroneAudit.Designer.cs
└── AgriDroneDbContextModelSnapshot.cs
```

Migration thực hiện:

- Chuẩn hóa enum Mission thành đủ 13 trạng thái.
- Chuyển ý nghĩa dữ liệu trạng thái cũ sang lifecycle mới.
- Bắt buộc Mission có Zone.
- Bổ sung source map, preflight và concurrency version.
- Backfill published map cho dữ liệu HealthInspection cũ khi cần.
- Di chuyển audit cũ sang shared audit và xóa `mission.drone_status_changes`.
- Bổ sung/cập nhật check constraint và exclusion constraint.

Kết quả kiểm tra database local:

- Migration `20260901054425_CompleteMissionLifecycleUc02AndMigrateDroneAudit` đã có trong bảng lịch sử migration.
- Database có đủ 13 enum label.
- Không còn bảng audit riêng `mission.drone_status_changes`.
- Không còn HealthInspection thiếu confirmed map sau migration.
- Các constraint preflight, source map và chống giao lịch đang tồn tại.
- Runtime báo database đã up to date, không có migration chờ áp dụng.

## 12. Kết quả xác minh

Build đã chạy:

```text
dotnet build AgriDrone.sln --no-restore
```

Kết quả:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Log runtime xác nhận API đã listen và application đã start. Các dòng lặp lại trong log là background outbox polling và retry kết nối RabbitMQ tại `127.0.0.1:5672`; đây là thiếu dependency môi trường, không phải vòng lặp state machine, lỗi migration hoặc lỗi UC02.

### 12.1. Kết quả tái kiểm tra gần nhất

Build được chạy lại bằng:

```text
dotnet build AgriDrone.sln --no-restore -v:minimal
```

Kết quả thực tế:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
Time Elapsed 00:00:48.64
```

Database local được đối chiếu lại và xác nhận:

- Migration `20260901054425_CompleteMissionLifecycleUc02AndMigrateDroneAudit` xuất hiện đúng một lần trong migration history.
- Enum `system.mission_status` có đủ 13 trạng thái UC02.
- `mission.drone_status_changes` không còn tồn tại.
- Constraint `ck_drone_missions_source_map` đang tồn tại.
- Constraint `ck_drone_missions_preflight_confirmation` đang tồn tại.
- Exclusion constraint `ex_drone_missions_no_schedule_overlap` dùng khoảng `[)` và áp dụng cho `SCHEDULED`/`IN_FLIGHT`.
- Foreign key bảo vệ quan hệ cùng Tenant, cùng Farm và đúng Zone đang tồn tại.

Smoke test runtime đã xác nhận được phần nền UC01 gồm tạo Tenant, đăng ký Drone, chống trùng Drone code và chuyển Drone sang `Maintenance`. Luồng API UC02 chưa chạy end-to-end vì database test chưa có Farm, Zone và tài khoản Farm Manager phù hợp; đồng thời RabbitMQ tại `127.0.0.1:5672` chưa chạy làm tiến trình debug bị dừng/ngắt kết nối. Vì vậy tài liệu này chỉ kết luận UC02 hoàn thành về code, migration và build, không dùng smoke test chưa hoàn tất làm bằng chứng runtime.

## 13. Kết luận hoàn thành

UC02 đã hoàn thành theo phạm vi hai bên đã thống nhất:

- Có aggregate và state machine Mission đầy đủ.
- Có API tạo, lập lịch, transition giai đoạn bay và xem chi tiết.
- Có phân quyền Farm Manager và cô lập Tenant/Farm.
- Có kiểm tra Farm, Zone và published map qua contract của BE1.
- Có chống trùng lịch ở Application và database.
- Có optimistic concurrency.
- Có shared audit, không dùng audit entity riêng.
- Có migration và migration đã được áp dụng trên database local.
- Solution build thành công, không có warning hoặc error.

Không thể ghi nhận là đạt toàn bộ acceptance criteria kiểm thử trong tài liệu đặc tả gốc vì unit/integration test đã được loại khỏi phạm vi theo yêu cầu. Đây là giới hạn xác minh đã biết, không phải phần code nghiệp vụ còn thiếu.

API end-to-end UC02 cũng chưa được xác nhận thành công trong lần tái kiểm tra này. Muốn xác nhận runtime đầy đủ cần chuẩn bị một Tenant có Farm, Zone, published map phù hợp, Drone `Available` và tài khoản Owner/Manager; sau đó chạy BE ổn định với RabbitMQ đang hoạt động hoặc tạm tắt RabbitMQ/outbox.

## 14. Phạm vi dành cho use case tiếp theo

Các phần sau không thuộc UC02 hoặc mới chỉ có nền tảng domain để module sau sử dụng:

- Upload media và telemetry: UC03.
- Điều phối AI processing/job: use case AI tương ứng.
- Quy trình review chi tiết và kết quả sức khỏe cây: use case review tương ứng.
- RabbitMQ phải chạy khi cần kiểm tra luồng integration event/outbox end-to-end.

## 15. Lưu ý kỹ thuật không chặn UC02

- `IDroneMissionRepository` hiện đặt trong Domain; nếu áp dụng Clean Architecture nghiêm ngặt hơn, interface này nên chuyển sang Application abstractions trong một đợt refactor riêng.
- Application đang dùng shared audit infrastructure theo chủ đích để đồng bộ với BE1/UC01; đây là ngoại lệ kiến trúc đã được yêu cầu.
- Preflight hiện lưu người xác nhận và thời điểm xác nhận, chưa phải một checklist kiểm tra thiết bị chi tiết.
- Source map do request chỉ định nhưng được xác minh qua contract của BE1 trước khi Mission được tạo.
