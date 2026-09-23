# Be-Plan Phase 1 — Legacy endpoint cutover

## Trạng thái mặc định

`LegacyFeatures:EnableDeprecatedEndpoints` mặc định là `false`. Mọi endpoint có `LegacyEndpointAttribute` bị chặn tại middleware, trước authorization handler/application handler và trước mọi database write.

Response ổn định:

- HTTP `410 Gone` với content type `application/problem+json`.
- `errorCode`: `LegacyFlow.Disabled`.
- `legacyRoute`: tên route ổn định, không chứa ID hoặc dữ liệu cardinality cao.
- `replacement`: hướng dẫn flow thay thế.
- `traceId`: correlation cho điều tra vận hành.

Flag `true` chỉ dành cho rollback khẩn cấp có thời hạn. Khi bật, route cũ chạy lại toàn bộ handler cũ và có thể tạo dữ liệu sai target model; cần approval, owner, thời điểm tắt lại và theo dõi metric.

## Phạm vi đã đóng

| Nhóm | Endpoint |
|---|---|
| Self-registration | `POST /api/auth/register` |
| Tenant invitation/membership | `POST /current/invitations/tenant-admin`; `POST /api/tenants/current/invitations/member`; cập nhật role/status member; transfer ownership |
| Farm membership | `POST /api/farms`; assign/update/revoke farm member |
| Tenant-owned drone | đăng ký drone theo tenant; đổi status; lấy availability theo farm |
| Direct mission control | tạo, schedule và transition mission theo farm |
| Legacy harvest quality | đọc catalogue; tạo/version/retire quality grade |

Tổng cộng 19 operations được gate. Hiện chưa có FieldTask controller; architecture test bắt buộc mọi FieldTask write route được thêm về sau phải mang legacy gate.

## Flow được giữ

- Login, forgot/reset password và tenant selection.
- Preview/accept invitation để hoàn tất owner provisioning sau approval.
- SystemAdmin bootstrap, user status, tenant lifecycle và audit.
- Farm/Zone read, cùng Mission/Drone read phục vụ migration và lịch sử.
- SystemAdmin provisioning nội bộ hiện hữu được giữ cho đến khi approval orchestration thay thế ở Phase 5; public/tenant actor không có đường tự tạo Tenant + Owner.

## Quan sát vận hành

- Counter: `legacy_endpoint_attempt_total`.
- Meter: `AgriDrone.Api.LegacyEndpoints`.
- Tags bounded: `route`, `actor`, `outcome`.
- Actor được chuẩn hóa thành `public`, system role, tenant role hoặc `authenticated`; không dùng user/tenant ID làm metric tag.
- Mỗi attempt có warning log event `4100`, gồm route, actor, trạng thái feature flag và trace ID.

OpenAPI giữ route để client thấy deprecation và response `410`. Snapshot Phase 1: `docs/openapi/be-plan-phase1.openapi.json`.

## Farm archive compatibility

`LegacyReadOnlyFarmArchiveDependencyQuery` chỉ đọc các dependency cũ từ Mission, Plant và FieldTask để bảo vệ archive behavior trong thời gian chuyển đổi. Adapter không có command/repository ghi và không tạo FieldTask/Harvest. Nó sẽ được thay bằng dependency SurveyOrder/Mission ở Phase 5/11 theo kế hoạch.

## Quy trình rollback khẩn cấp

1. Xác nhận client/incident cụ thể từ metric và log, ghi owner và deadline.
2. Bật `LegacyFeatures:EnableDeprecatedEndpoints=true` ở đúng environment cần rollback.
3. Theo dõi `legacy_endpoint_attempt_total` và dữ liệu mới phát sinh từ legacy handlers.
4. Hoàn tất client cutover, đưa flag về `false`, kiểm kê và xử lý mọi record phát sinh trong cửa sổ rollback.

Không dùng flag như trạng thái vận hành dài hạn và không bật chỉ để bỏ qua lỗi client.
