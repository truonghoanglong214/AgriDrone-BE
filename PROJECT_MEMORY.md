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
| **V3-04** | **Mapping, Plant Identity & Spatial Lineage** | **[x] Done (Remediated)** | **Canonical Remediation theo `2026-11-16-v3-04-comprehensive-remediation.md` hoàn thành xuất sắc** |
| **V3-05** | **Combo review, invoice finalization, pole count** | **[ ] Planned** | Sẵn sàng triển khai dựa trên Authoritative `VerifiedBillablePoleCount` từ V3-04 |
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

## 4. Tóm tắt kết quả Phase V3-04 hoàn thành (Canonical Remediation)
- **Pipeline Duy Nhất Phê Duyệt Kỹ Thuật Nội Bộ (Zero "Published" Semantics)**:
  - Loại bỏ hoàn toàn các đường bypass: `PublishFarmBaseMapCommand`, `ApproveInternalBaseMapCommand`, `VerifyOrderBillablePolesCommand`, `IFarmBaseMapPublicationOrchestrator`.
  - Luồng chuẩn hóa duy nhất: Ingestion (`MappingCandidatesReadyV4`) → Manager review → `ApproveFarmBaseMapInternallyCommand` → `FarmBaseMapInternalApprovalOrchestrator` → Đóng băng bằng chứng `SurveyOrderPoleCountSnapshot` & `SurveyOrderPoleCountSnapshotItem` → Cập nhật `SurveyOrder` → Phát sự kiện Outbox `FarmBaseMapInternallyApprovedV1` → Missions consumer (`FarmBaseMapInternallyApprovedV1Handler`) hoàn tất technical mapping flight mission.
  - Tách bạch tuyệt đối: V3-04 chỉ duyệt kỹ thuật nội bộ (`InternalApproved`); phát hành cho khách hàng (`Published`) thuộc phạm vi V3-06.
- **Bản đồ số & Lineage Bất biến (`FarmBaseMapVersion` & `FarmBaselinePointer`)**:
  - Không còn setter lineage public; lineage được cố định bất biến ngay khi ingest qua factory `CreateFirstBaseline` và `CreateFollowUp`.
  - Hỗ trợ Follow-up Order tạo phiên bản mới kế thừa `BaselineMapVersionId` từ baseline pointer và trỏ `PredecessorVersionId` vào bản đồ trước đó, bảo toàn Baseline gốc.
  - Chống race condition tạo song song 2 baseline bằng row lock PostgreSQL `FOR UPDATE` trên `farm.farms` và DB unique constraint `uq_farm_baseline_pointers_farm`.
- **Dung sai Không gian Động (`SpatialPolicy` Fail-Closed)**:
  - Entity `SpatialPolicy` với seed `DEFAULT` cố định, StreamKey chuẩn hóa chữ hoa, fingerprint SHA-256 tính theo invariant culture bao gồm cả 4 tham số dung sai.
  - Fail-closed: truy vấn policy tại thời điểm khảo sát (`batch.ProducedAt`), trả `AppError.Conflict` nếu thiếu policy, tuyệt đối không fallback ngầm 1.5m. Toàn bộ 4 tham số tolerance được snapshot vào `MappingCandidateBatch`.
- **Định danh Trụ/Cây Trồng Bất biến (`PlantProfile` & `PlantChangeEvent`)**:
  - Tính khoảng cách dịch chuyển toạ độ theo công thức geodesic (Haversine/Great-circle) chuẩn mét SRID 4326 trước khi mutate entity. Từ chối duyệt nếu vượt tolerance.
  - Quan hệ 1-1 chặt chẽ: `Plant.PlantProfileId` đảm bảo mọi cây trong bản đồ đều liên kết với một profile.
  - Decision `Replace` bắt buộc có active profile, tạo profile thay thế kế thừa predecessor và ghi nhận `ManagerDecisionId`, retire cây cũ sang `Removed`.
  - Decision `MatchExisting` ghi nhận quan sát định danh lại vào `plant.plant_profile_observations`.
  - `PlantChangeEvent` bất biến với factory methods chuyên biệt (`CreatedWithLineage`, `Relocated`, `Replaced`), lưu vết đầy đủ lineage và evidence.
- **Bằng chứng Chốt Số trụ Tính tiền Authoritative Snapshot (`SurveyOrderPoleCountSnapshot`)**:
  - Bổ sung bảng con chi tiết `survey.survey_order_pole_count_snapshot_items` lưu vết từng candidate được review.
  - Fingerprint SHA-256 canonical (xác định, không chứa runtime timestamp/actor/random IDs).
  - Đếm `COUNT(DISTINCT resultant_plant_profile_id)` nằm strictly trong ranh giới PostGIS (`ST_Within`).
  - Hỗ trợ 0 trụ hợp lệ (valid zero count), loại bỏ fallback `billableCount == 0`.
  - `SurveyOrder` lưu `VerifiedPoleCountSnapshotId` và `VerifiedBillablePoleCount`, được cập nhật an toàn qua hai domain methods riêng biệt: `RecordInitialMapApproval` và `RecordFollowUpMapSnapshot`.
- **Database Migrations & Constraints Toàn Vẹn**:
  - Migration `20261117050302_V3_04CanonicalRemediationFinal` bổ sung enum value `INTERNAL_APPROVED` (`suppressTransaction: true`), tạo bảng snapshot items, backfill status `Published` → `Applied`, và seed DEFAULT `SpatialPolicy` 15 cột.
  - Ràng buộc composite unique keys / FKs ngăn chặn liên kết chéo giữa tenants, farms và batches.

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
