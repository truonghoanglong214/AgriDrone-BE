# ADR-0002: Base map được quản lý ở cấp Farm

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Backend 2, GIS

> Amended by ADR-0008 for versioned FarmBoundary semantics and ADR-0011 for V3 integration contracts. The Farm-level base-map decision remains in force.

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
