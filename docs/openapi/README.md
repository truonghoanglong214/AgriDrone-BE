# OpenAPI compatibility snapshots

`be-plan-phase0.openapi.json` là snapshot của API legacy trước khi Phase 1 đóng các entry point sai nghiệp vụ. Snapshot được lấy từ `GET /swagger/v1/swagger.json` sau khi toàn bộ migration hiện tại áp dụng thành công trên PostgreSQL test.

`be-plan-phase1.openapi.json` là contract sau cutover Phase 1. Các route legacy vẫn hiện diện để client nhận hợp đồng chuyển tiếp ổn định, được đánh dấu `deprecated: true` và khai báo response `410`. Khi `LegacyFeatures:EnableDeprecatedEndpoints=false` (mặc định), middleware trả `LegacyFlow.Disabled` trước khi handler chạy.

Không chỉnh tay snapshot. Khi contract chủ ý thay đổi, tạo snapshot/review diff mới và giữ các bản trước để kiểm tra compatibility hoặc thông báo deprecation/`410 Gone`.

Step 0R không mở thêm HTTP endpoint nên không tạo snapshot Swagger giả. API intent
đã khóa cho các step triển khai sau được mô tả tại
[`be1-step0r.openapi-intent.md`](be1-step0r.openapi-intent.md). Khi controller thật
được mở, snapshot mới phải được sinh từ `GET /swagger/v1/swagger.json` và đối chiếu
với intent này.
