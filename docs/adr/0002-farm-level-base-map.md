# ADR-0002: Base map được quản lý ở cấp Farm

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Backend 2, GIS

## Context

Contract V1 hiện có thể publish một `ZoneMapVersion` cho từng Mapping Mission. `Be-Plan` yêu cầu một approved base map bền vững cho Farm, được tái sử dụng qua mọi Survey Service và survey lặp lại.

## Decision

- Thêm `FarmBaseMapVersion` làm aggregate/header với trạng thái `Draft | Published | Superseded`.
- `ZoneMapVersion` được giữ làm spatial slice và tham chiếu đúng một `FarmBaseMapVersion`.
- Mỗi Farm có tối đa một current published base map, được bảo vệ bằng database constraint/index và transaction publication.
- Initial publication chỉ được tạo từ Baseline Mapping Mission của SurveyOrder có `RequiresBaselineMapping = true`.
- Survey sau re-identify vào Digital Plant Profiles hiện hữu; không tạo base map mới chỉ vì có survey mới.
- Sửa map đã publish đi qua `BaseMapAmendment` có reason, review và audit. Plant ID không đổi; plant vắng mặt trong một survey không tự bị retire.
- TenantOwner chỉ đọc published map/profile; draft và pending candidate không được lộ.

## Consequences

V1 được giữ trong cửa sổ migration để đọc/replay. Flow mới dùng event V2 có SurveyOrder và Farm base-map context; dữ liệu Zone hiện hữu cần được inventory/backfill trước khi contract schema.
