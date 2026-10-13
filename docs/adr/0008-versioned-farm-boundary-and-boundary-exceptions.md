# ADR-0008: Versioned FarmBoundary và boundary-exception review

- Status: Accepted
- Date: 2026-10-07
- Decision owners: Product, Backend 1, Backend 2, GIS

## Context

Farm hiện lưu một mutable boundary polygon. Mapping, re-identification và pricing cần tham chiếu đúng approved boundary version để tránh tự gán detection ngoài Farm hoặc thay đổi ngược lịch sử của order/result đã xác nhận.

## Decision

- Applicant hoặc TenantOwner có thể cung cấp boundary draft. Chỉ assigned, active và flight-qualified SystemManager được approve operational boundary.
- Mỗi Farm có tối đa một current approved `FarmBoundary`. Edit tạo version mới; không sửa geometry của approved version tại chỗ.
- Boundary lifecycle là `Draft → Approved | Rejected`; approved version cũ trở thành `Superseded` khi version mới được publish.
- MVP dùng một valid PostGIS `Polygon` SRID 4326 cho mỗi Farm. Một Farm được phép chia thành nhiều Zone; mỗi active Zone phải nằm trong FarmBoundary và không overlap active Zone khác, trừ khi một ADR sau này cho phép rõ ràng.
- Near-boundary classification dùng versioned distance policy theo mét. Mỗi decision lưu measured distance, threshold và policy version; numeric default phải đến từ GIS/domain study, không hard-code tùy ý trong handler.
- Boundary exception lifecycle hỗ trợ `OutOfBoundary` và `NeedsReview`. Review outcome là `AcceptedInside`, `RejectedOutside` hoặc `LocationCorrected`.
- Original coordinate/geometry không bị overwrite. Corrected coordinate, reviewer, UTC review time, reason, evidence và authoritative FarmBoundary version được lưu riêng.
- Unresolved exception không được tạo official Plant, tham gia confirmed count, Disease Zone membership hoặc TenantOwner published read model.
- SystemAdmin có thể reassign/escalate nhưng không approve thay operational decision nếu chưa trở thành assigned manager theo audit trail.

## Legacy transition

Boundary hiện hữu không tự trở thành approved version chỉ vì đang nằm trên `Farm`. Expand migration giữ legacy geometry và tạo version/draft/backfill record theo inventory policy; manager approval là gate để đưa Farm vào target flow.

## Consequences

Order pricing, map publication, health ingestion và plant matching phải snapshot/reference FarmBoundary version. Farm/Zone API phải tách draft input khỏi approved operational boundary.
