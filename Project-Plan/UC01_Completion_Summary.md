# BE2-UC01 — Drone Registry & Availability

## 1. Thông tin chung

- **Dự án**: AgriDrone — Smart Dragon Fruit Farm Management System Using Drone and AI
- **Mã đồ án**: FA26SE218
- **Use case**: BE2-UC01 — Drone Registry & Availability
- **Ngày cập nhật**: 24/08/2026
- **Trạng thái**: `Done`
- **Phạm vi đánh giá**: Code nghiệp vụ, API, phân quyền, persistence và migration; không bao gồm unit test, integration test hoặc contract test.

## 2. Mục tiêu

UC01 quản lý danh mục Drone ở cấp hệ thống và cung cấp danh sách Drone khả dụng để Farm Manager lựa chọn khi lập Mission.

UC01 bảo đảm:

- Drone mới được đăng ký với trạng thái `Available`.
- Chỉ Drone hợp lệ và đang `Available` mới được dùng cho Mission mới.
- Drone đang bảo trì hoặc đã retire không được bắt đầu Mission.
- Không có hai Mission giữ lịch giao nhau trên cùng một Drone.
- Drone đã phát sinh lịch sử không bị hard-delete.
- Mọi lần đăng ký hoặc đổi trạng thái đều có audit.

## 3. Phân quyền API

| API | Quyền truy cập | Mục đích |
|---|---|---|
| `POST /api/tenants/{tenantId}/drones` | `SYSTEM_ADMIN` | Đăng ký Drone mới cho tenant |
| `PATCH /api/tenants/{tenantId}/drones/{droneId}/status` | `SYSTEM_ADMIN` | Chuyển trạng thái bảo trì, hoàn tất bảo trì hoặc retire Drone |
| `GET /api/farms/{farmId}/drones/available` | Tenant `Owner` hoặc `Manager` của đúng Farm | Lấy danh sách Drone khả dụng trong khoảng thời gian Mission |

Endpoint lấy Drone khả dụng sử dụng resource-based authorization với:

```text
FarmAccessTarget(TenantId, FarmId)
```

`TenantId` được lấy từ tenant context của người dùng. `FarmId` được lấy từ URL và dùng để kiểm tra quyền trên Farm.

## 4. Phạm vi sở hữu Drone

Drone thuộc về `Tenant`, không thuộc riêng một Farm.

Quy tắc áp dụng:

- Một Drone có thể được sử dụng cho nhiều Farm trong cùng Tenant.
- `farmId` trong API availability chỉ dùng để kiểm tra quyền của Owner/Manager.
- Sau khi được cấp quyền, hệ thống trả các Drone khả dụng thuộc Tenant hiện tại.
- Dữ liệu Drone của Tenant khác không được trả về.

## 5. Trạng thái Drone

Hệ thống giữ đầy đủ các trạng thái:

```text
Available
InMission
Maintenance
Inactive
Retired
```

Trong phạm vi UC01, API quản trị hỗ trợ:

```text
Available   -> Maintenance
Maintenance -> Available
Available   -> Retired
Maintenance -> Retired
```

Quy tắc:

- Drone mới mặc định là `Available`.
- `Retired` là trạng thái cuối và không thể quay lại hoạt động.
- Gửi lại cùng trạng thái được xử lý idempotent, không tạo audit trùng.
- `InMission` và `Inactive` được giữ để tương thích mô hình nhưng không phải transition quản trị của UC01.

## 6. Audit trạng thái

Mỗi lần đăng ký hoặc thay đổi trạng thái tạo một `DroneStatusChange` gồm:

- `TenantId`.
- `DroneId`.
- Trạng thái trước.
- Trạng thái mới.
- Người thực hiện.
- Thời điểm thực hiện.

UC01 không cung cấp API hoặc repository hard-delete Drone, nhờ đó lịch sử Mission và trạng thái được giữ lại.

## 7. Quy tắc Drone khả dụng

Một Drone được trả về khi đồng thời thỏa mãn:

```text
Đúng Tenant
AND chưa bị soft-delete
AND Status = Available
AND đã tới ngày đăng ký hoạt động
AND đăng ký còn hiệu lực đến cuối Mission
AND không bị trùng thời gian bảo trì
AND không có Mission Ready/Flying giao lịch
```

### 7.1. Biên thời gian bảo trì

Mission được phép kết thúc đúng lúc lịch bảo trì bắt đầu:

```text
Mission:     [08:00, 09:00)
Maintenance: [09:00, ...)
```

Điều kiện query:

```text
NextMaintenanceAt >= Mission.EndAt
```

### 7.2. Biên thời gian Mission

Khoảng lịch sử dụng quy ước nửa kín:

```text
[StartAt, EndAt)
```

Vì vậy hai Mission nối tiếp nhau không bị xem là giao lịch:

```text
Mission A: [08:00, 09:00)
Mission B: [09:00, 10:00)
```

Hai khoảng giao nhau khi:

```text
ExistingStart < NewEnd
AND ExistingEnd > NewStart
```

## 8. Database và migration

Migration UC01:

```text
20260820151810_CompleteDroneRegistryUc01
```

Migration bổ sung:

- PostgreSQL extension `btree_gist`.
- Cột `mission.drone_missions.scheduled_end_at`.
- Check constraint yêu cầu `scheduled_end_at > scheduled_at` khi có đủ hai giá trị.
- Exclusion constraint `ex_drone_missions_no_schedule_overlap`.
- Bảng `mission.drone_status_changes`.
- Foreign key bảo đảm Drone và audit thuộc cùng Tenant.
- Các index phục vụ availability query và status history.
- Giá trị `RETIRED` trong enum `system.drone_status`.

Exclusion constraint chỉ giữ lịch cho Mission có trạng thái:

```text
Ready
Flying
```

## 9. Thành phần đã triển khai

```text
AgriDrone.Modules.Missions/
├── Domain/Drones/
│   ├── Drone.cs
│   ├── DroneStatus.cs
│   ├── DroneStatusChange.cs
│   ├── IDroneRepository.cs
│   └── IDroneStatusChangeRepository.cs
├── Application/
│   ├── Abstractions/
│   │   ├── DroneErrors.cs
│   │   ├── IDroneQueries.cs
│   │   └── IMissionsUnitOfWork.cs
│   └── Features/Drones/
│       ├── RegisterDrone/
│       ├── ChangeDroneStatus/
│       └── GetAvailableDrones/
├── Infrastructure/
│   ├── Queries/DroneQueries.cs
│   ├── Repositories/DroneRepository.cs
│   ├── Repositories/DroneStatusChangeRepository.cs
│   └── Persistence/Configurations/
│       ├── DroneConfiguration.cs
│       ├── DroneMissionConfiguration.cs
│       └── DroneStatusChangeConfiguration.cs
└── DependencyInjection.cs

AgriDrone.Api/
├── Contracts/Drones/
└── Controllers/DronesController.cs
```

## 10. Kết quả hoàn thành

UC01 đã hoàn thành các yêu cầu nghiệp vụ trong phạm vi dự án:

- Quản lý đăng ký Drone.
- Quản lý trạng thái và retire Drone.
- Ghi nhận audit trạng thái.
- Lọc Drone khả dụng theo Tenant, trạng thái, đăng ký, bảo trì và lịch Mission.
- Phân quyền đúng cho System Admin, Tenant Owner và Farm Manager.
- Chống giao lịch ở cả query và PostgreSQL constraint.
- Hoàn thiện API, Application, Domain, Infrastructure, DI và migration.
- Không hard-delete Drone có lịch sử.

## 11. Bước tiếp theo

Use case tiếp theo:

```text
BE2-UC02 — Mission Lifecycle
```

UC02 cần xây dựng lifecycle của `DroneMission`, sử dụng Drone availability và database scheduling constraint đã hoàn thiện trong UC01.
