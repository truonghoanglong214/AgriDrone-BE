# AgriDrone — Agent Operating Guidelines & Project Instructions

> **Mục tiêu**: Hướng dẫn cho mọi AI Assistant / Agent tham gia phát triển dự án AgriDrone.  
> **Nguyên tắc cốt lõi**: Luôn đọc kỹ tài liệu này và `PROJECT_MEMORY.md` trước khi thực hiện bất kỳ tác vụ nào.

---

## 1. Project Memory & Context
Trước khi bắt đầu bất kỳ tác vụ nào, Agent **BẮT BUỘC** đọc file:
👉 [PROJECT_MEMORY.md](file:///D:/Project/ProjectMonHoc/DoAn/AgriDrone/PROJECT_MEMORY.md)

File này chứa:
- Trạng thái hiện tại của dự án và các phase đã hoàn thành.
- Công việc cụ thể đang làm và nhiệm vụ tiếp theo (Next tasks).
- Nhánh làm việc (`LongTH`) và các commit gần nhất.

---

## 2. Superpowers Workflow & Planning
Dự án áp dụng chặt chẽ workflow từ plugin **Superpowers**:

1. **Lập Kế hoạch Trước Khi Code (`writing-plans`)**:
   - Trước khi sửa hoặc viết code mới cho bất kỳ feature nào, Agent phải lập danh sách các bước rõ ràng (files cần tạo/sửa, logic xử lý, kiểm tra build).
   - Không được nhảy vào code ngay mà không có kế hoạch.
2. **Thực thi Kế hoạch Tuần tự (`executing-plans`)**:
   - Hoàn thành từng bước, cập nhật tiến độ cho người dùng.
3. **Phân tích Vấn đề bài bản (`systematic-debugging`)**:
   - Khi gặp lỗi biên dịch hoặc logic, tìm nguyên nhân gốc rễ (root cause) thay vì sửa mò hoặc đoán mò.

---

## 3. Quy định BẮT BUỘC về Kiểm thử & Biên dịch (CRITICAL)

> [!CAUTION]
> **TUYỆT ĐỐI KHÔNG TỰ Ý CHẠY `dotnet test` TRONG CÁC PHASE CODE (V3-01 ĐẾN V3-09)!**

- **Lý do**: Bộ Integration Tests chạy trên Docker PostgreSQL + PostGIS thực tế với nhiều thread barrier races, rất nặng và ngốn tài nguyên, gây đơ máy và nghẽn hệ thống của người dùng.
- **Quy định của dự án**:
  - Toàn bộ Integration Test Suite và Acceptance Test sẽ được chạy **TẬP TRUNG TẠI PHASE V3-10** (Phase cuối cùng theo Master Plan).
  - Trong các phase phát triển tính năng, Agent **CHỈ ĐƯỢC PHÉP CHẠY**:
    ```powershell
    dotnet build backend/AgriDrone.sln --no-restore --nologo
    ```
    (Lệnh này chạy rất nhanh ~5–8 giây, đảm bảo code biên dịch sạch 100%, 0 Warning, 0 Error mà không làm treo máy).

---

## 4. Chuẩn mực Lập trình Backend (C# / .NET 10)
1. **Kiến trúc Clean Architecture & Modular Monolith**:
   - Modules: `Surveys`, `Missions`, `Farms`, `Plants`, `Identity`.
   - Domain logic nằm trong Domain Entities / Value Objects; Application Handlers (MediatR) điều phối nghiệp vụ.
   - Không vi phạm boundary giữa các module (tuân thủ Architecture Tests).
2. **Result Pattern**:
   - Dùng `Result` / `Result<T>` và `AppError` để trả về lỗi nghiệp vụ. Không dùng Exception để điều khiển luồng logic.
3. **Bất biến & Truy vết Lịch sử (Immutability & Audit)**:
   - Các bản ghi tài chính (`SurveyFundingReceipt`, `SurveyFundingReversal`), trạng thái (`OrderStateHistory`), quyết định hủy (`SurveyCancellationDecision`) là bất biến (append-only), tuyệt đối không update đè.
4. **Cờ an toàn (Fail-closed)**:
   - Luôn giữ `PublicIntakeEnabled = false` và `SurveyPayments:Enabled = false` trong cấu hình môi trường phát triển / production cho đến khi Phase V3-10 được nghiệm thu.
