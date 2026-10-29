# BE1 Step 6 Completion Report: Initial Farm Base Map, Review Queue & Persistent Plant Identity

**Date:** 2026-10-28  
**Status:** Completed (CodeComplete, Hardened & Tested)  
**Scope:** BE1 — Step 6 (UC08 / Phase 11 — All Sub-Phases: 6A, 6B, 6C, 6D, 6E)

---

## 1. Executive Summary

Step 6 thiết lập luồng tiếp nhận kết quả bay khảo sát nền (`BaselineMappingCandidatesReadyV3`), quản lý bản nháp cơ sở (Draft Farm Base Map), hàng đợi đánh giá (Review Queue) cho System Manager với các quyết định chuẩn hóa, xác thực toàn vẹn không gian bằng thuật toán phân ô lưới $O(N)$ Spatial Bucketing, và cơ chế công bố nguyên tử (Atomic Farm-level Publication) bảo đảm nguyên lý sống còn **Persistent Plant Identity** qua các mùa vụ khảo sát.

Toàn bộ 6 nhóm lỗi/vấn đề kỹ thuật đã được phân tích, sửa chữa triệt để và kiểm chứng:
- **Enum `MappingCandidateReviewDecision`** tách riêng biệt 6 trạng thái nghiệp vụ.
- **Persistent Plant Identity** hiện thực hóa đầy đủ 4 kịch bản: `MatchExisting`, `Relocate`, `CreateNew`, `Replace`.
- **Database Schema & Constraints** khắc phục triệt để lỗi Alternate Key bảng `mapping_candidate_zones` và bổ sung `Supersede` bản đồ cũ để tuân thủ 100% partial unique index PostgreSQL.
- **Authoritative Batch Validator** nâng cấp thuật toán $O(N)$ Spatial Bucketing, kiểm tra trùng lặp lưới `(RowIndex, ColumnIndex)` và chặn 100% các điểm `Pending`/`NEEDS_REVIEW`.
- **Ingestion Consumer** bổ sung concurrency check cho `ExpectedCurrentFarmBaseMapVersionId`.
- **Unit Tests & Architecture Tests** đạt 100% Passed (86/86 Architecture Tests, 5/5 Validator Tests).

---

## 2. Deliverables & Technical Enhancements

### 2.1. Domain & Enums
- [`MappingCandidateReviewDecision.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/Staging/MappingCandidateReviewDecision.cs):
  - `Pending = 0`
  - `MatchExisting = 1`
  - `Relocate = 2`
  - `CreateNew = 3`
  - `Replace = 4`
  - `Reject = 5`
- [`FarmBaseMapVersion.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/FarmBaseMapVersion.cs):
  - Bổ sung phương thức `Supersede(DateTimeOffset supersededAt)`.
- [`MappingCandidate.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/Staging/MappingCandidate.cs):
  - Ràng buộc nghiệp vụ nghiêm ngặt trong `ApplyReview`: `MatchExisting` và `Relocate` bắt buộc phải có target plant ID; `Relocate` bắt buộc có `correctedPosition`.

### 2.2. Database Schema & Migration
- [`MappingCandidateZoneConfiguration.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/MappingCandidateZoneConfiguration.cs):
  - Xóa bỏ `.HasPrincipalKey(z => z.ZoneId)` gây sinh Unique Constraint sai trên toàn bảng.
  - Chuyển sang quan hệ khóa ngoại composite `(BatchId, ZoneId)` liên kết với Candidate.
- **Migration:**
  - [`20261028032022_Step6_FixMappingCandidateZoneAlternateKey.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261028032022_Step6_FixMappingCandidateZoneAlternateKey.cs): Drop `AK_mapping_candidate_zones_zone_id` và add composite FK.

### 2.3. Authoritative Batch Validator
- [`BaselineMappingBatchValidator.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Application/Services/BaselineMappingBatchValidator.cs):
  - **Spatial Grid Bucketing $O(N)$**: Chia không gian thành các cell $0.5\text{m} \times 0.5\text{m}$, kiểm tra trùng lặp khoảng cách (< 0.3m) cực nhanh, triệt tiêu nguy cơ timeout khi batch có 5.000 - 10.000 cây.
  - **Chặn Pending & NEEDS_REVIEW**: Báo lỗi `Error` nếu batch còn candidate chưa được review hoặc AI phân loại `NEEDS_REVIEW`/`OUT_OF_BOUNDARY` còn pending.
  - **Kiểm tra Lưới (Grid Position Check)**: Phát hiện và chặn các candidate active trùng `(RowIndex, ColumnIndex)` trong cùng một Zone.
  - **Kiểm tra ZoneId**: Đảm bảo toàn bộ candidate thuộc các Zone hợp lệ trong batch.

### 2.4. Orchestrator & Persistent Plant Identity
- [`FarmBaseMapPublicationOrchestrator.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.Database/Mapping/FarmBaseMapPublicationOrchestrator.cs):
  - **Supersede bản đồ cũ**: Tìm và chuyển các bản đồ `Published` (với Farm) và `Confirmed` (với Zone) sang `Superseded` trước khi publish bản đồ mới, bảo đảm an toàn partial unique index.
  - **Persistent Plant Identity**:
    - `MatchExisting`: Cập nhật `mapVersionId` trên cây cũ, không tạo plant mới, giữ nguyên Plant ID.
    - `Relocate`: Cập nhật vị trí tọa độ mới trên cây cũ (`ApplyPublishedMapPosition`), ghi `PlantChangeEvent.MappingPositionChanged`, giữ nguyên Plant ID.
    - `CreateNew`: Sinh `plantId` mới, gọi `Plant.CreateFromMapping`, ghi `PlantChangeEvent.MappingCreated`.
    - `Replace`: Cho cây cũ về hưu (`oldPlant.Retire(PlantLifecycleStatus.Removed, now)`), sinh `plantId` mới, gắn `plant.LinkReplacedPlant(oldPlantId)`, ghi `PlantChangeEvent.MappingCreated`.

### 2.5. Ingestion Consumer
- [`BaselineMappingCandidatesReadyV3Handler.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.Database/Mapping/BaselineMappingCandidatesReadyV3Handler.cs):
  - Kiểm tra `ExpectedCurrentFarmBaseMapVersionId` với bản đồ `Published` hiện tại của farm, phát hiện và trả về lỗi concurrency nếu có version drift.
  - **Auto Data Enrichment**:
    - Tự động truy vấn ranh giới `FarmBoundary` mới nhất đã được phê duyệt (`Approved`) của nông trại từ DB nếu BE2 không truyền `FarmBoundaryVersionId`.
    - Tự động dùng NetTopologySuite `boundary.Geometry.Covers(point)` để gán `IN_BOUNDARY` / `OUT_OF_BOUNDARY`.
    - Hỗ trợ cả 2 định dạng tọa độ: `(Longitude, Latitude)` trực tiếp và `OriginalPosition` GeoJSON.
    - Tự sinh `CandidateId`, `HandoffId`, `SourceObservationId`, `ProducedAt` nếu thiếu.
    - Lưu trường `TreeId` (ví dụ `"tree_r1_c1"`) từ BE2 vào staging để System Manager đối soát trực tiếp trên UI.

### 2.6. Hợp đồng tích hợp tinh gọn (Lean V3 Contract) & TreeId Staging
- [`BaselineMappingCandidatesReadyV3.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Mapping/BaselineMappingCandidatesReadyV3.cs):
  - Hỗ trợ đầy đủ các trường tinh gọn `double Longitude`, `double Latitude`, `string? TreeId` cho mỗi candidate.
  - Chuyển hơn 15 trường suy đoán/hạ tầng sang dạng tùy chọn (`optional`/`default`).
- [`V3BusinessContractValidators.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/Validation/V3BusinessContractValidators.cs):
  - Chỉ bắt buộc `SurveyOrderId`, `MissionId`, `FarmId`, `Zones` và tọa độ `(Longitude, Latitude)` hoặc `OriginalPosition`. Không từ chối payload thiếu ranh giới hay media asset ids.
- [`MappingCandidate.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/Staging/MappingCandidate.cs) & [`MappingCandidateConfiguration.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/MappingCandidateConfiguration.cs):
  - Thêm cột `tree_id` (`varchar(100)`).
- [`GetFarmBaseMapDraftQueryHandler.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Application/Features/Maps/GetFarmBaseMapDraft/GetFarmBaseMapDraftQueryHandler.cs):
  - Trả về `TreeId` trong `CandidateDto` để hiển thị trên Swagger và UI.
- **Migration:**
  - [`20261028033823_Step6_AddTreeIdToMappingCandidate.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261028033823_Step6_AddTreeIdToMappingCandidate.cs): Bổ sung cột `tree_id` vào bảng `farm.mapping_candidates`.

---

## 3. Verification & Test Results

1. **Unit Tests:**
   - [`BaselineMappingBatchValidatorTests.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/tests/AgriDrone.UnitTests/Application/Farms/BaselineMappingBatchValidatorTests.cs):
     - `ValidateWhenCandidatesArePendingReturnsInvalidWithPendingError`: **Passed**
     - `ValidateWhenCandidateHasNeedsReviewClassificationAndPendingFlagsNeedsReviewError`: **Passed**
     - `ValidateWhenCandidatesAreTooCloseDetectsDuplicateViaSpatialBucketing`: **Passed**
     - `ValidateWhenDuplicateGridPositionInSameZoneFlagsDuplicateGridError`: **Passed**
     - `ValidateWhenCandidatesProperlyReviewedAndNonOverlappingReturnsValid`: **Passed**
   - [`StreamlinedBaselineMappingCandidatesReadyV3Tests.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/tests/AgriDrone.UnitTests/Integration/StreamlinedBaselineMappingCandidatesReadyV3Tests.cs):
     - `ValidatorAcceptsStreamlinedLeanPayloadFromBe2`: **Passed**
     - `ValidatorRejectsWhenBothCoordinateFormatsAreMissing`: **Passed**
     - `SerializationDeserializationPreservesStreamlinedFields`: **Passed**
     - `MappingCandidateDomainEntityStoresTreeIdCorrectly`: **Passed**
2. **Architecture Tests:**
   - Chạy toàn bộ 86 bài test kiến trúc trong [`AgriDrone.ArchitectureTests`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/tests/AgriDrone.ArchitectureTests/): **Passed (86/86)**
3. **Build Solution:**
   - `dotnet build backend/AgriDrone.sln --no-restore`: **Succeeded (0 Warning(s), 0 Error(s))**
