# Be-Plan Phase 3 Report

Ngày hoàn thành code: 2026-09-23

## Phạm vi hoàn thành

- Chuyển `Drone` thành tài sản toàn hệ thống: domain, command/response,
  repository, query và EF configuration không còn `TenantId`.
- Thêm registry `GET/POST /api/system/drones` và
  `PATCH /api/system/drones/{droneId}/status`, chỉ dành cho SystemAdmin.
- Thêm
  `GET /api/system-manager/farms/{farmId}/drones/available`; handler tự xác minh
  active primary Farm assignment qua `ISystemManagerAccessService`, không tin
  TenantId từ client.
- Xóa route tenant-scoped Drone cũ sau khi replacement đã có contract và
  authorization tests.
- Query availability và schedule-conflict không còn filter theo tenant.
- Registration/status registry dùng system-admin audit với `TenantId` và
  `FarmId` null; audit phát sinh trong Mission vẫn giữ customer Tenant/Farm.

## Database và an toàn dữ liệu

Migration `20260923112119_ConvertDronesToSystemOwned`:

- fail-fast và liệt kê collision của code, serial, registration trước khi đổi
  schema; không tự merge hai physical drones;
- lưu ownership cũ vào `mission.drone_legacy_tenant_ownership` để bảo toàn dấu
  vết và hỗ trợ rollback;
- đổi Mission→Drone FK từ `(drone_id, tenant_id)` sang `drone_id`;
- thay unique indexes tenant-scoped bằng global unique indexes;
- thay schedule index và PostgreSQL exclusion constraint thành global theo
  `drone_id`, ngăn double booking kể cả Mission thuộc hai customer tenants;
- chỉ bỏ `mission.drones.tenant_id` sau khi preflight và archive hoàn tất.

Preflight read-only: `docs/operations/be-plan-phase3-drone-preflight.sql`.

## Rà soát Phase 0–2

- Không sửa hoặc xóa migration lịch sử.
- Phase 1 legacy gate vẫn mặc định trả `410 Gone`; ba route Drone cũ được xóa
  ở Phase 3 vì replacement đã tồn tại và có authorization/model tests.
- Phase 2 SystemManager access được tái sử dụng tại application handler để
  giới hạn availability theo Farm assignment; system actor vẫn không cần
  TenantMembership.
- Baseline trước Phase 3: build pass, 394 unit tests và 43 architecture tests
  pass.

## Verification

- `dotnet build AgriDrone.sln --no-restore`: pass, 0 warning, 0 error.
- Unit tests: 397 pass.
- Architecture tests: 45 pass.
- Formatter check cho toàn bộ file Phase 3: pass. Formatter toàn solution vẫn
  báo whitespace debt có sẵn ở các file ngoài phạm vi Phase 3.
- Sinh SQL riêng cho migration Phase 3: pass; SQL chứa preflight fail-fast,
  legacy ownership archive, global indexes/FK và global exclusion constraint.
- EF pending-model check: pass.
- Docker daemon không hoạt động (`dockerDesktopLinuxEngine` pipe không tồn tại),
  nên chưa thể áp migration và chạy integration suite trên PostgreSQL thật.
  Migration phải được chạy lại trên integration stack trước release; giới hạn
  hạ tầng này không được xem là bằng chứng migration đã áp dụng thành công.
