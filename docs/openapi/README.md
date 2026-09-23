# OpenAPI compatibility snapshots

`be-plan-phase0.openapi.json` là snapshot của API legacy trước khi Phase 1 đóng các entry point sai nghiệp vụ. Snapshot được lấy từ `GET /swagger/v1/swagger.json` sau khi toàn bộ migration hiện tại áp dụng thành công trên PostgreSQL test.

`be-plan-phase1.openapi.json` là contract sau cutover Phase 1. Các route legacy vẫn hiện diện để client nhận hợp đồng chuyển tiếp ổn định, được đánh dấu `deprecated: true` và khai báo response `410`. Khi `LegacyFeatures:EnableDeprecatedEndpoints=false` (mặc định), middleware trả `LegacyFlow.Disabled` trước khi handler chạy.

Không chỉnh tay snapshot. Khi contract chủ ý thay đổi, tạo snapshot/review diff mới và giữ các bản trước để kiểm tra compatibility hoặc thông báo deprecation/`410 Gone`.
