# ADR-0008: Versioned FarmBoundary và boundary-exception review

- Status: Accepted
- Date: 2026-10-07
- Amended: 2026-10-17 — same-visit drone-GNSS boundary capture before automatic Baseline flight (superseded)
- Amended: 2026-10-20 — Orthophoto-based 4-corner boundary marking after flight: bay chụp khu vực farm sau duyệt lịch hẹn -> ghép Orthophoto georeferenced -> Manager chấm 4 góc trên ảnh bản đồ -> hệ thống quy đổi tọa độ tạo FarmBoundary -> duyệt boundary & publish base map -> Order chuyển AwaitingPricing
- Decision owners: Product, Backend 1, Backend 2, GIS

## Context

Farm hiện lưu một mutable boundary polygon. Mapping, re-identification và pricing cần tham chiếu đúng approved boundary version để tránh tự gán detection ngoài Farm hoặc thay đổi ngược lịch sử của order/result đã xác nhận.

## Decision

- Applicant hoặc TenantOwner có thể cung cấp boundary draft/tọa độ ước lượng ban đầu. Chỉ assigned, active và flight-qualified SystemManager được approve operational boundary.
- **Quy trình Baseline và xác định ranh giới cho unmapped Farm (Cập nhật 2026-10-20 - Phương án 1)**:
  1. **Duyệt lịch hẹn (Appointment Confirmed)**: Đề xuất và xác nhận lịch hẹn Baseline. Khi lịch hẹn được xác nhận và đảm bảo điều kiện an toàn bay (consent, flight permission, phi công hợp lệ), `SurveyOrder` chuyển trạng thái sang `BaselineReady`.
  2. **Bay chụp ảnh khu vực farm**: Drone cất cánh (`StartFlight`), `SurveyOrder` chuyển sang `BaselineInProgress`. Chuyến bay thực hiện chụp ảnh toàn cảnh khu vực farm theo vùng ước lượng / pilot flight.
  3. **Ghép ảnh thành Orthophoto có tọa độ địa lý**: Drone bay xong, ảnh được upload lên hệ thống (MinIO) và kích hoạt pipeline xử lý (Metashape) để ghép thành ảnh trực giao Orthophoto có tham chiếu tọa độ địa lý (GeoTIFF / WGS84). `SurveyOrder` chuyển sang `AwaitingBaselineReview`.
  4. **Manager mở ảnh bản đồ, chấm bốn góc của farm**: Manager mở giao diện bản đồ / Orthophoto Viewer xem ảnh trực giao thực tế độ nét cao vừa ghép và click chấm 4 điểm góc của nông trại trực tiếp trên ảnh.
  5. **Hệ thống đổi bốn điểm thành tọa độ, tạo boundary và duyệt**: Hệ thống chuyển đổi 4 điểm ảnh thành tọa độ địa lý (WGS84 SRID 4326 Polygon), tạo phiên bản `FarmBoundary`. Manager review và duyệt boundary. AI đếm trụ và nhận diện cây trong ranh giới này. Khi publish Base Map hoàn tất, `SurveyOrder` chuyển sang `AwaitingPricing` để chốt số lượng trụ và báo giá.
- Bãi bỏ cơ chế cũ bắt buộc Manager phải điều khiển drone bay đến lần lượt từng góc ngoài thực địa để lấy mẫu GNSS (`BoundaryCaptureSession`) trước chuyến bay mapping.
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

Order pricing, map publication, health ingestion và plant matching phải snapshot/reference FarmBoundary version. Unmapped Farm có thể cất cánh bay Baseline chụp ảnh ngay sau khi lịch hẹn được duyệt (`BaselineReady`); boundary chính thức được xác lập từ ảnh Orthophoto thực tế của chuyến bay này, giúp giảm thiểu thao tác đo đạc thủ công tại thực địa và đảm bảo độ chính xác hình học cao.
