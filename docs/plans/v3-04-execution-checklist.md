# V3-04 — Execution Checklist: Mapping, Spatial Policy, Plant Identity & Billable Pole Count

Date: **2026-11-17**. Status: **[~] Remediation In Progress — code builds clean; migration gates remain open**.
[Implementation Plan](v3-04-mapping-and-plant-identity-implementation-plan.md), [Remediation Plan V2](../superpowers/plans/2026-11-17-v3-04-exhaustive-remediation-v2.md), [Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

> [!IMPORTANT]
> **TESTS NOT RUN — DEFERRED TO V3-10**:
> Theo quy định nghiêm ngặt của dự án, tuyệt đối không chạy `dotnet test` trong các feature phases (V3-01 đến V3-09).
> Toàn bộ quá trình kiểm chứng trong Phase V3-04 được thực hiện thông qua static scans và biên dịch sạch:
> `dotnet build backend/AgriDrone.sln --no-restore --nologo` (0 Warning, 0 Error).

---

## 1. Exit Gates Bắt buộc cho V3-04 (Canonical Remediation V2)
- [ ] **G0 (Migration History Verification)**: **OPEN** — chưa có bằng chứng `__EFMigrationsHistory` từ local/shared/staging, nên chưa được chọn Nhánh A hoặc Nhánh B.
- [x] **G1 (Luồng Duy Nhất V4 Runtime)**: Pipeline V4 runtime duy nhất: Ingest (`MappingCandidatesReadyV4`) → Manager review → `ApproveFarmBaseMapInternallyCommand` → `IFarmBaseMapInternalApprovalOrchestrator`. Đã xóa bỏ hoàn toàn `BaselineMappingCandidatesReadyV3Handler` và `BaselineMappingCandidatesReadyV3Processor` khỏi DI và codebase.
- [x] **G2 (Provenance & Metadata Thật)**: `MissionPlantGpsPublisher` đọc metadata từ `AiProcessingJob` hoàn thành, sinh deterministic candidate UUIDs v5 từ `jobId + treeId`, không bịa GUID / timestamp ngẫu nhiên (`Guid.NewGuid()`, `UtcNow`).
- [x] **G3 (CaptureAttemptId Xuyên suốt)**: Contract V4 → `MappingCandidateBatch.CaptureAttemptId` → `SurveyOrderPoleCountSnapshot.CaptureAttemptId` → `FarmBaseMapInternallyApprovedV1.CaptureAttemptId` → `FarmBaseMapInternallyApprovedV1Handler` consumer validation.
- [x] **G4 (Phân định PlantId & PlantProfileId)**: Tra cứu phân định rõ ràng trong `FarmBaseMapInternalApprovalOrchestrator`: `targetPlantId` tra trong `activePlantsById`, từ `plant.PlantProfileId` tra trong `activeProfilesById`.
- [x] **G5 (Chống Stale Follow-up)**: `MappingCandidatesReadyV4Handler` kiểm tra nghiêm ngặt `order.BaselineMapVersionId == pointer.CurrentBaselineMapVersionId`, `ExpectedCurrentFarmBaseMapVersionId == latestAcceptedMap.Id`, và `mission.SourceMapVersionId == latestAcceptedMap.Id`.
- [x] **G6 (Dùng Đủ 4 Thuộc Tính SpatialPolicy)**: `GridSpacingToleranceM`, `RowBearingToleranceDeg`, `BoundaryToleranceM`, `MatchingDistanceThresholdM` đều được tiêu thụ trong `MappingCandidatesReadyV4Handler` và `BaselineMappingBatchValidator`. Fails closed nếu thiếu policy hoặc tolerance <= 0.
- [x] **G7 (Boundary Distance Chuẩn Mét PostGIS)**: `GeodesicSpatialCalculator` và PostGIS geography geodesic distance tính toán mét thực, không dùng khoảng cách độ NTS.
- [x] **G8 (DB Invariants Baseline & Follow-up)**: Check constraint `ck_farm_base_map_versions_lineage`, unique filtered index `uq_farm_base_map_versions_one_baseline` (`is_baseline=true`), `uq_farm_base_map_versions_one_draft` (`status='DRAFT'`), `uq_farm_base_map_versions_farm_version` trên `(tenant_id, farm_id)`.
- [x] **G9 (Evidence FK Toàn Vẹn & Restrict)**: Mọi FK snapshot items, snapshots, batches, observations đều sử dụng `DeleteBehavior.Restrict`.
- [x] **G10 (Plant & Profile 1–1)**: Unique filtered index `uq_plants_farm_profile` trên `(farm_id, plant_profile_id)` WHERE `plant_profile_id IS NOT NULL`.
- [x] **G11 (Snapshot Fingerprint Deterministic)**: Tính SHA-256 canonical từ stable input tokens (candidate ID, target plant ID, previous position, row/col index, rejection reason), không phụ thuộc GUID output mới sinh.
- [x] **G12 (Không Ghi Published Trong V3-04)**: `FarmBaseMapInternalApprovalOrchestrator` chỉ phê duyệt nội bộ (`InternalApproved`), không gán `PublishedMapVersionId` / `MapPublishedAt`. `DroneMission` ghi nhận `InternallyApprovedMapVersionId` và `MapInternallyApprovedAt`.
- [x] **G13 (Migration Backfill Không Đoán)**: Exact join matching `(order_id, mission_id, tenant_id, farm_id)`, `RAISE EXCEPTION` nếu thiếu attempt, mapping kind không xác định, hoặc tolerance <= 0.
- [ ] **G14 (Migration Không Lẫn Phase Khác)**: **OPEN** — `20261117050302` còn chứa pricing/appointment/post-flight; phải tách sau khi đóng G0, không được xóa SQL riêng lẻ làm lệch model snapshot.
- [x] **G15 (Boundary Exception Đúng Scope)**: Query boundary exception kiểm tra đủ 5 chiều: `TenantId + FarmId + SurveyOrderId + MissionId + FarmBoundaryVersionId`.
- [x] **G16 (Snapshot Domain Invariants Đầy Đủ)**: `DomainGuard` và check constraint `ck_survey_order_pole_count_snapshot_items_acceptance`.
- [x] **G17 (Biên Dịch Sạch 100%)**: `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt `0 Warning, 0 Error`.
- [x] **G18 (Không Chạy Test)**: Ghi nhận đầy đủ "TESTS NOT RUN — deferred V3-10".

---

## 2. Tiến độ Thực hiện Từng Task Remediation V2
| Task ID | Nội dung công việc | Trạng thái | Ghi chú & Đầu ra |
|---|---|---|---|
| **Task 0** | Khóa trạng thái tài liệu trước khi sửa code | `[x] Completed` | Đã khóa trạng thái Remediation In Progress |
| **Task 1** | Gate M0: Quyết định chiến lược migration | `[ ] Blocked by evidence` | Cần kết quả `__EFMigrationsHistory` của mọi database liên quan |
| **Task 2** | Sửa nguồn provenance ở Missions/API | `[x] Completed` | `MissionPlantGpsPublisher`, `MappingCandidatesReadyV4`, API payload, `AiProcessingJob.RecordCompletedMappingRun` |
| **Task 3** | Truyền `CaptureAttemptId` xuyên suốt batch | `[x] Completed` | `MappingCandidateBatch`, `MappingCandidateBatchConfiguration` |
| **Task 4** | V4 Ingestion fail-closed & chống stale lineage | `[x] Completed` | `MappingCandidatesReadyV4Handler` validation & FOR UPDATE locks |
| **Task 5** | Boundary evidence PostGIS & SpatialPolicy | `[x] Completed` | `GeodesicSpatialCalculator`, `BaselineMappingBatchValidator` |
| **Task 6** | Xóa pipeline V3 khỏi runtime | `[x] Completed` | Gỡ DI V3, xóa `BaselineMappingCandidatesReadyV3Handler` & Processor |
| **Task 7** | Sửa dứt điểm `PlantId` / `PlantProfileId` & 1-1 | `[x] Completed` | Orchestrator & PlantConfiguration unique filtered index |
| **Task 8** | Orchestrator: CaptureAttempt, boundary scope, no Published | `[x] Completed` | `FarmBaseMapInternalApprovalOrchestrator` refactored cleanly |
| **Task 9** | Canonical fingerprint & snapshot invariants | `[x] Completed` | `SurveyOrderPoleCountSnapshot`, items, invariants |
| **Task 10** | Khóa DB invariants & evidence FKs | `[x] Completed` | Entity configurations, restrict FKs & check constraints |
| **Task 11** | Semantics DroneMission & Missions consumer | `[x] Completed` | `DroneMission.InternallyApprovedMapVersionId`, `DroneMissionApprovalOutcome`, consumer validation |
| **Task 12** | Tạo migration sạch & backfill có kiểm chứng | `[~] In Progress` | Backfill/invariants đã siết; còn chờ G0 và tách scope ngoài V3-04 |
| **Task 13** | Static verification, build sạch & chốt trạng thái | `[~] In Progress` | Build đạt 0 Warning, 0 Error; chưa được chốt Done khi G0/G14 còn mở; tests deferred to V3-10 |
