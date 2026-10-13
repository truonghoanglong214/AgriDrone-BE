# Be-Plan Glossary

Status: **Approved**  
Effective date: 2026-09-22; amended 2026-10-08 by Step 0R
Source of truth: `Codex-Plan/Be-Plan.md`

Tài liệu này là glossary duy nhất cho workflow dịch vụ khảo sát. Tên trong code/API có thể dùng `PascalCase` hoặc mã `UPPER_SNAKE_CASE`, nhưng không được đổi nghĩa dưới đây.

| Thuật ngữ chuẩn | Định nghĩa bắt buộc | Không được hiểu là |
|---|---|---|
| **Public Applicant** | Người chưa đăng nhập gửi `NewCustomer` Survey Request và cung cấp snapshot liên hệ/Farm ban đầu. | TenantOwner đã được provision; người được phép tạo trực tiếp Tenant/Farm; actor vận hành drone. |
| **SystemAdmin** | System actor quản trị toàn hệ thống: thẩm định Survey Request, quản lý SystemManager, drone hệ thống, Survey Service/giá và master data. | TenantAdmin hoặc thành viên của một Tenant khách hàng. |
| **SystemManager** | Nhân sự vận hành của AgriDrone, có profile active, available và flight-qualified; chỉ thao tác Farm/Order được gán làm primary manager. | Farm Manager thuộc TenantMembership/FarmMembership; TenantOwner; worker của khách hàng. |
| **TenantOwner** | Chủ tài khoản khách hàng gắn với Tenant sau approval/invitation; xác nhận lịch, thanh toán và chỉ xem dữ liệu/kết quả đã publish của Tenant mình. | Người tự vận hành drone, duyệt AI thô hoặc tự tạo Mission. |
| **Survey Service** | Sản phẩm khảo sát được bán và định giá theo active pole position. MVP chỉ có `PLANT_HEALTH` và `HARVEST_READINESS`. | Baseline Mapping; Follow-up; harvest/yield management; dịch vụ tính theo hectare. |
| **Baseline Mapping** | Mission vận hành bắt buộc, tự động thêm vào SurveyOrder đầu tiên khi Farm chưa có approved base map; tạo map nền và Digital Plant Profiles ban đầu sau human review. | Survey Service bán riêng; quy trình chạy lại ở mọi survey; map draft mà TenantOwner được xem. |
| **Follow-up** | Survey lặp lại trên cùng Farm và cùng Survey Service khi đã có lần completed tương thích; so sánh với lần compatible gần nhất. | Một Survey Service hoặc price item riêng; mọi survey sau Baseline Mapping bất kể service. |
| **Official Result** | Survey Result đã được SystemManager có thẩm quyền review/correct/approve và publish; đây là kết quả duy nhất TenantOwner được xem như kết luận chính thức. | AI output/prediction pending, draft map hoặc dữ liệu chưa publish. |
| **Pole Position** | Vị trí một trụ thanh long trong approved FarmBoundary/scope; active pole position là đơn vị tính giá. | Số nhánh trên trụ; diện tích hectare; một AI detection chưa review. |
| **ConfirmedSurveyPoleCount** | Số active pole positions trong approved boundary/scope được assigned SystemManager xác nhận từ current published Farm map. | Estimated pole count trong request; pending candidates; count tự suy ra từ area. |
| **FarmBoundary** | Polygon SRID 4326 được version hóa; mỗi Farm có tối đa một current approved version và có thể chứa nhiều Zone không overlap. | Farm center point; Zone boundary; mutable polygon được sửa ngược lịch sử. |
| **Boundary Exception** | Candidate/finding được phân loại `OutOfBoundary` hoặc `NeedsReview` theo một FarmBoundary và policy version cụ thể; chỉ decision có reviewer, reason và evidence mới resolve exception. | Dữ liệu được tự động kéo vào Farm gần nhất; lý do để overwrite original coordinate. |
| **Farm Base Map** | Bản đồ cấp Farm đã publish, gồm các Zone map version và official active Plant mappings; là nguồn authoritative cho inventory/count tại một thời điểm. | Draft mapping candidate; một `ZoneMapVersion` độc lập; dữ liệu tự hết hạn theo TTL. |
| **Disease Zone** | Polygon bệnh được đề xuất từ verified findings, review/correct và publish bởi assigned SystemManager. | Farm operational Zone; raw AI cluster; một region tự động official. |
| **Treatment Recommendation** | Catalogue entry immutable/versioned theo condition và health/severity, có expert/source provenance, disclaimer và effective window; official result chỉ tham chiếu version đã được manager chọn. | Free-form advice do AI hoặc manager nhập trực tiếp; nội dung pending tự hiển thị cho TenantOwner. |
| **Digital Plant Profile** | Lịch sử của một biological plant generation tại một pole position. Replacement kết thúc profile cũ và tạo PlantId mới. | Pole pricing unit bất biến qua mọi replacement; record bị overwrite khi thay cây. |
| **PlantInventoryChangeReport** | Owner report cho removal/replacement/new plant; chỉ thay đổi official inventory sau manager verification và evidence gate tương ứng. | API trực tiếp tạo/xóa Plant hoặc tự thay đổi confirmed count. |
| **MissionPurpose** | Mục đích readiness server-owned: `BaselineMapping`, `PlantHealth` hoặc `HarvestReadiness`; mỗi purpose có gate riêng. | Survey Service; client-supplied `IsReady`; một readiness rule yêu cầu payment cho mọi flight. |
| **V3 Ready Handoff** | Payload BE2 → BE1 chứa kết quả AI còn pending review, đầy đủ order/mission/boundary/map/job/model/policy provenance. | Kết quả đã approved/published; quyền cho TenantOwner xem raw AI output. |

## Quy tắc ngôn ngữ liên quan

- `SystemAdmin` và `SystemManager` là system actors, không cần chọn Tenant để đăng nhập/vận hành.
- Customer-side role mục tiêu là `TenantOwner`; tên `TenantAdmin`, `Member`, `Farm Manager` và `Worker` chỉ dùng khi nói về legacy data/code.
- “Base map” luôn là trạng thái ở cấp Farm; `ZoneMapVersion` chỉ là spatial slice của một `FarmBaseMapVersion`.
- “Published”, “official” và “verified” không được dùng cho AI output còn pending.
- “Harvest Readiness” chỉ là đánh giá qua dấu hiệu nhìn thấy; không bao gồm harvest record, sản lượng, batch hoặc hậu thu hoạch.
- `PricePerHa` và `ConfirmedSurveyAreaHa` chỉ là legacy compatibility fields; target flow không dùng để tính hoặc snapshot giá.
- “Ready” trong tên V3 handoff nghĩa là sẵn sàng để BE1 ingest/review, không có nghĩa business data đã được approve hoặc publish.
