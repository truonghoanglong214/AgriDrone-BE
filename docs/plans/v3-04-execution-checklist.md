# V3-04 — Execution Checklist: Mapping, Spatial Policy, Plant Identity & Billable Pole Count

Date: **2026-11-18**. Status: **[x] Done (Remediated) — G0–G18 closed; build clean; tests deferred to V3-10**.
[Implementation Plan](v3-04-mapping-and-plant-identity-implementation-plan.md), [Remediation Plan V2](../superpowers/plans/2026-11-17-v3-04-exhaustive-remediation-v2.md), [Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

> [!IMPORTANT]
> **TESTS NOT RUN — DEFERRED TO V3-10**:
> Theo quy định nghiêm ngặt của dự án, tuyệt đối không chạy `dotnet test` trong các feature phases (V3-01 đến V3-09).
> Toàn bộ quá trình kiểm chứng trong Phase V3-04 được thực hiện thông qua static scans và biên dịch sạch:
> `dotnet build backend/AgriDrone.sln --no-restore --nologo` (0 Warning, 0 Error).

---

## 1. Exit Gates Bắt buộc cho V3-04 (Canonical Remediation V2)
- [x] **G0 (Migration History Verification)**: **PASS — Nhánh A**. Local là database duy nhất theo xác nhận của chủ dự án; không migration V3-04 nào từng được apply. Hai remediation migration cũ đã được loại khỏi chain và thay bằng E1/S1 mới.
- [x] **G1 (Luồng Duy Nhất V4 Runtime)**: **PASS**. Source chỉ còn pipeline V4; local inbox/outbox và RabbitMQ main/retry/dead backlog của consumer V3 đều 0; không có database/môi trường khác cần đối soát.
- [x] **G2 (Provenance & Metadata Thật)**: `MissionPlantGpsPublisher` đọc metadata từ `AiProcessingJob` hoàn thành, sinh deterministic candidate UUIDs v5 từ `jobId + treeId`, không bịa GUID / timestamp ngẫu nhiên (`Guid.NewGuid()`, `UtcNow`).
- [x] **G3 (CaptureAttemptId Xuyên suốt)**: Contract V4 → `MappingCandidateBatch.CaptureAttemptId` → `SurveyOrderPoleCountSnapshot.CaptureAttemptId` → `FarmBaseMapInternallyApprovedV1.CaptureAttemptId` → `FarmBaseMapInternallyApprovedV1Handler` consumer validation.
- [x] **G4 (Phân định PlantId & PlantProfileId)**: Tra cứu phân định rõ ràng trong `FarmBaseMapInternalApprovalOrchestrator`: `targetPlantId` tra trong `activePlantsById`, từ `plant.PlantProfileId` tra trong `activeProfilesById`.
- [x] **G5 (Chống Stale Follow-up)**: `MappingCandidatesReadyV4Handler` kiểm tra nghiêm ngặt `order.BaselineMapVersionId == pointer.CurrentBaselineMapVersionId`, `ExpectedCurrentFarmBaseMapVersionId == latestAcceptedMap.Id`, và `mission.SourceMapVersionId == latestAcceptedMap.Id`.
- [x] **G6 (Dùng Đủ 4 Thuộc Tính SpatialPolicy)**: `GridSpacingToleranceM`, `RowBearingToleranceDeg`, `BoundaryToleranceM`, `MatchingDistanceThresholdM` đều được tiêu thụ trong `MappingCandidatesReadyV4Handler` và `BaselineMappingBatchValidator`. Fails closed nếu thiếu policy hoặc tolerance <= 0.
- [x] **G7 (Boundary Distance Chuẩn Mét PostGIS)**: `GeodesicSpatialCalculator` và PostGIS geography geodesic distance tính toán mét thực, không dùng khoảng cách độ NTS.
- [x] **G8 (DB Invariants Baseline & Follow-up)**: Check constraint `ck_farm_base_map_versions_baseline_lineage`; ba model index có tên riêng để EF không ghi đè nhau: `uq_farm_base_map_versions_one_baseline`, `uq_farm_base_map_versions_one_draft`, `uq_farm_base_map_versions_one_published`; version index là `uq_farm_base_map_versions_tenant_farm_version`.
- [x] **G9 (Evidence FK Toàn Vẹn & Restrict)**: Mọi FK snapshot items, snapshots, batches, observations đều sử dụng `DeleteBehavior.Restrict`.
- [x] **G10 (Plant & Profile 1–1)**: Unique filtered index `uq_plants_farm_plant_profile` trên `(farm_id, plant_profile_id)` WHERE `plant_profile_id IS NOT NULL`.
- [x] **G11 (Snapshot Fingerprint Deterministic)**: Tính SHA-256 canonical từ stable input tokens (candidate ID, target plant ID, previous position, row/col index, rejection reason), không phụ thuộc GUID output mới sinh.
- [x] **G12 (Không Ghi Published Trong V3-04)**: `FarmBaseMapInternalApprovalOrchestrator` chỉ phê duyệt nội bộ (`InternalApproved`), không gán `PublishedMapVersionId` / `MapPublishedAt`. `DroneMission` ghi nhận `InternallyApprovedMapVersionId` và `MapInternallyApprovedAt`.
- [x] **G13 (Migration Backfill Không Đoán)**: Expand → backfill → validate → constrain. Capture attempt được resolve qua `mission.drone_missions.capture_attempt_id`, rồi kiểm tra exact scope `(order, mission, tenant, farm)` với `survey.capture_attempts`; `RAISE EXCEPTION` nếu thiếu/sai scope, mapping kind không xác định, policy không hiệu lực, fingerprint sai hoặc tolerance <= 0.
- [x] **G14 (Migration Không Lẫn Phase Khác)**: **PASS** — E1 `20261118040000` chỉ thêm enum ngoài transaction; S1 `20261118042702` chỉ chứa object V3-04. SQL generated diff không có pricing, appointment hoặc post-flight settlement.
- [x] **G15 (Boundary Exception Đúng Scope)**: Query boundary exception kiểm tra đủ 5 chiều: `TenantId + FarmId + SurveyOrderId + MissionId + FarmBoundaryVersionId`.
- [x] **G16 (Snapshot Domain Invariants Đầy Đủ)**: `DomainGuard` và check constraint `ck_survey_order_pole_count_snapshot_items_acceptance`.
- [x] **G17 (Biên Dịch Sạch 100%)**: `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt `0 Warning, 0 Error`.
- [x] **G18 (Không Chạy Test)**: Ghi nhận đầy đủ "TESTS NOT RUN — deferred V3-10".

---

## 2. Tiến độ Thực hiện Từng Task Remediation V2
| Task ID | Nội dung công việc | Trạng thái | Ghi chú & Đầu ra |
|---|---|---|---|
| **Task 0** | Khóa trạng thái tài liệu trước khi sửa code | `[x] Completed` | Đã khóa trạng thái Remediation In Progress |
| **Task 1** | Gate M0: Quyết định chiến lược migration | `[x] Completed — Branch A` | Chủ dự án xác nhận local là database duy nhất; lịch sử local không có migration V3-04 |
| **Task 2** | Sửa nguồn provenance ở Missions/API | `[x] Completed` | `MissionPlantGpsPublisher`, `MappingCandidatesReadyV4`, API payload, `AiProcessingJob.RecordCompletedMappingRun` |
| **Task 3** | Truyền `CaptureAttemptId` xuyên suốt batch | `[x] Completed` | `MappingCandidateBatch`, `MappingCandidateBatchConfiguration` |
| **Task 4** | V4 Ingestion fail-closed & chống stale lineage | `[x] Completed` | `MappingCandidatesReadyV4Handler` validation & FOR UPDATE locks |
| **Task 5** | Boundary evidence PostGIS & SpatialPolicy | `[x] Completed` | `GeodesicSpatialCalculator`, `BaselineMappingBatchValidator` |
| **Task 6** | Xóa pipeline V3 khỏi runtime | `[x] Completed` | Đã gỡ DI/xóa handler; inbox/outbox và RabbitMQ V3 backlog đều 0 |
| **Task 7** | Sửa dứt điểm `PlantId` / `PlantProfileId` & 1-1 | `[x] Completed` | Orchestrator & PlantConfiguration unique filtered index |
| **Task 8** | Orchestrator: CaptureAttempt, boundary scope, no Published | `[x] Completed` | `FarmBaseMapInternalApprovalOrchestrator` refactored cleanly |
| **Task 9** | Canonical fingerprint & snapshot invariants | `[x] Completed` | `SurveyOrderPoleCountSnapshot`, items, invariants |
| **Task 10** | Khóa DB invariants & evidence FKs | `[x] Completed` | Entity configurations, restrict FKs & check constraints |
| **Task 11** | Semantics DroneMission & Missions consumer | `[x] Completed` | `DroneMission.InternallyApprovedMapVersionId`, `DroneMissionApprovalOutcome`, consumer validation |
| **Task 12** | Tạo migration sạch & backfill có kiểm chứng | `[x] Completed` | E1/S1 theo Branch A; provenance nullable → exact backfill → validation → NOT NULL; duplicate guards chạy trước unique indexes |
| **Task 13** | Static verification, build sạch & chốt trạng thái | `[x] Completed` | Static scans đạt; generated SQL đã inspect nhưng không apply; build 0 Warning, 0 Error; tests deferred to V3-10 |

---

## 3. Operational evidence — 2026-11-18

Local environment được kiểm tra read-only lúc **11:05 Asia/Bangkok** bằng PostgreSQL 17.5 và RabbitMQ local:

- Migration history: không có `20261116131222`, `20261116145210` hoặc `20261117050302`; migration mới nhất local là `20261109071540_Step7EPlantHealthCompletionOutput`.
- Outbox `mapping.baseline-candidates-ready.v3`: 1 `PUBLISHED`, 0 unresolved.
- Inbox consumer `be1-baseline-mapping-candidates-ready-v3`: 1 `COMPLETED`, 0 unresolved.
- RabbitMQ queues `agridrone.be1.baseline-mapping-candidates-ready.v3`, retry 1–3 và dead: tất cả `messages_ready = 0`, `messages_unacknowledged = 0`.
- Có 1 message trong queue `agridrone.be2.farm-base-map-published.v3.dead`; đây không phải backlog của consumer V3 bị xóa trong G1 và chưa bị sửa/xóa trong đợt này.
- Chủ dự án xác nhận **không có database nào khác** ngoài local, vì vậy bằng chứng local là toàn bộ phạm vi vận hành và Nhánh A được chốt.
- Hai remediation migration cũ `20261116145210` và `20261117050302` đã bị loại. Chain mới giữ migration gốc `20261116131222`, sau đó chạy E1 `20261118040000_V3_04AddInternalApprovedMapStatus` rồi S1 `20261118042702_V3_04CanonicalMappingRemediation`.
- `dotnet ef migrations script` xác nhận enum chạy trước `START TRANSACTION` của S1; S1 thêm cột provenance nullable, backfill/validate rồi mới `SET NOT NULL`; ba filtered unique index baseline/draft/published đều xuất hiện; không có SQL pricing/appointment/post-flight.
- `SurveyPayments:Enabled` đã được đưa về `false` trong Development để giữ fail-closed đến V3-10.

Script dùng lại cho mọi môi trường: `docs/operations/v3-04-remediation-preflight.sql`.
