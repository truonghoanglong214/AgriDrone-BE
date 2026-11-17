# V3-04 — Execution Checklist: Mapping, Spatial Policy, Plant Identity & Billable Pole Count

Date: **2026-11-17**. Status: **[x] Canonical Remediation Completed — `2026-11-16-v3-04-comprehensive-remediation.md` Successfully Implemented**.  
[Implementation Plan](v3-04-mapping-and-plant-identity-implementation-plan.md), [Canonical Remediation Plan](../superpowers/plans/2026-11-16-v3-04-comprehensive-remediation.md), [Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

---

## 1. Exit Gates Bắt buộc cho V3-04 (Canonical Remediation)
- [x] **G1 (Luồng Duy Nhất Phê Duyệt Kỹ Thuật Nội Bộ)**: Chỉ còn một pipeline: Ingest (`MappingCandidatesReadyV4`) → Manager review → `ApproveFarmBaseMapInternallyCommand` → `IFarmBaseMapInternalApprovalOrchestrator`. Xóa toàn bộ bypass cũ (`PublishFarmBaseMapCommand`, `ApproveInternalBaseMapCommand`, `VerifyOrderBillablePolesCommand`). Không có trạng thái `PUBLISHED` trong V3-04; phát hành cho khách hàng thuộc V3-06.
- [x] **G2 (Contract V4 & Sự kiện Nội bộ V1)**: `MappingCandidatesReadyV4` hỗ trợ cả `FirstBaseline` và `FollowUpSnapshot`. Orchestrator phát `FarmBaseMapInternallyApprovedV1` vào Outbox để Missions module hoàn thành technical mapping mission qua `FarmBaseMapInternallyApprovedV1Handler`.
- [x] **G3 (Lineage & Follow-up Độc Lập)**: `FarmBaseMapVersion` factory methods `CreateFirstBaseline` và `CreateFollowUp` đóng băng lineage ngay lúc ingest. Follow-up kế thừa `BaselineMapVersionId` từ baseline pointer và trỏ `PredecessorVersionId` vào bản đồ trước đó, không bị chặn bởi cờ baseline và không ghi đè baseline pointer.
- [x] **G4 (Chống Race Condition)**: Sử dụng `SELECT * FROM farm.farms WHERE id = ... FOR UPDATE` và ràng buộc duy nhất DB `uq_farm_baseline_pointers_farm` để loại bỏ race condition khi tạo baseline song song.
- [x] **G5 (Lineage Bất Biến)**: Không còn setter/public method `SetLineage` hay `ConfigureLineage`.
- [x] **G6 (Spatial Policy Fail-closed & Freeze)**: Policy dung sai không gian động được snapshot đầy đủ 4 tham số vào batch (`batch.ProducedAt`). Đã seed policy `DEFAULT` cố định. Thiếu policy trả `AppError.Conflict`, tuyệt đối không fallback ngầm.
- [x] **G7 (Dung Sai Geodesic & Mutate An Toàn)**: Quyết định match/relocate tính khoảng cách geodesic từ toạ độ reviewed. Kiểm tra vượt ngưỡng trước khi cập nhật entity.
- [x] **G8 (Bằng Chứng Snapshot & Item-level Evidence)**: Thực thể `SurveyOrderPoleCountSnapshot` lưu bằng chứng SHA-256 fingerprint (tính xác định không timestamp/actor), quản lý danh sách chi tiết `SurveyOrderPoleCountSnapshotItem`. Đếm `COUNT(DISTINCT resultant_plant_profile_id)` nằm trong ranh giới PostGIS (`ST_Within`). Cho phép 0 trụ hợp lệ, loại bỏ fallback `billableCount == 0`.
- [x] **G9 (Idempotent Execution)**: Kiểm tra trạng thái draft và handoffId giúp retry sau commit trả cùng kết quả mà không sinh duplicate dữ liệu.
- [x] **G10 (Ràng Buộc Toàn Vẹn Composite Keys)**: Composite FKs ngăn chặn liên kết chéo tenant/farm/batch giữa snapshots, candidate batches và plant profiles.
- [x] **G11 (Không Xuất Bản Trước V3-06)**: Không produce hay consume `FarmBaseMapPublishedV3` trong V3-04. `SurveyOrder.RecordBaselinePublication` không được gọi.
- [x] **G12 (EF Core Migration & Seed)**: Migration `20261117050302_V3_04CanonicalRemediationFinal` bổ sung enum value `INTERNAL_APPROVED` (`suppressTransaction: true`), tạo bảng `survey_order_pole_count_snapshot_items`, backfill status batch `Published` → `Applied`, và seed DEFAULT `SpatialPolicy` đầy đủ 15 cột.
- [x] **G13 (Biên Dịch Sạch 100%)**: Toàn bộ solution biên dịch thành công (`0 Warning, 0 Error`). Tuyệt đối không chạy `dotnet test`.
- [x] **G14 (Đồng Bộ Documentation & Memory Bank)**: Memory bank và checklist phản ánh chính xác kết quả remediation.

---

## 2. Tiến độ Thực hiện Từng Task Remediation
| Task ID | Nội dung công việc | Trạng thái | Ghi chú & Đầu ra |
|---|---|---|---|
| **Task 0** | Khởi động remediation, ghi nhận dirty files | `[x] Completed` | Loại bỏ migration chưa chuẩn, dọn model snapshot |
| **Task 1** | Chuẩn hóa Domain Invariants & Terminology | `[x] Completed` | `MappingKind`, `FarmBaselinePointer` PK=id, `MappingCandidateBatch.MarkApplied`, `SurveyOrderPoleCountSnapshotItem`, `SurveyOrder` methods tách biệt |
| **Task 2** | Hoàn thiện V4 Contract & Internal Approved V1 | `[x] Completed` | `MappingCandidatesReadyV4`, `FarmBaseMapInternallyApprovedV1`, registry, validators |
| **Task 3** | Pipeline Ingestion & Draft Creation | `[x] Completed` | `MappingCandidatesReadyV4Processor`, `MappingCandidatesReadyV4Handler` với row-locking `FOR UPDATE` |
| **Task 4** | Tolerance-aware Batch Validator | `[x] Completed` | `BaselineMappingBatchValidator` sử dụng `batch.GridSpacingToleranceM` |
| **Task 5** | Canonical Internal Approval Orchestrator | `[x] Completed` | `IFarmBaseMapInternalApprovalOrchestrator`, `FarmBaseMapInternalApprovalOrchestrator`, `ApproveFarmBaseMapInternallyCommand` |
| **Task 6** | Consumer Hoàn Tất Technical Mapping Mission | `[x] Completed` | `FarmBaseMapInternallyApprovedV1Processor`, `FarmBaseMapInternallyApprovedV1Handler`, `DroneMission.ApplyInternallyApprovedFarmBaseMap` |
| **Task 7** | EF Configurations & Migrations | `[x] Completed` | Migration `20261117050302_V3_04CanonicalRemediationFinal` với enum `INTERNAL_APPROVED`, backfill SQL, seed SpatialPolicy |
| **Task 8** | Xóa Bypasses & Clean Codebase | `[x] Completed` | Xóa `PublishFarmBaseMapCommand`, `ApproveInternalBaseMapCommand`, `VerifyOrderBillablePolesCommand`, `IFarmBaseMapPublicationOrchestrator`, `OrderPoleEvidenceReader` fallback |
| **Task 9** | Build & Final Verification | `[x] Completed` | `dotnet build backend/AgriDrone.sln --no-restore --nologo` → 0 Warning, 0 Error |
