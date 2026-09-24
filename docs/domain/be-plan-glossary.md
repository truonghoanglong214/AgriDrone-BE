# Be-Plan Glossary

Status: **Approved**  
Effective date: 2026-09-22  
Source of truth: `Codex-Plan/Be-Plan.md`

Tài liệu này là glossary duy nhất cho workflow dịch vụ khảo sát. Tên trong code/API có thể dùng `PascalCase` hoặc mã `UPPER_SNAKE_CASE`, nhưng không được đổi nghĩa dưới đây.

| Thuật ngữ chuẩn | Định nghĩa bắt buộc | Không được hiểu là |
|---|---|---|
| **Public Applicant** | Người chưa đăng nhập gửi `NewCustomer` Survey Request và cung cấp snapshot liên hệ/Farm ban đầu. | TenantOwner đã được provision; người được phép tạo trực tiếp Tenant/Farm; actor vận hành drone. |
| **SystemAdmin** | System actor quản trị toàn hệ thống: thẩm định Survey Request, quản lý SystemManager, drone hệ thống, Survey Service/giá và master data. | TenantAdmin hoặc thành viên của một Tenant khách hàng. |
| **SystemManager** | Nhân sự vận hành của AgriDrone, có profile active, available và flight-qualified; chỉ thao tác Farm/Order được gán làm primary manager. | Farm Manager thuộc TenantMembership/FarmMembership; TenantOwner; worker của khách hàng. |
| **TenantOwner** | Chủ tài khoản khách hàng gắn với Tenant sau approval/invitation; xác nhận lịch, thanh toán và chỉ xem dữ liệu/kết quả đã publish của Tenant mình. | Người tự vận hành drone, duyệt AI thô hoặc tự tạo Mission. |
| **Survey Service** | Sản phẩm khảo sát được bán và định giá theo hectare. MVP chỉ có `PLANT_HEALTH` và `HARVEST_READINESS`. | Baseline Mapping; Follow-up; harvest/yield management. |
| **Baseline Mapping** | Mission vận hành bắt buộc, tự động thêm vào SurveyOrder đầu tiên khi Farm chưa có approved base map; tạo map nền và Digital Plant Profiles ban đầu sau human review. | Survey Service bán riêng; quy trình chạy lại ở mọi survey; map draft mà TenantOwner được xem. |
| **Follow-up** | Survey lặp lại trên cùng Farm và cùng Survey Service khi đã có lần completed tương thích; so sánh với lần compatible gần nhất. | Một Survey Service hoặc price item riêng; mọi survey sau Baseline Mapping bất kể service. |
| **Official Result** | Survey Result đã được SystemManager có thẩm quyền review/correct/approve và publish; đây là kết quả duy nhất TenantOwner được xem như kết luận chính thức. | AI output/prediction pending, draft map hoặc dữ liệu chưa publish. |

## Quy tắc ngôn ngữ liên quan

- `SystemAdmin` và `SystemManager` là system actors, không cần chọn Tenant để đăng nhập/vận hành.
- Customer-side role mục tiêu là `TenantOwner`; tên `TenantAdmin`, `Member`, `Farm Manager` và `Worker` chỉ dùng khi nói về legacy data/code.
- “Base map” luôn là trạng thái ở cấp Farm; `ZoneMapVersion` chỉ là spatial slice của một `FarmBaseMapVersion`.
- “Published”, “official” và “verified” không được dùng cho AI output còn pending.
- “Harvest Readiness” chỉ là đánh giá qua dấu hiệu nhìn thấy; không bao gồm harvest record, sản lượng, batch hoặc hậu thu hoạch.
