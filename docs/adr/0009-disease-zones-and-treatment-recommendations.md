# ADR-0009: Disease Zone và expert-validated treatment recommendation

- Status: Accepted
- Date: 2026-10-07
- Decision owners: Product, Backend 1, AI/GIS

## Context

Plant Health AI có thể tạo individual findings, spatial clusters và recommendation candidates, nhưng AI output không được trở thành official owner-facing advice nếu chưa qua human review và catalogue provenance.

## Decision

- Một `DiseaseZone` biểu diễn một Polygon SRID 4326. Nhiều vùng rời nhau được biểu diễn bằng nhiều DiseaseZone, không dùng MultiPolygon trong MVP.
- Zone tham chiếu SurveyOrder, SurveyResult, Farm, FarmBoundary version, Farm base-map version, condition/severity version và membership version.
- Lifecycle là `Proposed → Reviewed → Published`; proposal có thể thành `Rejected`, published version cũ có thể thành `Superseded`.
- Assigned eligible SystemManager có thể sửa geometry/membership trong review, nhưng original AI proposal và corrected decision đều phải được giữ.
- Chỉ Published DiseaseZone được TenantOwner nhìn thấy hoặc dùng trong official result.
- Treatment recommendation là immutable/versioned catalogue entry theo condition/disease + severity, effective window và lifecycle.
- SystemAdmin publish catalogue entry kèm expert/source provenance, advisory disclaimer và effective window. MVP không tạo actor `DomainExpert` riêng.
- AI chỉ gửi recommendation candidate/reference. SystemManager chọn đúng một applicable catalogue version hoặc từ chối tất cả với reason; không được nhập free-form treatment advice vào official result.
- Retired recommendation vẫn resolve được cho historical result nhưng không được chọn cho decision mới ngoài explicit historical replay.

## Consequences

Plant Health V3 phải mang proposed Disease Zones, membership, boundary exceptions và recommendation candidates. Result publication transaction phải commit zone/recommendation provenance cùng SurveyResult, audit và outbox.
