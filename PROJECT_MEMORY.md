# AgriDrone Project Memory Bank & Status Tracker

**Repository**: `D:\Project\ProjectMonHoc\DoAn\AgriDrone`  
**Current Active Branch**: `LongTH`  
**Last Updated**: 2026-11-16  
**Tech Stack**: .NET 10, C# 14, Clean Architecture, Entity Framework Core, PostgreSQL 17 + PostGIS 3.5, NetTopologySuite, RabbitMQ 4.2.7, Redis 8.8.1, MinIO.

---

## 1. Project Overview & Architecture
AgriDrone là hệ thống quản lý và điều phối dịch vụ khảo sát bay drone cho cây trồng (thanh long, cây ăn trái).
- **Kiến trúc**: Modular Monolith + Clean Architecture + Domain-Driven Design (DDD).
- **Modules cốt lõi**:
  - `AgriDrone.Modules.Surveys`: Quản lý yêu cầu, báo giá, chính sách giá, cọc, lịch hẹn, hợp đồng khảo sát, kết quả và hủy đơn.
  - `AgriDrone.Modules.Missions`: Quản lý drone, phi vụ bay (`DroneMission`), checklist kiểm tra an toàn (`Preflight`), tải media và xử lý AI.
  - `AgriDrone.Modules.Farms`: Quản lý ranh giới trang trại, các phiên bản bản đồ số (`FarmBaseMapVersion`).
  - `AgriDrone.Modules.Plants`: Quản lý định danh cây trồng (`PlantProfile`), biến động cây trồng (`PlantChangeEvent`), phát hiện bệnh (`DiseaseObservation`).
  - `AgriDrone.Modules.Identity`: Quản lý phân quyền, phân công quản lý trang trại (`FarmManagerAssignment`).
  - `AgriDrone.Database`: Shared DbContext (`AgriDroneSchemaDbContext`), điều phối viên nghiệp vụ (`SurveyCaptureExecutionCoordinator`), migrations, outbox processor.

---

## 2. Master Plan Phase Tracker (BE1)

| Phase | Phạm vi nghiệp vụ | Trạng thái | Ghi chú & Exit Gate |
|---|---|---|---|
| **V3-00** | Business rules, traceability, documentation | **[x] Done** | Design locked, ADR 0019/0020 |
| **V3-01** | Policy model, contracts, scope separation | **[x] Done** | Tách MAP/COMBO, First/Follow-up |
| **V3-02** | Catalogue, shared deposit, approval snapshots | **[x] Done** | Published price revisions, deposit policies |
| **V3-03** | Capture execution, funding, re-flight, cancellation | **[x] Done (Code)** | Hoàn thành `03A` → `03H-2` (Commit `16210b3`), `03H-3` handoff |
| **V3-04** | **Mapping, Plant Identity & Spatial Lineage** | **[→] NEXT** | **Đang chuẩn bị triển khai** (Xem mục 4) |
| **V3-05** | Combo review, invoice finalization, pole count | **[ ] Planned** | Billable count, InvoiceRevision |
| **V3-06** | Settlement, atomic release, customer notice | **[ ] Planned** | Release proof, financial facts |
| **V3-07** | Owner portal, media viewer, comparison | **[ ] Planned** | Historical comparison UI |
| **V3-08** | Claims, refund execution, post-release correction | **[ ] Planned** | Transfer proofs, complaint window |
| **V3-09** | Financial reporting, revenue dashboard, Excel | **[ ] Planned** | Ledger reconciliation |
| **V3-10** | **Comprehensive Acceptance, Cutover & Release** | **[ ] Final Gate** | **Toàn bộ Integration Test Suite chạy tập trung tại đây** |

---

## 3. Tóm tắt kết quả Phase V3-03 vừa hoàn thành (Commit `16210b3`)
- **Điều phối viên bay (`SurveyCaptureExecutionCoordinator`)**:
  - Quản lý vòng đời `CaptureAttempt`: `Planned` → `InFlight` → `AwaitingUpload` → `Processing` → `Accepted` / `ReflightRequired`.
  - Checksum & Media thật: `FreezeInputManifestAsync` đọc trực tiếp `MissionMedia`, kiểm tra SHA-256 (64 hex), dung lượng > 0, xác thực với `IObjectStorage.GetInfoAsync`.
  - Bay lại chất lượng (Quality Re-flight): Tạo successor attempt cùng order, không thu thêm cọc.
- **Quy trình Thanh toán & Đảo cọc (`ProcessSurveyPaymentEventCommandHandler`)**:
  - Hỗ trợ First vs Follow-up × MAP vs COMBO.
  - Xử lý hoàn trả/chargeback (`SurveyFundingReversal`): ghi nhận fact đảo tiền bất biến, giảm cọc trên `SurveyDepositObligation`, thu hồi readiness về `AwaitingDeposit` nếu chưa bay.
- **Hủy đơn & Hoàn cọc (Cancellation & Refund Cap)**:
  - Phân loại: CustomerBeforeCapture, CustomerAfterAcceptableCapture (yêu cầu Admin + bằng chứng kỹ thuật), OperationalPostponement, ServiceFailure (hoàn 100%).
  - Refund Cap: Khấu trừ `completedRefunds` và `activeReservations`, ngăn chặn hoàn vượt receipt (`REFUND_RECONCILIATION_HOLD`).
  - No-Show: Đối chiếu hạn mức `AllowedFreeNoShowReschedules` (dưới hạn mức -> FreeReschedule, vượt hạn mức -> AdminReviewRequired).
- **Chống xung đột đa luồng & TOCTOU**:
  - Row lock PostgreSQL `FOR SHARE` cho assignment, `FOR UPDATE` cho cancel và revoke handler.
  - Chặn hủy phân công khi có chuyến bay đang thực hiện (`ACTIVE_CAPTURE_IN_FLIGHT`).

---

## 4. Kế hoạch & Nhiệm vụ tiếp theo: Phase V3-04 (Mapping & Plant Identity)
Trọng tâm công việc Phase V3-04:
1. **Quản lý Bản đồ số (`FarmBaseMapVersion`)**:
   - First Order tạo bản đồ Baseline đầu tiên (nội bộ).
   - Follow-up Order tạo phiên bản mới trỏ `PredecessorVersionId` (bảo toàn Baseline cũ, không ghi đè).
2. **Định danh Cây trồng bất biến (`PlantProfile`)**:
   - Cấp `PlantProfileId` vĩnh viễn cho từng trụ trên bản đồ Baseline.
   - Mọi biến động (thêm mới, dịch chuyển toạ độ do sai số GPS/GNSS, thay thế, hủy) ghi nhận vào `PlantChangeEvent` bất biến kèm quyết định duyệt của Manager.
3. **Tính số trụ tính tiền (`VerifiedBillablePoleCount`)**:
   - Lọc và đếm số trụ nằm trong ranh giới trang trại (`in-boundary`) làm căn cứ lập hóa đơn ở V3-05.

---

## 5. Nguyên tắc Vận hành Bắt buộc cho AI Agent
1. **KHÔNG CHẠY TEST TÍCH HỢP TRONG CÁC PHASE CODE (V3-01 -> V3-09)**:
   - Nghiêm cấm chạy `dotnet test` với PostgreSQL container / integration test suites vì gây treo máy và tốn thời gian.
   - Toàn bộ test nghiệm thu tích hợp sẽ chạy tập trung ở **Phase V3-10**.
   - Chỉ được phép chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo` để kiểm tra biên dịch (thời gian ~5–8s).
2. **KẾ HOẠCH TRƯỚC KHI CODE (Superpowers: writing-plans)**:
   - Luôn lập checklist và xác định rõ file cần sửa/tạo mới trước khi chạm vào mã nguồn.
3. **BẢO TỒN DỮ LIỆU & BẤT BIẾN (Immutability)**:
   - Không UPDATE đè lên các bản ghi lịch sử, receipt, snapshot hoặc sự kiện domain.
   - Dùng Result pattern (`Result<T>`), không throw exception để điều khiển luồng nghiệp vụ.
