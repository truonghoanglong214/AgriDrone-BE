# V3-04 — Execution Checklist: Mapping, Spatial Policy, Plant Identity & Billable Pole Count

Date: **2026-11-16**. Status: **[x] Accepted — All Tasks 1-7 Completed and Verified Cleanly**.  
[Implementation Plan](v3-04-mapping-and-plant-identity-implementation-plan.md), [Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

---

## 1. Exit Gates Bắt buộc cho V3-04
- [x] **G1 (Lineage Bản đồ)**: `FarmBaseMapVersion` có `PredecessorVersionId` liên kết phiên bản trước; Follow-up không ghi đè Baseline gốc.
- [x] **G2 (Spatial Policy)**: Bảng `survey.spatial_policies` lưu các tham số dung sai không gian động có phiên bản, kiểm tra validation đầy đủ.
- [x] **G3 (Định danh Cây trồng)**: `PlantProfile` quản lý ID vĩnh viễn của trụ; `PlantChangeEvent` lưu vết bất biến mọi biến động (Relocation, Replacement, Retired).
- [x] **G4 (Chốt Số trụ Tính tiền)**: `SurveyOrder.VerifiedBillablePoleCount` chỉ đếm các trụ nằm trong ranh giới PostGIS (`ST_Within`), không bypass ranh giới.
- [x] **G5 (Biên dịch Sạch)**: Toàn bộ solution biên dịch thành công 100% (`0 Warning, 0 Error`). Tuyệt đối không chạy `dotnet test`.

---

## 2. Tiến độ Thực hiện Từng Task

| Task ID | Nội dung công việc | Trạng thái | Ghi chú & Đầu ra |
|---|---|---|---|
| **Task 1** | Entity `SpatialPolicy` & Configuration | `[x] Completed` | `survey.spatial_policies`, constraints, repository |
| **Task 2** | `FarmBaseMapVersion` Lineage & Baseline | `[x] Completed` | `PredecessorVersionId`, `IsBaseline`, `BaselineMapVersionId`, approval |
| **Task 3** | `PlantProfile` & `PlantChangeEvent` | `[x] Completed` | `plant.plant_profiles`, `plant.plant_change_events` lineage |
| **Task 4** | `SurveyOrder` Verified Billable Pole Count | `[x] Completed` | `verified_billable_pole_count`, `baseline_map_version_id` |
| **Task 5** | EF Core Migration `V3_04MappingAndPlantIdentity` | `[x] Completed` | Model snapshot đồng bộ, migration sạch |
| **Task 6** | Application Handlers cho Mapping & Pole Count | `[x] Completed` | `ApproveInternalBaseMapCommand`, `VerifyOrderBillablePolesCommand`, Orchestrator |
| **Task 7** | Cập nhật Memory Bank & Commit `LongTH` | `[x] Completed` | `PROJECT_MEMORY.md` |
