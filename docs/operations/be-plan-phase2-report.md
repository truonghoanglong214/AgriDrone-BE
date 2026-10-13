# Be-Plan Phase 2 Report

Ngày hoàn thành code: 2026-09-23

## Phạm vi hoàn thành

- Thêm và seed system role `SYSTEM_MANAGER`.
- SystemManager đăng nhập nhận JWT system session không cần Tenant hoặc
  `TenantMembership`.
- Thêm `SystemManagerProfile` với lifecycle active/suspended, availability,
  flight qualification/expiry và optimistic version.
- Thêm lịch sử `FarmManagerAssignment`, actor/reason ở cả assign và end, cùng
  filtered unique index giới hạn một assignment active trên mỗi Farm.
- Thêm API chỉ SystemAdmin cho create/activate/suspend/update qualification,
  update availability và assign/reassign/end primary manager.
- Thêm `ISystemManagerAccessService`; server resolve Farm và Tenant từ resource,
  sau đó kiểm tra active assignment. Client không gửi TenantId vào quyết định
  authorization.
- Thêm `GET /api/system-manager/farms` cho SystemManager. Query order được nối ở
  Phase 5 vì Phase 2 chưa có aggregate `SurveyOrder`.
- Giữ nguyên toàn bộ legacy membership/assignment data; không auto-promote user.

## API mới

- `POST /api/system/managers`
- `PUT /api/system/managers/{profileId}/activate`
- `PUT /api/system/managers/{profileId}/suspend`
- `PUT /api/system/managers/{profileId}/availability`
- `PUT /api/system/managers/{profileId}/qualification`
- `PUT /api/system/farms/{farmId}/primary-manager`
- `PUT /api/system/farms/{farmId}/primary-manager/end`
- `GET /api/system-manager/farms`

## Database

Migration: `20260923105007_AddSystemManagerFarmAssignments`.

Migration thêm ba PostgreSQL enum, hai bảng mới, optimistic concurrency columns,
FK Farm/Tenant đồng nhất, FK actor/profile và filtered unique index cho active
assignment. EF xác nhận không còn pending model changes.

## Verification

- `dotnet build AgriDrone.sln --no-restore`: pass, 0 warning, 0 error.
- Unit tests: 394 pass.
- Architecture tests: 43 pass.
- EF pending-model check: pass.
- Integration suite không chạy được trong môi trường hiện tại: 10 test fail vì
  PostgreSQL `127.0.0.1:55432`, RabbitMQ và Redis không hoạt động; Docker daemon
  không khả dụng. Đây là giới hạn hạ tầng, không phải assertion failure của code
  Phase 2.

## Release gate còn phụ thuộc môi trường

Trước khi deploy, bật stack integration và chạy lại toàn bộ integration suite để
chứng minh migration từ database thật và concurrent insert trên PostgreSQL. Model
test hiện đã xác nhận filtered unique index tồn tại và handler đã chuyển unique/
optimistic-concurrency violation thành conflict ổn định.
