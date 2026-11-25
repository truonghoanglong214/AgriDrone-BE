# V3-04 — Mapping, Spatial Policy, Plant Identity & Billable Pole Count Implementation Plan

**Checkpoint 2026-11-25.** Đã triển khai mapping, spatial policy, lineage trụ và count snapshot. Remediation G0–G18 đã đóng ở checkpoint V3-04.

- [ ] Nghiệm thu mapping/Baseline, lineage, PostGIS, migration/preflight và race ở V3-10. [Acceptance matrix](../testing/v3-business-acceptance.md)

[Backlog V3-06 đến V3-10](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#4-status-và-dependency-roadmap)

<details>
<summary>Hồ sơ triển khai và evidence theo từng checkpoint</summary>

Các trạng thái, checklist và số liệu bên dưới ghi theo thời điểm lập từng phần.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triển khai toàn diện hạ tầng bản đồ số (Mapping Lineage), chính sách không gian có phiên bản (`SpatialPolicy`), định danh cây trồng bất biến (`PlantProfile` & `PlantChangeEvent`) và cơ chế chốt số lượng trụ tính tiền (`VerifiedBillablePoleCount`) chuẩn bị dữ liệu đầu vào bất biến cho lập hóa đơn ở Phase V3-05.

**Architecture:** Modular Monolith + Clean Architecture. Phối hợp giữa các module `Farms` (Bản đồ & Ranh giới), `Plants` (Định danh cây & Lịch sử biến động), `Surveys` (Chính sách không gian, đơn hàng & chốt số trụ). Dùng PostGIS NetTopologySuite cho tính toán hình học ranh giới, append-only history cho mọi biến động cây trồng, và Result pattern để kiểm soát lỗi.

**Tech Stack:** .NET 10, C# 14, Entity Framework Core, PostgreSQL 17 + PostGIS 3.5, NetTopologySuite, MediatR, Result Pattern.

**Spec:** [BE1-Core-Business-Implementation-Plan.md Mục 8](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#8-v3-0405--mapping-identity-combo-và-countinvoice), [Business-Rules-v3.md Mục 5](../../Codex-Plan/Business-Rules-v3.md#L75-L87), [v3-01-locked-design.md](../domain/v3-01-locked-design.md).

---

## Global Constraints

1. **KHÔNG CHẠY TEST TÍCH HỢP TRONG PHASE NÀY**: Toàn bộ Integration Test Suite chạy tập trung tại Phase V3-10. Mọi bước verification chỉ chạy:
   ```powershell
   dotnet build backend/AgriDrone.sln --no-restore --nologo
   ```
2. **KẾ HOẠCH TRƯỚC KHI CODE**: Tuân thủ triệt để Superpowers `writing-plans` và `executing-plans`.
3. **BẢO TỒN DỮ LIỆU & BẤT BIẾN**:
   - Follow-up Order KHÔNG ĐƯỢC ghi đè lên Baseline cũ; phải tạo `FarmBaseMapVersion` mới trỏ `PredecessorVersionId`.
   - Một trụ vật lý chỉ có một `PlantProfileId` duy nhất. Mọi biến động (dời toạ độ GNSS, thay thế, hủy) bắt buộc ghi `PlantChangeEvent` bất biến kèm duyệt của Manager.
   - Không bypass ranh giới: Trụ ngoài ranh giới không được tính tiền (`VerifiedBillablePoleCount` chỉ đếm trụ `in-boundary`).

---

## Review Focus

1. **Lineage bản đồ không bị chu kỳ**: `PredecessorVersionId` phải cùng Farm và có `VersionNumber` nhỏ hơn; bản đồ Baseline gốc có `PredecessorVersionId == null`.
2. **Sai số GNSS không làm đổi ID cây**: Trường hợp dịch chuyển tọa độ trong ngưỡng `SpatialPolicy.MatchingDistanceThresholdM` phải giữ nguyên `PlantProfileId` và ghi `PlantChangeEvent` loại `Relocation`.
3. **Cây chết/thay thế cấp ID mới**: Trụ thay thế được cấp `PlantProfileId` mới, trỏ `PredecessorProfileId` về profile cũ, profile cũ chuyển `Retired`.
4. **Trụ ngoài ranh giới không lọt vào Billable Count**: Các trụ nằm ngoài hoặc chạm biên phải đưa vào `BoundaryException`, không được cộng vào `VerifiedBillablePoleCount`.
5. **Fail-closed khi thiếu SpatialPolicy**: Nếu không tìm thấy chính sách không gian hiệu lực cho ngày khảo sát, quy trình phân tích bản đồ phải từ chối `SPATIAL_POLICY_NOT_FOUND`.

---

## Danh mục Files Cần Tạo / Sửa Đổi

### Module Surveys
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Policies/SpatialPolicy.cs` (Entity chính sách không gian)
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Policies/ISpatialPolicyRepository.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs` (Bổ sung `VerifiedBillablePoleCount`, `CountVerifiedAt`, `BaselineMapVersionId`)
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SpatialPolicyConfiguration.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderConfiguration.cs`

### Module Farms
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/FarmBaseMapVersion.cs` (Thêm `PredecessorVersionId`, `IsBaseline`, `BaselineMapVersionId`, `ApprovedInternalAt`)
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/FarmBaseMapPublicationContext.cs` (Bổ sung tham số lineage)
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/FarmBaseMapVersionConfiguration.cs`

### Module Plants
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/PlantProfile.cs` (Aggregate root định danh cây trồng)
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/PlantProfileStatus.cs` (Active, NeedsReview, Retired, Replaced)
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/IPlantProfileRepository.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Mapping/PlantChangeEvent.cs` (Bổ sung `PlantProfileId`, `SourceSurveyOrderId`, `DecisionId`, audit)
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantProfileConfiguration.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantChangeEventConfiguration.cs`

### Database & Migrations
- Modify: `backend/src/BuildingBlocks/AgriDrone.Database/AgriDroneSchemaDbContext.cs`
- Create: EF Core Migration `V3_04MappingAndPlantIdentity`

### Application Handlers & Coordination
- Create: `backend/src/Modules/AgriDrone.Modules.Farms/Application/Features/Maps/ApproveInternalBaseMap/` (Approve bản đồ nội bộ, tạo Baseline hoặc Follow-up snapshot)
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Application/Features/Plants/ReviewPlantIdentityChanges/` (Duyệt biến động cây trồng)
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Orders/VerifyOrderBillablePoles/` (Tính toán và chốt số trụ tính tiền)

---

## Chi tiết Kế hoạch Triển khai (Bite-sized Tasks)

### Task 1: Xây dựng Entity `SpatialPolicy` (Chính sách Không gian có phiên bản)
**Files:**
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Policies/SpatialPolicy.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Policies/ISpatialPolicyRepository.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SpatialPolicyConfiguration.cs`

**Steps:**
- [ ] 1.1 Tạo `SpatialPolicy.cs` kế thừa `Entity`:
  - Thuộc tính: `StreamKey`, `VersionNumber`, `PredecessorVersionId`, `BoundaryToleranceM`, `MatchingDistanceThresholdM`, `RowBearingToleranceDeg`, `GridSpacingToleranceM`, `EffectiveFrom`, `EffectiveTo`, `CreatedBy`, `CreatedAt`, `ReasonCode`, `EvidenceReference`, `PolicyFingerprint`.
  - Factory method `Create(...)` có kiểm tra validation invariants (`BoundaryToleranceM > 0`, `MatchingDistanceThresholdM > 0`, `Utc` timestamps, hash SHA-256 fingerprint).
- [ ] 1.2 Tạo interface `ISpatialPolicyRepository.cs`: `GetEffectiveAsync(string streamKey, DateTimeOffset evaluatedAt, CancellationToken token)`.
- [ ] 1.3 Tạo `SpatialPolicyConfiguration.cs` ánh xạ bảng `survey.spatial_policies`, đặt check constraints cho dung sai dương và unique index `(stream_key, version_number)`.
- [ ] 1.4 Kiểm tra build: `dotnet build backend/AgriDrone.sln --no-restore --nologo`.

---

### Task 2: Nâng cấp `FarmBaseMapVersion` hỗ trợ Version Lineage & Baseline Separation
**Files:**
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/FarmBaseMapVersion.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/FarmBaseMapPublicationContext.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/FarmBaseMapVersionConfiguration.cs`

**Steps:**
- [ ] 2.1 Mở rộng `FarmBaseMapVersion.cs`:
  - Thêm `PredecessorVersionId` (nullable Guid), `IsBaseline` (bool), `BaselineMapVersionId` (nullable Guid), `ApprovedInternalAt` (nullable DateTimeOffset), `ApprovedInternalBy` (nullable Guid).
  - Phương thức `ApproveInternal(...)`: chuyển trạng thái sang duyệt nội bộ (`ApprovedInternal`) để pipeline Combo/matching sử dụng mà không public ra ngoài cho Owner.
  - Phương thức `Supersede(...)`: đánh dấu phiên bản cũ khi phiên bản mới được duyệt.
- [ ] 2.2 Cập nhật `FarmBaseMapPublicationContext.cs` nhận thêm `predecessorVersionId`, `isBaseline`, `baselineMapVersionId`.
- [ ] 2.3 Cập nhật `FarmBaseMapVersionConfiguration.cs`: cấu hình foreign key tự trỏ `PredecessorVersionId` (`Restrict`), cột `is_baseline`, `baseline_map_version_id`.
- [ ] 2.4 Kiểm tra build: `dotnet build backend/AgriDrone.sln --no-restore --nologo`.

---

### Task 3: Xây dựng Aggregate Root `PlantProfile` & Hoàn thiện `PlantChangeEvent`
**Files:**
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/PlantProfileStatus.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/PlantProfile.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Plants/IPlantProfileRepository.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Plants/Domain/Mapping/PlantChangeEvent.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantProfileConfiguration.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantChangeEventConfiguration.cs`

**Steps:**
- [ ] 3.1 Tạo `PlantProfileStatus.cs`: `Active`, `NeedsReview`, `Retired`, `Replaced`.
- [ ] 3.2 Tạo `PlantProfile.cs` kế thừa `AggregateRoot`:
  - `TenantId`, `FarmId`, `PhysicalPoleIdentifier` (mã trụ vật lý, ví dụ `R01-P05`), `BaselineLocation` (Point), `CurrentLocation` (Point), `BaselineMapVersionId` (Guid), `CurrentMapVersionId` (Guid), `Status` (`PlantProfileStatus`), `PredecessorProfileId` (nullable Guid), `CreatedAt`, `UpdatedAt`.
  - Phương thức `Relocate(Point newLocation, Guid mapVersionId, DateTimeOffset occurredAt)`: Cập nhật tọa độ khi có dịch chuyển do sai số GNSS, giữ nguyên ID.
  - Phương thức `Retire(DateTimeOffset occurredAt)`: Chuyển trạng thái sang `Retired`.
  - Phương thức `Replace(Guid newProfileId, DateTimeOffset occurredAt)`: Chuyển sang `Replaced`.
- [ ] 3.3 Mở rộng `PlantChangeEvent.cs`:
  - Thêm trường `PlantProfileId` (Guid), `SourceSurveyOrderId` (Guid), `SpatialPolicyVersionId` (Guid), `ManagerDecisionId` (nullable Guid).
  - Đảm bảo tính bất biến (append-only), không có phương thức mutate dữ liệu đã ghi.
- [ ] 3.4 Tạo `PlantProfileConfiguration.cs` và cập nhật `PlantChangeEventConfiguration.cs`:
  - Bảng `plant.plant_profiles`, spatial index trên `current_location`, unique index trên `(farm_id, physical_pole_identifier)` cho các profile đang `Active`.
- [ ] 3.5 Kiểm tra build: `dotnet build backend/AgriDrone.sln --no-restore --nologo`.

---

### Task 4: Mở rộng `SurveyOrder` hỗ trợ `VerifiedBillablePoleCount` & Seams
**Files:**
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderConfiguration.cs`

**Steps:**
- [ ] 4.1 Thêm vào `SurveyOrder.cs`:
  - `VerifiedBillablePoleCount` (int?): Số trụ hợp lệ trong ranh giới để tính tiền.
  - `CountVerifiedAt` (DateTimeOffset?): Thời điểm chốt số trụ.
  - `CountVerifiedBy` (Guid?): Quản lý viên hoặc hệ thống duyệt chốt số.
  - Phương thức `RecordVerifiedBillablePoleCount(int count, Guid verifiedBy, DateTimeOffset verifiedAt)`:
    - Invariants: `count >= 0`, `Status` phải ở trạng thái xử lý/nghiệm thu kỹ thuật, cập nhật `UpdatedAt`.
- [ ] 4.2 Cập nhật `SurveyOrderConfiguration.cs`:
  - Map cột `verified_billable_pole_count`, `count_verified_at`, `count_verified_by`.
  - Check constraint: `verified_billable_pole_count >= 0`.
- [ ] 4.3 Kiểm tra build: `dotnet build backend/AgriDrone.sln --no-restore --nologo`.

---

### Task 5: Tạo EF Core Migration `V3_04MappingAndPlantIdentity` & Cập nhật Snapshot
**Files:**
- Modify: `backend/src/BuildingBlocks/AgriDrone.Database/AgriDroneSchemaDbContext.cs`
- Create: Migration files trong `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/`

**Steps:**
- [ ] 5.1 Đăng ký `DbSet<SpatialPolicy>` và `DbSet<PlantProfile>` trong `AgriDroneSchemaDbContext.cs`.
- [ ] 5.2 Tạo migration:
  ```powershell
  dotnet ef migrations add V3_04MappingAndPlantIdentity --project backend/src/BuildingBlocks/AgriDrone.Database --startup-project backend/src/AgriDrone.Api --context AgriDroneSchemaDbContext
  ```
- [ ] 5.3 Kiểm tra model snapshot đồng bộ:
  ```powershell
  dotnet build backend/AgriDrone.sln --no-restore --nologo
  ```

---

### Task 6: Triển khai Application Handlers cho Mapping & Billable Pole Count
**Files:**
- Create: `backend/src/Modules/AgriDrone.Modules.Farms/Application/Features/Maps/ApproveInternalBaseMap/ApproveInternalBaseMapCommand.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Farms/Application/Features/Maps/ApproveInternalBaseMap/ApproveInternalBaseMapCommandHandler.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Orders/VerifyOrderBillablePoles/VerifyOrderBillablePolesCommand.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Orders/VerifyOrderBillablePoles/VerifyOrderBillablePolesCommandHandler.cs`

**Steps:**
- [ ] 6.1 Viết `ApproveInternalBaseMapCommandHandler`:
  - Nhận lệnh duyệt bản đồ từ Manager sau khi phân tích kỹ thuật xong.
  - Kiểm tra nếu là First Order: Đánh dấu `IsBaseline = true`, sinh Baseline pointer cho Farm.
  - Nếu là Follow-up Order: Giữ nguyên Baseline cũ, tạo `FarmBaseMapVersion` mới trỏ `PredecessorVersionId`.
- [ ] 6.2 Viết `VerifyOrderBillablePolesCommandHandler`:
  - Truy vấn các trụ cây đã nhận dạng của đơn hàng, đối chiếu ranh giới `FarmBoundary` bằng spatial predicate `ST_Within(plant.location, boundary.geometry)`.
  - Tính tổng số trụ nằm hoàn toàn bên trong ranh giới.
  - Ghi nhận `VerifiedBillablePoleCount` vào `SurveyOrder` và phát event `OrderBillablePolesVerifiedV3`.
- [ ] 6.3 Kiểm tra build: `dotnet build backend/AgriDrone.sln --no-restore --nologo`.

---

### Task 7: Cập nhật Memory Bank & Đóng gói Handoff
**Files:**
- Modify: `PROJECT_MEMORY.md`

**Steps:**
- [ ] 7.1 Cập nhật `PROJECT_MEMORY.md`: Ghi nhận tiến độ hoàn thành các tasks của V3-04.
- [ ] 7.2 Commit toàn bộ code sạch lên nhánh `LongTH`.

</details>
