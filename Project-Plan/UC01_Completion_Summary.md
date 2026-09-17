# BE2-UC01 — Drone Registry & Availability

## 1. Thông tin chung

- **Dự án**: AgriDrone — Smart Dragon Fruit Farm Management System Using Drone and AI
- **Mã đồ án**: FA26SE218
- **Use case**: BE2-UC01 — Drone Registry & Availability
- **Ngày cập nhật**: 09/09/2026
- **Trạng thái**: `Done về chức năng; còn một cleanup vị trí folder`
- **Phạm vi đánh giá**: Domain, Application, Infrastructure, API, phân quyền, persistence, migration và build; chưa bao gồm unit test, integration test hoặc API authorization test riêng cho Drone.

## 2. Quyết định nghiệp vụ đã điều chỉnh

Drone được xem là tài sản thuộc `Tenant`, không thuộc riêng một Farm và không do `SystemAdmin` sở hữu.

Phân quyền được chốt như sau:

- `TenantAdmin` đại diện tenant đăng ký, xem và quyết định trạng thái vận hành của Drone thuộc tenant hiện tại.
- `SystemAdmin` chỉ theo dõi Drone xuyên tenant để hỗ trợ vận hành; không đăng ký, thay đổi trạng thái hoặc retire Drone.
- Farm Owner/Manager chỉ xem Drone khả dụng trong tenant để lựa chọn khi lập Mission.

Thay đổi này thay thế phân quyền UC01 ban đầu vốn giao việc đăng ký và đổi trạng thái Drone cho `SystemAdmin`.

## 3. Mục tiêu nghiệp vụ

UC01 bảo đảm:

- Drone mới được đăng ký vào tenant trong JWT và mặc định ở trạng thái `Available`.
- TenantAdmin không phải nhập `TenantId` bằng tay và không thể quản lý Drone của tenant khác.
- TenantAdmin xem được danh sách và chi tiết Drone thuộc tenant hiện tại.
- TenantAdmin quản lý trạng thái vận hành của Drone.
- SystemAdmin xem được danh sách và chi tiết Drone xuyên tenant nhưng không có API mutation.
- Chỉ Drone hợp lệ và đang `Available` mới được dùng cho Mission mới.
- Drone đang bảo trì, ngừng hoạt động, đang bay hoặc đã retire không được bắt đầu Mission.
- Drone có Mission đang chặn không được chuyển sang trạng thái làm Drone mất khả dụng.
- Không có hai Mission giữ lịch giao nhau trên cùng một Drone.
- Drone có lịch sử không bị hard-delete.
- Đăng ký và thay đổi trạng thái đều được ghi vào shared audit.

## 4. Phân quyền và API

### 4.1. TenantAdmin

| API | Mục đích |
|---|---|
| `POST /api/drones` | Đăng ký Drone vào tenant hiện tại |
| `GET /api/drones` | Lấy danh sách Drone của tenant, có phân trang, tìm kiếm và lọc trạng thái |
| `GET /api/drones/{droneId}` | Lấy chi tiết Drone thuộc tenant hiện tại |
| `PATCH /api/drones/{droneId}/operational-status` | Thay đổi trạng thái vận hành của Drone thuộc tenant hiện tại |

Tất cả endpoint trên sử dụng:

```text
AccessAuthorizationPolicies.TenantAdmin
```

`TenantId` được lấy từ `ICurrentTenant`, không xuất hiện trong route hoặc request body. Repository/query tiếp tục dùng `TenantId` để cô lập dữ liệu.

Policy `TenantAdmin` cũng cho phép tenant `Owner`, vì `Owner` có mức tenant access cao hơn `Admin`.

### 4.2. SystemAdmin

| API | Mục đích |
|---|---|
| `GET /api/system/drones` | Xem danh sách Drone toàn hệ thống; có thể lọc theo `TenantId`, trạng thái và từ khóa |
| `GET /api/system/drones/{droneId}` | Xem chi tiết một Drone xuyên tenant |

Controller sử dụng:

```text
AccessAuthorizationPolicies.SystemAdmin
```

SystemAdmin không có endpoint `POST`, `PUT`, `PATCH` hoặc `DELETE` đối với Drone.

### 4.3. Farm Owner/Manager

| API | Mục đích |
|---|---|
| `GET /api/farms/{farmId}/drones/available` | Lấy Drone khả dụng trong khoảng thời gian dự kiến của Mission |

Endpoint sử dụng resource-based authorization:

```text
FarmAccessTarget(TenantId, FarmId)
AccessAuthorizationPolicies.FarmManage
```

`TenantId` lấy từ tenant context. `FarmId` lấy từ route và chỉ dùng để kiểm tra quyền trên Farm; Drone vẫn thuộc Tenant.

## 5. Phạm vi sở hữu và tenant isolation

- Một Drone thuộc đúng một Tenant.
- Một Drone có thể phục vụ nhiều Farm trong cùng Tenant.
- TenantAdmin chỉ truy vấn và thay đổi Drone có `Drone.TenantId` trùng tenant trong JWT.
- Khi `droneId` tồn tại ở tenant khác, endpoint TenantAdmin trả về `NotFound` thay vì làm lộ dữ liệu.
- Query SystemAdmin được tách thành method có tên `GetSystemPageAsync` và `GetSystemDetailsAsync`; query TenantAdmin luôn bắt buộc `TenantId`.
- SystemAdmin chỉ được gọi query xuyên tenant thông qua controller có policy `SystemAdmin`.

## 6. Đăng ký Drone

Khi đăng ký:

1. API lấy `TenantId` từ JWT.
2. Validator kiểm tra code, tên, model, manufacturer, serial, registration, trọng lượng và khoảng ngày đăng ký.
3. Code, serial và registration number được chuẩn hóa.
4. Hệ thống chống trùng trong cùng Tenant đối với:
   - `Code`.
   - `SerialNumber`.
   - `RegistrationNumber`.
5. Domain tạo Drone mới với trạng thái `Available`.
6. Shared audit ghi action `REGISTER`.

## 7. Trạng thái Drone

Các trạng thái:

```text
Available
InMission
Maintenance
Inactive
Retired
```

TenantAdmin được phép thực hiện:

```text
Available   -> Maintenance
Maintenance -> Available
Available   -> Inactive
Maintenance -> Inactive
Inactive    -> Available
Available   -> Retired
Maintenance -> Retired
Inactive    -> Retired
```

Mission lifecycle tự quản lý:

```text
Available -> InMission
InMission -> Available     khi chuyến bay hoàn thành
InMission -> Maintenance   khi chuyến bay thất bại
```

Quy tắc:

- `Retired` là trạng thái cuối và không thể quay lại hoạt động.
- TenantAdmin không thể trực tiếp đặt trạng thái `InMission`; trạng thái này chỉ do Mission lifecycle thay đổi.
- Gửi lại đúng trạng thái hiện tại được xử lý idempotent và không ghi audit trùng.
- `NextMaintenanceAt` chỉ được truyền khi hoàn tất bảo trì `Maintenance -> Available`.
- Thời điểm bảo trì tiếp theo phải lớn hơn thời điểm hoàn tất bảo trì.
- Thay đổi thành công ghi shared audit action `CHANGE_OPERATIONAL_STATUS`.

## 8. Mission đang chặn thay đổi trạng thái

Khi target status là một trong các trạng thái làm Drone không còn khả dụng:

```text
Maintenance
Inactive
Retired
```

Backend từ chối nếu Drone đang được gán vào Mission có trạng thái:

```text
Draft
Scheduled
InFlight
```

TenantAdmin phải hủy hoặc xử lý Mission đang gán trước khi thay đổi trạng thái Drone. Quy tắc này tránh trường hợp một Mission `Scheduled` bắt đầu với Drone không còn `Available`.

## 9. Quy tắc Drone khả dụng

Một Drone được trả về khi đồng thời thỏa mãn:

```text
Đúng Tenant
AND chưa bị soft-delete
AND Status = Available
AND RegistrationDate không sau ngày bắt đầu Mission
AND RegistrationExpiryDate không trước ngày kết thúc Mission
AND NextMaintenanceAt không nằm trước thời điểm kết thúc Mission
AND không có Mission Scheduled/InFlight giao thời gian
```

Khoảng thời gian sử dụng quy ước nửa kín:

```text
[StartAt, EndAt)
```

Hai khoảng giao nhau khi:

```text
ExistingStart < NewEnd
AND ExistingEnd > NewStart
```

Vì vậy hai Mission nối tiếp nhau không bị xem là giao lịch:

```text
Mission A: [08:00, 09:00)
Mission B: [09:00, 10:00)
```

## 10. Audit và lịch sử

UC01 ban đầu sử dụng bảng `mission.drone_status_changes`. Khi UC02 được triển khai, migration:

```text
20260901054425_CompleteMissionLifecycleUc02AndMigrateDroneAudit
```

đã:

1. Chuyển lịch sử cũ từ `mission.drone_status_changes` sang `system.audit_logs`.
2. Xóa bảng audit riêng `mission.drone_status_changes`.
3. Chuyển code đăng ký, thay đổi trạng thái và Mission lifecycle sang shared audit.

Các action hiện được sử dụng:

```text
REGISTER
CHANGE_OPERATIONAL_STATUS
MISSION_STATUS_CHANGE
```

UC01 không cung cấp hard-delete API hoặc repository method cho Drone, nhờ đó lịch sử Mission và audit được giữ lại.

## 11. Database và migration

Migration nền của UC01:

```text
20260820151810_CompleteDroneRegistryUc01
```

Migration này bổ sung:

- PostgreSQL extension `btree_gist`.
- Cột `mission.drone_missions.scheduled_end_at`.
- Check constraint yêu cầu `scheduled_end_at > scheduled_at` khi có đủ hai giá trị.
- Exclusion constraint chống hai Mission giữ lịch giao nhau trên cùng Drone.
- Index phục vụ availability query.
- Giá trị `RETIRED` trong enum `system.drone_status`.
- Nền audit Drone ban đầu, sau đó được UC02 chuyển sang shared audit như mô tả ở trên.

Các thay đổi phân quyền, list/detail và query SystemAdmin hiện tại không thay đổi schema, vì vậy không cần migration mới.

## 12. Cấu trúc source chuẩn

```text
AgriDrone.Modules.Missions/
├── Domain/Drones/
│   ├── Drone.cs
│   ├── DroneStatus.cs
│   └── IDroneRepository.cs
├── Application/
│   ├── Abstractions/
│   │   ├── DroneErrors.cs
│   │   └── IDroneQueries.cs
│   └── Features/Drones/
│       ├── RegisterDrone/
│       ├── ChangeDroneStatus/
│       ├── GetAvailableDrones/
│       ├── GetDrones/
│       ├── GetDroneDetails/
│       ├── GetSystemDrones/
│       └── GetSystemDroneDetails/
├── Infrastructure/
│   ├── Queries/DroneQueries.cs
│   ├── Repositories/DroneRepository.cs
│   └── Persistence/Configurations/
│       ├── DroneConfiguration.cs
│       └── DroneMissionConfiguration.cs
└── DependencyInjection.cs

AgriDrone.Api/
├── Contracts/Drones/
└── Controllers/
    ├── DronesController.cs
    └── SystemDronesController.cs
```

Mỗi use case có query/command, validator và handler riêng. API request nằm trong API project; domain behavior nằm trong `Drone`; EF query và repository implementation nằm trong Infrastructure.

Tại thời điểm cập nhật, ba file `GetSystemDronesQuery`, `GetSystemDronesQueryValidator` và `GetSystemDronesQueryHandler` đang nằm vật lý tại:

```text
Application/Features/GetSystemDrones/
```

Namespace của chúng đã đúng, nên build không lỗi. Tuy nhiên, để thống nhất với các feature Drone còn lại, cần chuyển folder này thành:

```text
Application/Features/Drones/GetSystemDrones/
```

## 13. Kết quả xác minh

Lần build gần nhất ngày 09/09/2026:

```text
dotnet build backend/AgriDrone.sln --no-restore

Build succeeded.
0 Warning(s)
0 Error(s)
```

Chưa có test riêng cho các use case Drone. Vì vậy trạng thái hoàn thành ở đây xác nhận chức năng và build đúng theo phạm vi đã chốt, không phải xác nhận runtime hoặc authorization test đầy đủ.

## 14. Kết luận

UC01 hiện cung cấp đầy đủ:

- TenantAdmin đăng ký, xem và quản lý trạng thái Drone của tenant hiện tại.
- SystemAdmin theo dõi danh sách và chi tiết Drone xuyên tenant ở chế độ read-only.
- Farm Owner/Manager lấy danh sách Drone khả dụng để lập Mission.
- Tenant isolation, availability, chống trùng lịch, blocking Mission và shared audit.
- Không hard-delete và không có SystemAdmin mutation API.

Sau khi chuyển đúng folder `GetSystemDrones`, UC01 có thể được giữ ổn định và bước triển khai tiếp theo là tiếp tục BE2-UC03 với `CompleteUploadSession`, telemetry JSON chuẩn hóa và finalize upload.
