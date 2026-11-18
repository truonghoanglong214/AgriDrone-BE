# AgriDrone Project Memory Bank & Status Tracker

**Repository**: `D:\Project\ProjectMonHoc\DoAn\AgriDrone`  
**Current Active Branch**: `LongTH`  
**Last Updated**: 2026-11-17
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
| **V3-04** | **Mapping, Plant Identity & Spatial Lineage** | **[~] Remediation In Progress** | **Code/build đã sạch 0W/0E; G0 migration-history và G14 migration-scope chưa có bằng chứng đóng; Tests deferred to V3-10** |
| **V3-05** | **Combo review, invoice finalization, pole count** | **[ ] Planned** | Chỉ bắt đầu sau khi đóng đủ exit gates V3-04 |
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

## 4. Tiến độ Phase V3-04 (Canonical Remediation V2 — In Progress)
> **Trạng thái**: Phần code nghiệp vụ đã được khắc phục và build sạch, nhưng chưa được phép ghi Done vì chưa có bằng chứng `__EFMigrationsHistory` để chọn nhánh migration và migration hiện tại còn lẫn thay đổi ngoài V3-04.
> **Nguyên tắc**: Tests NOT RUN (dành tập trung cho V3-10); `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 Warning, 0 Error.

Các hạng mục đã hoàn thành toàn diện:
1. **Migration Gate M0 — Chưa đóng**:
   - Chưa có kết quả `__EFMigrationsHistory` từ các database liên quan, vì vậy chưa được tự chọn Nhánh A hoặc Nhánh B.
   - Migration `20261117050302` vẫn chứa pricing/appointment/post-flight ngoài scope V3-04; phải tách theo đúng phase sau khi có quyết định M0.
2. **Provenance & Metadata Thật**:
   - `MissionPlantGpsPublisher` đọc metadata từ `AiProcessingJob` hoàn thành, sinh deterministic candidate UUIDs v5 từ `jobId + treeId`, loại bỏ mọi GUID / timestamp ngẫu nhiên (`Guid.NewGuid()`, `UtcNow`).
3. **CaptureAttemptId Xuyên suốt**:
   - Lưu `CaptureAttemptId` trong `MappingCandidateBatch`, xác thực đúng order/mission/boundary, truyền sang `SurveyOrderPoleCountSnapshot`, `FarmBaseMapInternallyApprovedV1`, và `FarmBaseMapInternallyApprovedV1Handler`.
4. **Phân định rõ `PlantId` và `PlantProfileId`**:
   - `FarmBaseMapInternalApprovalOrchestrator` tra cứu đúng thứ tự: `targetPlantId` → `Plant` → `targetProfileId` → `PlantProfile`. Không nhầm ID giữa 2 aggregate.
5. **Spatial Policy & True Geodesic Boundary**:
   - `GeodesicSpatialCalculator` và PostGIS geography tính toán mét thực, initial bearing degrees. Tiêu thụ đủ 4 trường tolerances; fails closed nếu thiếu policy hoặc tolerance <= 0.
6. **Xóa Pipeline V3 Khỏi Runtime**:
   - Xóa bỏ hoàn toàn `BaselineMappingCandidatesReadyV3Handler` và `BaselineMappingCandidatesReadyV3Processor` khỏi DI và codebase.
7. **Khóa DB Invariants & Restrict FKs**:
   - Check constraint `ck_farm_base_map_versions_lineage`, `ck_survey_order_pole_count_snapshot_items_acceptance`.
   - Unique filtered index `(tenant_id, farm_id)` cho baseline (`is_baseline=true`) và draft (`status='DRAFT'`).
   - Unique filtered index `uq_plants_farm_profile` trên `(farm_id, plant_profile_id)`.
   - Restrict toàn bộ FK snapshot evidence (`MappingCandidateBatch`, `PlantProfile`, `MappingCandidate`).
8. **Bảo toàn Semantics Nội bộ (Zero Published Trong V3-04)**:
   - `FarmBaseMapInternalApprovalOrchestrator` chỉ phê duyệt nội bộ (`InternalApproved`), không gán các trường mang nghĩa Published.
   - `DroneMission` ghi nhận `InternallyApprovedMapVersionId` và `MapInternallyApprovedAt` với outcome-based method (`DroneMissionApprovalOutcome`), không chạm `PublishedMapVersionId`/`MapPublishedAt`.

---

## 5. Kế hoạch & Nhiệm vụ tiếp theo: Phase V3-05 (Combo Review, Invoice Finalization, Pole Count)
Trọng tâm công việc Phase V3-05:
1. **Rà soát Combo Đa dịch vụ (Combo Review Workflow)**:
   - Tổng hợp kết quả bay định kỳ và kết quả khảo sát sức khỏe cây trồng theo đợt.
   - Đối chiếu số trụ phát hiện với số trụ đã chốt tại V3-04.
2. **Khóa Hóa đơn & Bảng giá Cuối cùng (`InvoiceFinalization` / `InvoiceRevision`)**:
   - Tính toán `FinalPrice = VerifiedBillablePoleCount * PricePerPoleSnapshot * (1 - ComboDiscountPercentage)`.
   - Lập biên bản quyết toán, trừ tiền cọc (`SurveyFundingReceipt`), xác định số dư còn phải trả (`RemainingAmount`).
3. **Quản lý Bất biến & Không ngoại lệ (Fail-closed)**:
   - Đảm bảo snapshot thương mại bất biến trước khi phát hành hóa đơn cho chủ vườn.

---

## 6. Nguyên tắc Vận hành Bắt buộc cho AI Agent
1. **KHÔNG CHẠY TEST TÍCH HỢP TRONG CÁC PHASE CODE (V3-01 -> V3-09)**:
   - Nghiêm cấm chạy `dotnet test` với PostgreSQL container / integration test suites vì gây treo máy và tốn thời gian.
   - Toàn bộ test nghiệm thu tích hợp sẽ chạy tập trung ở **Phase V3-10**.
   - Chỉ được phép chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo` để kiểm tra biên dịch (thời gian ~5–8s).
2. **KẾ HOẠCH TRƯỚC KHI CODE (Superpowers: writing-plans)**:
   - Luôn lập checklist và xác định rõ file cần sửa/tạo mới trước khi chạm vào mã nguồn.
3. **BẢO TỒN DỮ LIỆU & BẤT BIẾN (Immutability)**:
   - Không UPDATE đè lên các bản ghi lịch sử, receipt, snapshot hoặc sự kiện domain.
   - Dùng Result pattern (`Result<T>`), không throw exception để điều khiển luồng nghiệp vụ.
