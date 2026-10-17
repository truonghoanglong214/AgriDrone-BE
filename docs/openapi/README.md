# OpenAPI compatibility snapshots

`be-plan-phase0.openapi.json` là snapshot của API legacy trước khi Phase 1 đóng các entry point sai nghiệp vụ. Snapshot được lấy từ `GET /swagger/v1/swagger.json` sau khi toàn bộ migration hiện tại áp dụng thành công trên PostgreSQL test.

`be-plan-phase1.openapi.json` là contract sau cutover Phase 1. Các route legacy vẫn hiện diện để client nhận hợp đồng chuyển tiếp ổn định, được đánh dấu `deprecated: true` và khai báo response `410`. Khi `LegacyFeatures:EnableDeprecatedEndpoints=false` (mặc định), middleware trả `LegacyFlow.Disabled` trước khi handler chạy.

Không chỉnh tay snapshot. Khi contract chủ ý thay đổi, tạo snapshot/review diff mới và giữ các bản trước để kiểm tra compatibility hoặc thông báo deprecation/`410 Gone`.

Step 0R không mở thêm HTTP endpoint nên không tạo snapshot Swagger giả. API intent
đã khóa cho các step triển khai sau được mô tả tại
[`be1-step0r.openapi-intent.md`](be1-step0r.openapi-intent.md). Khi controller thật
được mở, snapshot mới phải được sinh từ `GET /swagger/v1/swagger.json` và đối chiếu
với intent này.

`be1-surveys.openapi.json` là snapshot runtime mới nhất sau Phase 4E. Snapshot khóa
tám route Survey Request cho public, TenantOwner và SystemAdmin; public submit là
anonymous, các command retry-safe yêu cầu `Idempotency-Key`, các enum dùng wire
token ổn định. Route approve chỉ dành cho SystemAdmin, nhận body có checklist/
reason/expected version và trả identifiers/trạng thái, không lộ invitation token,
password hoặc applicant PII. Hai route SystemManager boundary-scope cung cấp
assigned-manager work context và atomic boundary verification với GeoJSON,
evidence cùng optimistic version guards. Bốn route Phase 4B bổ sung manager
propose/re-propose Baseline appointment, owner confirm/request-reschedule và manager
đọc authoritative Baseline readiness theo `missionId`; appointment dùng optimistic
version guards và readiness không nhận payment state từ client. Route Phase 4C
`POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm` chỉ nhận
expected Order/base-map version và evidence; count, price version, unit price,
currency và final price đều là server-owned output. Phase 4D mở thêm
`POST /api/tenant-owner/survey-orders/{orderId}/payments`, chỉ nhận
expected Order version và trả session được tạo từ amount/currency snapshot phía
server. `POST /api/integrations/payments/{provider}/callbacks` là anonymous provider
route có hai header timestamp/signature, payload identity/event/amount/currency và
không có Bearer security requirement; chữ ký vẫn được kiểm tra trước application
handler. Phase 4E bổ sung request/review/apply PriceAdjustment append-only,
SystemAdmin payment reconciliation và paid-service readiness theo MissionPurpose.
SystemAdmin survey-service catalogue chỉ nhúng `currentPrice` đang hiệu lực; lịch
sử giá đầy đủ có response riêng tại `/{serviceId}/prices`.
`POST /api/system-admin/survey-services/{serviceId}/prices` chỉ nhận amount,
currency và expected service version; backend tự đặt thời điểm hiệu lực UTC hiện
tại và để version mở cho tới lần thay giá tiếp theo.
Callback nhận thêm refund/chargeback nhưng vẫn giữ PaymentEvent bất biến; owner
không có contract xác nhận payment trực tiếp. Snapshot được sinh từ
`GET /swagger/v1/swagger.json`

R2 (2026-10-15) khóa public catalogue và paid-readiness purpose chỉ còn
`PLANT_HEALTH`. `HARVEST_READINESS` vẫn tồn tại trong database/contract cũ để
đọc lịch sử nhưng không còn là public request option. Capture authorization
được quản trị qua:

- `PUT /api/system-admin/survey-orders/{orderId}/capture-authorizations/{type}/verify`
- `PUT /api/system-admin/survey-orders/{orderId}/capture-authorizations/{type}/revoke`

`type` chỉ nhận `WRITTEN_DATA_USE_CONSENT` hoặc `FLIGHT_PERMISSION`.
của API Development đang chạy với startup migration/core-master-data validation
tạm tắt bằng environment override; giá trị mặc định của ứng dụng vẫn bật các
startup gate này.
