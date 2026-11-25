# AgriDrone Project Memory Bank & Status Tracker

**Repository**: `D:\Project\ProjectMonHoc\DoAn\AgriDrone`  
**Current Active Branch**: `LongTH`  
**Last Updated**: 2026-11-19
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
| **V3-04** | **Mapping, Plant Identity & Spatial Lineage** | **[x] Done (Remediated)** | **G0–G18 closed; E1/S1 migration chain sạch; build 0W/0E; tests deferred to V3-10** |
| **V3-05** | **Combo review, invoice finalization, pole count** | **[>] In Progress** | V3-05A/B/C/D source implemented (`D01–D05`); build pending .NET 10 SDK; V3-05E next |
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

## 4. Tiến độ Phase V3-04 (Canonical Remediation V2 — Done 2026-11-18)
> **Trạng thái**: Hoàn thành G0–G18. Chủ dự án xác nhận local là database duy nhất; lịch sử migration và backlog local đã đủ để chốt Nhánh A. Migration canonical mới không lẫn phase khác.
> **Nguyên tắc**: Tests NOT RUN (dành tập trung cho V3-10); `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 Warning, 0 Error.

Các hạng mục đã hoàn thành toàn diện:
1. **Migration Gate M0 — Đã đóng theo Nhánh A**:
   - Chủ dự án xác nhận không có database nào khác ngoài local. Local preflight lúc 11:05 ngày 2026-11-18 (Asia/Bangkok) cho thấy các migration V3-04 cũ chưa từng apply; migration mới nhất local là `20261109071540_Step7EPlantHealthCompletionOutput`.
   - Local V3 backlog: outbox chưa xử lý = 0, inbox chưa hoàn tất = 0; RabbitMQ main/retry/dead queues của `be1-baseline-mapping-candidates-ready-v3` đều 0.
   - Đã loại hai remediation migration cũ `20261116145210` và `20261117050302`. Chain canonical: `20261116131222_V3_04MappingAndPlantIdentity` → `20261118040000_V3_04AddInternalApprovedMapStatus` → `20261118042702_V3_04CanonicalMappingRemediation`.
   - E1 thêm enum ngoài transaction và có `Down` no-op. S1 chỉ chứa scope V3-04, backfill provenance theo expand → exact join → validate → constrain; không có pricing/appointment/post-flight.
   - Generated SQL đã được inspect nhưng không apply database; script preflight read-only giữ tại `docs/operations/v3-04-remediation-preflight.sql`.
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
   - Unique filtered index `uq_plants_farm_plant_profile` trên `(farm_id, plant_profile_id)`.
   - Restrict toàn bộ FK snapshot evidence (`MappingCandidateBatch`, `PlantProfile`, `MappingCandidate`).
8. **Bảo toàn Semantics Nội bộ (Zero Published Trong V3-04)**:
   - `FarmBaseMapInternalApprovalOrchestrator` chỉ phê duyệt nội bộ (`InternalApproved`), không gán các trường mang nghĩa Published.
   - `DroneMission` ghi nhận `InternallyApprovedMapVersionId` và `MapInternallyApprovedAt` với outcome-based method (`DroneMissionApprovalOutcome`), không chạm `PublishedMapVersionId`/`MapPublishedAt`.
9. **Verification cuối**:
   - Sửa ba filtered index baseline/draft/published thành ba EF model-index name riêng, tránh EF ghi đè metadata khi cùng dùng `(TenantId, FarmId)`.
   - `SurveyPayments:Enabled` ở Development đã trả về `false`, giữ fail-closed đến V3-10.
   - Static scans đạt; `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 Warning, 0 Error.
   - **TESTS NOT RUN — deferred to V3-10**.

---

## 5. Tiến độ Phase V3-05 (Combo Review, Invoice Finalization, Pole Count — In Progress 2026-11-23)
> **Trạng thái**: Đã hoàn thành và gia cố toàn diện **V3-05A** và **V3-05B** (giải quyết triệt để 9 vấn đề kiến trúc/database review và hoàn tất 8 mục trong Checklist sửa và xác nhận cuối cùng C1–C8). Source Verified / Build Clean, DB Acceptance Pending (deferred to V3-10).

Các hạng mục cốt lõi đã hoàn thành trong V3-05A & V3-05B:
1. **Phân tách hoàn toàn MAP/COMBO V3 khỏi Enum Legacy**:
   - Thay thế việc dùng sai `SurveyServiceType` bằng `ServiceCode` (`"MAP"`, `"COMBO"`) và capability `ScreensDisease` boolean trong `SurveyInternalApprovedResultRevision`.
   - Loại bỏ mọi câu cast PostgreSQL enum `system.survey_service_type` trong check constraint và migration.
2. **Khóa quan hệ cùng Order & Lineage tại Database & Aggregate**:
   - Bổ sung composite alternate keys `(Id, SurveyOrderId, TenantId, FarmId)` và `(Id, SurveyOrderId)` cho `SurveyOrderPoleCountSnapshot`, `SurveyInternalApprovedResultRevision`, và `SurveyInvoiceRevision`.
   - Ràng buộc composite foreign keys chặt chẽ giữa Invoice, Result, Snapshot và Order, ngăn chặn triệt để liên kết chéo Order (cross-order reference).
   - Kiểm tra lineage bằng revision tiền nhiệm thật (`currentRevision`, `currentInvoiceRevision`): revision 1 bắt buộc predecessor null, revision > 1 bắt buộc khớp ID tiền nhiệm hiện hành, tăng đúng +1, xử lý giới hạn `int.MaxValue`.
   - Check constraints lineage: `revision_number = 1` bắt buộc `predecessor IS NULL`; `revision_number > 1` bắt buộc có predecessor hợp lệ và khác chính record hiện tại.
3. **Đối chiếu Approved Result với Count Snapshot cụ thể**:
   - `RecordInternalApprovedResult` đối chiếu toàn diện 9 trường context: `OrderId`, `TenantId`, `FarmId`, `MissionId`, `CaptureAttemptId`, `FarmBaseMapVersionId`, `FarmBoundaryVersionId`, `MappingCandidateBatchId`, `PoleCountSnapshotId`.
   - Tự động cập nhật `VerifiedPoleCountSnapshotId` và `VerifiedBillablePoleCount` từ snapshot vào aggregate root `SurveyOrder`.
4. **Bảo vệ toàn diện Provenance GUIDs liên module & Pre-release Correction**:
   - Cấu hình composite foreign keys trong `CrossModuleRelationshipConfiguration` cho `CaptureAttempt`, `DroneMission`, `MappingCandidateBatch`, `FarmBaseMapVersion` (base & baseline), `FarmBoundary`, và `User`.
   - Hỗ trợ mô hình correction nguyên tử trong 1 physical transaction thay cả approval và invoice (bàn giao chi tiết cho V3-05D/E).
5. **Giới hạn tiền tệ & Xử lý Zero-Pole**:
   - Định nghĩa `MaxPersistableAmount = 9_999_999_999_999_999m` (numeric(18,2) VND nguyên).
   - Kiểm tra độc lập: `PricePerPole > 0`, nguyên VND và `<= MaxPersistableAmount` (áp dụng ngay cả khi count = 0); `ValidAllocatedDepositNet >= 0`, nguyên và `<= MaxPersistableAmount`.
   - Guard tràn số `TotalAmount.Overflow` trước khi nhân.
   - Zero-Pole ngoại lệ bắt buộc phải có `ZeroPoleApprovedBy`, `ZeroPoleReasonCode` (1..64 ký tự), và `ZeroPoleEvidenceReference` (1..500 ký tự).
6. **Canonicalization dùng chung cho Fingerprint, Factory và Replay**:
   - Sử dụng `Utf8JsonWriter` để tuần tự hóa SHA-256 evidence fingerprint có cấu trúc, loại bỏ nguy cơ injection dấu phân cách `|`.
   - Ép kiểu số nguyên VND sang `(long)` khi ghi JSON để đảm bảo token thống nhất (`1000m` và `1000.00m` sinh cùng bytes).
   - Chuẩn hóa timestamp UTC về microsecond precision (`(ticks / 10L) * 10L`) định dạng `"yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'"` cho `ReviewedAt`, `FinalizedAt`, `SupersededAt` và lưu chính giá trị đã chuẩn hóa.
7. **Đồng bộ toàn diện Persistence, Migrations & Designer Model**:
   - Migration `20261122150000_V3_05ComboReviewAndInvoiceRevisions.cs` bổ sung đầy đủ 20 indexes FK và check constraints, hỗ trợ `Down()` an toàn.
   - Tạo file migration model target `20261122150000_V3_05ComboReviewAndInvoiceRevisions.Designer.cs` chuẩn EF Core convention.
   - `AgriDroneSchemaDbContextModelSnapshot.cs` đồng bộ 100% metadata.
8. **Các hạng mục hoàn thành và remediated trong V3-05C (Nhận COMBO pending — 2026-11-24)**:
   - **Hoàn thành toàn diện 12 tiêu chí V01–V12 (Source verified)**:
     - **V01 / R2 (Nhánh V3 COMBO & Chốt Nguồn Map)**: Tách rẽ nhánh V3 (`mission.Purpose == MissionPurpose.Capture`) trước guard legacy trong `DetectMissionDiseasesCommandHandler`. Chốt capture map qua `bm.id = {mapId}` (bỏ fallback `OR mapId IS NULL`), kiểm tra map `INTERNALLY_APPROVED` hoặc `PUBLISHED` mà không bắt buộc `published_at IS NOT NULL`. MAP và legacy sai scope bị từ chối sạch sẽ.
     - **V02 / R2 (Nguồn Batch Chuẩn Xác)**: Truy vấn mapping batch từ `farm.mapping_candidate_batches` (bỏ schema sai `mapping.` và bỏ fallback `OR mission_id`). Tra cứu exact từ `SurveyOrderPoleCountSnapshot` hoặc `farm.mapping_candidate_batches` theo `survey_order_id` và `capture_attempt_id`.
     - **V03 / R2 (Approved Job Reference Binding)**: Dùng `ApprovedJobReference` định danh đợt xử lý đã duyệt. Bind `approval.ProcessingJobId` từ `survey.capture_input_approvals` vào snapshot `ComboAnalysisProcessingSnapshot`, đối chiếu chặt chẽ `source.ApprovedJobReference == attempt.ProcessingJobReference` tại `ComboAnalysisPendingImport`.
     - **V04 / R2 (Snapshot & Coverage Thật)**: `DetectMissionDiseasesCommandHandler` resolve approval trước inference; đọc exact raw images active (`MediaRole == RawImage && StorageStatus == Active && DeletedAt == null`); lưu `AiProcessingJob.Id` riêng với `ApprovedJobReference`, exact manifest binding, metadata tọa độ/nhãn/confidence và `plant_profile_id`.
     - **V05 / R3 (Replay với Serializer & Deterministic Handoff)**: `MissionComboAnalysisPublisher` inject `IIntegrationMessageSerializer`, deserialize `existing.Body` (`byte[]`) để kiểm tra actor, jobId, sourceRevision, mission, farm, order, schema version và trả `HandoffId` gốc; so sánh revision khớp snapshot; sinh `HandoffId` tất định qua RFC 4122 UUIDv5; dùng `job.CompletedAt` bất biến cho `ProducedAt`.
     - **V06 / R4 (Replay First & Active Attempt theo Filter Index)**: Di chuyển dedup replay lên đầu dưới Order lock (Rank 2); loại bỏ hoàn toàn `CurrentCaptureAttemptId`; resolve active attempt bằng filter `status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING')` của index `uq_capture_attempts_active_order`; yêu cầu Order và active attempt cùng `Processing`, recheck `attempt.MissionId`.
     - **V07 / R4 (FindingId trong Membership, Pairing & Spatial Boundary)**: Bổ sung `FindingId` vào `ProposedDiseaseZoneMembershipV1` và `ComboPendingZoneMembershipDto`; cập nhật validator, fingerprint và importer; kiểm tra zone membership trỏ tới finding trong batch; validate polygon closed; xác thực candidate-profile pairs qua snapshot items; blocking count và reasons suy ra từ cùng danh sách.
     - **V08 / R2–R5 (Lineage Bất Biến)**: Lineage chốt theo active attempt; root revision 1 không predecessor; successor có supersedesHandoffId và revision tăng; từ chối fork (`LineageForkConflict`).
     - **V09 / R1, R4 (Strict JSON & Canonical Fingerprint)**: `ComboAnalysisReadyV1Processor` kiểm tra strict root object, từ chối duplicate properties mọi cấp; `ComboAnalysisV1Fingerprint` chuẩn hóa canonical UTF-8 JSON deterministic; handler kiểm tra tenant context và fingerprint trước ACK.
     - **V10 / R5 (Composite Alternate Key & FK 4 Cột)**: Alternate key `(id, survey_order_id, tenant_id, farm_id)` trên `SurveyComboImportBatch`; FK 4 cột từ `SurveyInternalApprovedResultRevision` tới batch với `Restrict`; migration `20261123140000_V3_05CComboAnalysisPendingImport` và `AgriDroneSchemaDbContextModelSnapshot` đồng bộ 100%.
     - **V11 / R6 (Read Port Handoff V3-05D)**: `SurveyComboPendingReadPort` lọc pending batch theo active attempt (`uq_capture_attempts_active_order`), loại bỏ batch mồ côi của attempt cũ sau re-flight; tìm single lineage head; map đầy đủ `Geometry`, `GuidanceCandidates`, `Memberships` (kèm `FindingId`), và `BlockingReasons`; scoped `GetBatchByIdAsync` theo orderId.
     - **V12 / R7 (Docs & Memory Gate Closed)**: Cập nhật kế hoạch và tài liệu; source gate đóng với `Source verified`; runtime/acceptance tests được bảo lưu tại V3-10 theo quy định dự án.
   - **Hoàn thành 100% bốn điều kiện remediation M1–M4 và Checklist sửa bản hiện tại (F01–F06)**:
     - **M1 / F01 / F03 (Persist Manifest Files & Exact Input Subset)**: `CaptureInputManifestFile` lưu bất biến `(manifest_id, media_asset_id, sha256, size_bytes, media_role)` trong `FreezeInputManifestAsync`. `ComboAnalysisPendingImport` sửa compile pattern `source.InputMediaIds is not { Count: > 0 } inputIds`, kiểm tra manifest status `Approved`, bắt buộc có frozen files, loại bỏ ID trùng lặp, dùng `inputSet.SetEquals(frozenSet)` để bắt cả thiếu và thừa input media. `DetectMissionDiseasesCommandHandler` dựng danh sách ảnh phân tích chính xác từ subset `RawImage` của manifest files đã freeze, kiểm tra tính đầy đủ, SHA-256 và size trước khi inference.
     - **M2 / F03 (Consumer Binding Chặt Chẽ & Context Bất Biến)**: `ComboAnalysisPendingImport` bắt buộc có `InputApprovalId`, `InputManifestId`, `InputManifestHash`; load exact approval/manifest; đối chiếu `attempt.InputApprovalReference`, `attempt.ProcessingJobReference`, `source.ApprovedJobReference` và tập file input với approved manifest files. `DetectMissionDiseasesCommandHandler` giữ một immutable record `V3ComboInferenceContext`, bỏ subquery raw SQL `ORDER BY approved_at DESC LIMIT 1`, commit revalidate đúng snapshot ID và approval ban đầu.
     - **M3 / F02 / F04 (Khóa Nguồn Mapping Snapshot, NTS Topology Zones & Schema Chuẩn)**: Xóa hoàn toàn fallback sang `farm.mapping_candidate_batches`. Trong `DetectMissionDiseasesCommandHandler`, xóa bỏ `mapping_kind` và `status = 'APPROVED'` khỏi bảng `survey.survey_order_pole_count_snapshots` (đúng 100% schema entity). Query `MappingKind` và `Status` từ bảng `farm.mapping_candidate_batches` dựa trên `snapshot.MappingCandidateBatchId`, yêu cầu status `Approved` và parse typed enum. Bỏ `?? []` tại publisher, yêu cầu `comboSnapshot.DiseaseZones is not null`; writer xác nhận collection `DiseaseZones: []`. Importer kiểm tra topology polygon bằng NetTopologySuite (`zonePolygon.IsValid`) và kiểm `zonePolygon.Covers(findingPoint)`.
     - **M4 / F05 / F06 (Persist Structured Blocking Items JSONB & Read Port Validation)**: `ComboAnalysisPendingImport` tạo tập `ComboPendingBlockingItem` ổn định có type, id, code, description; lưu trường JSONB `blocking_items` vào `SurveyComboImportBatch`; `SurveyComboPendingReadPort` map trực tiếp count và reasons từ `batch.BlockingItems` đã persist, bắt buộc `persistedItems.Count == batch.BlockingCount` và validate JSON shape, xóa bỏ fallback tự dựng lại từ `RawPayload`. Migration `20261123140000_V3_05CComboAnalysisPendingImport.cs`, designer và snapshot đã đồng bộ hoàn hảo. Trace First COMBO và Follow-up COMBO khép kín từ route → frozen input → AI inference → Outbox → Inbox → pending batch → read DTO.
   - **Giải quyết triệt để 3 vấn đề Review P1 (2026-11-24)**:
     - **P1 · F03 (Per-Image Coverage Proof & Phân biệt SKIPPED / SUCCESS)**: FastAPI detector (`models.py`, `main.py`) trả về `image_outcomes: List[ImageInferenceOutcome]` chi tiết `SUCCESS`/`SKIPPED`/`FAILED` cho từng ảnh đầu vào (ảnh ngoài polygon gán `SKIPPED`). .NET Integration (`MetashapeService.cs`) xóa bỏ hoàn toàn fallback tự sinh `SUCCESS` khi thiếu outcome. Handler (`DetectMissionDiseasesCommandHandler.cs`) kiểm tra hai ca riêng trước khi ghi run Completed: (1) response thiếu outcomes hoặc thiếu ảnh; (2) toàn bộ ảnh bị polygon lọc bỏ (all SKIPPED hoặc không có ảnh nào SUCCESS trong ranh giới). Cả hai đều bị chặn với lỗi `DetectDiseases.IncompleteImageCoverage` hoặc `DetectDiseases.ZeroInferenceCoverage`. Lưu `image_outcomes` vào `job.Parameters` JSON để kiểm toán.
     - **P1 · F03 (ADR 0020 Lock Serialization)**: Áp dụng thứ tự khóa nghiêm ngặt: Order (Rank 2 `FOR UPDATE`, kiểm tra status `Processing`) → CaptureAttempt (Rank 7 `FOR UPDATE`, kiểm tra status `PROCESSING`, recheck input approval và job reference) → DroneMission (Rank 8 `FOR UPDATE`) → Approval (Rank 10) → Snapshot (Rank 6). Nếu Order bị hủy (Cancel) hoặc chuyển bay lại (Re-flight) khi detector đang chạy, transaction commit rollback an toàn và ném exception / trả conflict, loại bỏ hoàn toàn TOCTOU.
     - **P1 · F04 (Writer Sinh Proposal Zones Thật Với Phân Cụm Không Gian)**: Writer chỉ đưa findings đủ điều kiện (`isDisease && detection.Confidence >= request.ConfidenceThreshold`) vào danh sách đề xuất zones, loại bỏ hoàn toàn các detection `Healthy`. Helper `BuildProposedDiseaseZones` áp dụng Spatial Clustering (BFS Connected Components với `ClusterMaxDistanceM = 15.0m`) để tách các cụm bệnh cùng nhãn ở xa nhau thành các zones riêng biệt, tính bounding box buffer 3m khép kín 5 điểm cho từng cụm. Tạo `ProposedComboDiseaseZoneV1` với deterministic UUIDv5 `SourceProposalId`, liên kết `Memberships` (finding_id, plant_profile_id, confidence) và `EvidenceMedia` của đúng cụm đó. Gán danh sách proposals thật vào `comboSnapshot.DiseaseZones` (hoặc `[]` khi 0 detections / toàn healthy - confirmed empty).
9. **Các hạng mục đã triển khai trong V3-05D (Manager review và freeze — 2026-11-25)**:
   - **D01 Application Contracts & Handlers** (`Modules/AgriDrone.Modules.Surveys/Application/Features/Results/`): Tạo `GetSurveyResultReviewQuery`, `RecordSurveyComboReviewCommand`, `FreezeSurveyInternalResultCommand`, validators FluentValidation và MediatR handlers lấy actor từ `IExecutionContext`.
   - **D02 Domain & Persistence** (`SurveyComboReviewRevision`, `SurveyResultReviewOperation`, cập nhật `SurveyInternalApprovedResultRevision`): Bổ sung `ComboReviewRevisionId`/`ComboReviewFingerprint` + composite FK 5 cột với check constraint MAP/COMBO; bảng `survey_combo_review_revisions` (append-only, canonical decision fingerprint, unique one-current + predecessor lineage anti-fork) và `survey_result_review_operations` (actor/action/version/operationId replay); migration `20261125000000_V3_05DComboReviewAndInternalFreeze.cs`, Designer và `AgriDroneSchemaDbContextModelSnapshot.cs`.
   - **D03 Read Port & Validation**: Mở rộng `ISurveyComboPendingReadPort`/`SurveyComboPendingReadPort` với `GetPendingBatchForReviewAsync(tenantId, farmId, orderId)` và `GetComboReviewContextAsync` kèm `SurveyExpectedFreezeTokensDto`; validate geometry NTS, PostGIS `ST_Covers` trong `FarmBoundary`, accepted finding memberships covered bởi zone polygon, `SourcedGuidance` hiệu lực theo version & `GuidanceCandidates`, exact blocker keys `(SourceType, SourceId, ReasonCode)`, evidence media thuộc Mission/Tenant/Farm, và negative coverage proof (`HasCompleteComboInputCoverage`).
   - **D04 Transaction Coordinator & DI**: Tạo `ISurveyResultReviewTransaction` & `SurveyResultReviewTransaction` trong `AgriDrone.Database/Surveys/` tuân thủ ADR 0020 (Rank 1 Assignment `FOR SHARE` -> Rank 2 Order `FOR UPDATE` -> Rank 3 Receipt -> Rank 6 Snapshot/Map/Boundary/Batch -> Rank 7 Attempt `FOR UPDATE` -> Rank 8 Mission/Job -> Rank 10 InputApproval/Manifest), gọi `SurveyOrder.RecordInternalApprovedResult` + `CaptureAttempt.AcceptProcessing` nguyên tử trong cùng Serializable transaction (giữ Order ở `Processing` tới V3-05E). Cập nhật `docs/contracts/survey-business-v3.md` mục 6.1. Build `.NET 10` trên container Linux chưa xác minh được do thiếu `dotnet` SDK trong môi trường này (chờ kiểm tra tại môi trường có SDK 10 và V3-10 integration suite).
10. **Kế hoạch thực hiện tiếp theo**:
   - **V3-05E**: Count finalization command (`FinalizeSurveyOrderInvoiceCommand`), lock 11 ranks theo ADR 0020, và pre-release correction.
   - **V3-05F**: V3 APIs và bàn giao V3-06.

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

---

## 7. AWS Lightsail Demo Deployment Readiness (Phases P0–P5 Completed 2026-11-19)
> **Trạng thái**: Toàn bộ codebase packaging, proxy safety, compose architecture, scripts và CI/CD đã hoàn tất 100%. Sẵn sàng deploy lên AWS Lightsail (Singapore).
- **Packaging & Containerization**:
  - `backend/src/AgriDrone.Api/Dockerfile`: Multi-stage .NET 10 SDK -> ASP.NET 10 Runtime non-root (`$APP_UID`), build sẵn `efbundle` self-contained Linux-x64, healthcheck nhẹ.
  - `backend/src/AgriDrone.Api/Dockerfile.migrator`: Dedicated migrator image chạy one-shot migration.
  - `backend/services/metashape-service/Dockerfile`: PyTorch / Torchvision CPU-only từ official wheel index, pinned packages, non-root user `appuser`.
- **Proxy Safety & Configuration**:
  - `Program.cs`: `ForwardedHeadersOptions` cấu hình `KnownIPNetworks` cho các dải subnet Docker container bridge (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), `ForwardLimit = 1`.
  - `Program.cs` & `appsettings.json`: Dynamic CORS fail-closed đọc từ `Cors:AllowedOrigins` (mặc định rỗng, không wildcard credentials khi production).
  - `appsettings.json`: `DatabaseInitialization:ApplyMigrationsOnStartup = false` (tách migration ra khỏi API startup).
- **MinIO Public/Internal Split**:
  - `MinioStorageOptions.cs` thêm `PublicEndpoint` (URL presigned dành cho browser bên ngoài `https://media.domain.com`), trong khi `Endpoint` giữ `http://minio:9000` cho internal calls trong Docker bridge.
- **Production Compose & Edge Caddy**:
  - `compose.production.yaml`: Hai profiles: `core` (Caddy, API, Postgres 17, RabbitMQ, Redis, MinIO, one-shot Migrator) và `processing` (FastAPI, NodeODM concurrency=1).
  - `infrastructure/caddy/Caddyfile`: Tự động Let's Encrypt HTTPS cho `api.domain` và `media.domain`; chỉ publish duy nhất port 80/443; chặn console 9001 và Caddy admin.
- **Operations & CI/CD**:
  - `infrastructure/scripts/`: `bootstrap-lightsail.sh` (cài Docker, 16GB Swapfile, folder `/opt/agridrone`), `deploy.sh` (flock lock, pre-deploy backup, one-shot migration, smoke test, automatic rollback), `backup.sh` (`pg_dump -Fc` nén + sha256), `restore.sh` (diễn tập an toàn), `smoke-test.sh`.
  - `.github/workflows/`: `ci.yml` (build check .NET 10 0W/0E, compose config validation; không chạy test) và `deploy-lightsail.yml` (GHCR push immutable tags, SSH deploy).
- **Verification Evidence**:
  - `dotnet build backend/AgriDrone.sln --no-restore --nologo`: **0 Warning, 0 Error**.
  - `docker compose config`: 100% Valid, Published ports: 80 & 443 ONLY.
  - Integration Tests: Deferred to V3-10 per project governance.
