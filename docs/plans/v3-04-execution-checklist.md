# V3-04 — Execution Checklist: Mapping, Spatial Policy, Plant Identity & Billable Pole Count

Date: **2026-11-16**. Status: **[x] Accepted — All Tasks & Remediation Items Completed and Verified Cleanly**.  
[Implementation Plan](v3-04-mapping-and-plant-identity-implementation-plan.md), [Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

---

## 1. Exit Gates Bắt buộc cho V3-04
- [x] **G1 (Lineage Bản đồ & Hai giai đoạn Release)**: `FarmBaseMapVersion` có `PredecessorVersionId` liên kết phiên bản trước; Follow-up không ghi đè Baseline gốc. Tách biệt `ApproveInternal` (`InternalApproved`) với customer publication (`Published` ở V3-06); bổ sung `farm.farm_baseline_pointers`.
- [x] **G2 (Spatial Policy Fail-closed)**: Bảng `survey.spatial_policies` lưu các tham số dung sai không gian động có phiên bản. Seed DEFAULT policy cố định, truy xuất theo thời điểm capture (`batch.ProducedAt`), trả `AppError.Conflict` khi thiếu policy, không fallback 1.5m; chuẩn hóa uppercase StreamKey và fingerprint theo invariant culture.
- [x] **G3 (Định danh Cây trồng & Trụ)**: `PlantProfile` quản lý ID vĩnh viễn của trụ; kiểm tra dung sai geodesic (Haversine) trước khi mutate; `Plant.PlantProfileId` liên kết 1-1; Replacement fail-closed bảo toàn predecessor; `PlantProfileObservation` ghi nhận quan sát lại cho `MatchExisting`; `PlantChangeEvent` bất biến và đầy đủ lineage metadata.
- [x] **G4 (Bằng chứng Chốt Số trụ Tính tiền Authoritative Snapshot)**: Thực thể `SurveyOrderPoleCountSnapshot` lưu bằng chứng SHA-256 fingerprint, đếm `COUNT(DISTINCT resultant_plant_profile_id)` nằm trong ranh giới PostGIS (`ST_Within`). Phân định ranh giới Clean Architecture qua `IOrderPoleEvidenceReader` và kiểm tra quyền quản lý farm (`ISystemManagerAccessService`).
- [x] **G5 (Biên dịch Sạch)**: Toàn bộ solution biên dịch thành công 100% (`0 Warning, 0 Error`). Tuyệt đối không chạy `dotnet test`.

---

## 2. Tiến độ Thực hiện Từng Task & Remediation
| Task ID | Nội dung công việc | Trạng thái | Ghi chú & Đầu ra |
|---|---|---|---|
| **Task 1** | Entity `SpatialPolicy` & Configuration | `[x] Completed` | `survey.spatial_policies`, constraints, repository, fail-closed, fingerprint invariant culture |
| **Task 2** | `FarmBaseMapVersion` Lineage & Baseline | `[x] Completed` | `PredecessorVersionId`, `IsBaseline`, `BaselineMapVersionId`, `FarmBaselinePointer`, `InternalApproved` |
| **Task 3** | `PlantProfile` & `PlantChangeEvent` | `[x] Completed` | `plant.plant_profiles`, `plant.plant_profile_observations`, `plant.plant_change_events` lineage, geodesic check |
| **Task 4** | `SurveyOrder` Verified Billable Pole Count & Snapshot | `[x] Completed` | `SurveyOrderPoleCountSnapshot`, `verified_pole_count_snapshot_id`, `RecordVerifiedPoleCount` |
| **Task 5** | EF Core Migrations | `[x] Completed` | `V3_04MappingAndPlantIdentity` & `V3_04MappingLineageAndEvidenceRemediation` (backfill SQL, pointer seed) |
| **Task 6** | Application Handlers & Cross-Module Boundaries | `[x] Completed` | `IOrderPoleEvidenceReader`, `OrderPoleEvidenceReader`, `VerifyOrderBillablePolesCommandHandler`, `FarmBaseMapPublicationOrchestrator` |
| **Task 7** | Cập nhật Memory Bank & Commit `LongTH` | `[x] Completed` | `PROJECT_MEMORY.md`, 0 Warning, 0 Error |
