# V3-04 Comprehensive Remediation — Canonical Implementation Plan

**Ngày lập:** 2026-11-16  
**Trạng thái:** READY FOR IMPLEMENTATION — chưa thực thi  
**Phạm vi:** V3-04 Mapping, Plant Identity, Spatial Lineage và Authoritative Pole Count Evidence  
**Mục tiêu:** Thay thế toàn bộ các đường xử lý chồng chéo bằng một pipeline duy nhất, đóng đủ invariant ở Domain và Database, hỗ trợ First Baseline lẫn Follow-up thật sự, và giữ customer publication ngoài V3-04.

> Tài liệu này là kế hoạch canonical để thực thi remediation. Nếu tài liệu tóm tắt bên ngoài repository khác với tài liệu này thì tài liệu này được ưu tiên.

---

## 1. Ràng buộc bắt buộc

- Tuyệt đối không chạy `dotnet test` trong V3-04.
- Chỉ được kiểm tra biên dịch bằng:
  ```powershell
  dotnet build backend/AgriDrone.sln --no-restore --nologo
  ```
- Không publish/release bản đồ cho khách hàng trong V3-04.
- `FarmBaseMapVersion.Publish(...)`, customer release và `FarmBaseMapPublishedV3` thuộc V3-06.
- Thiếu policy, sai tenant/farm/order, sai lineage, thiếu target profile, còn boundary exception hoặc vượt tolerance đều fail-closed bằng `Result<T>` và `AppError`.
- Snapshot, snapshot item, observation, plant change event và outbox event là append-only.
- Không update đè evidence đã chốt; correction/revision nghiệp vụ thuộc V3-05.
- Không commit code nếu người dùng chưa yêu cầu commit.

---

## 2. Các quyết định kiến trúc đã khóa

### 2.1 Một pipeline duy nhất

```text
MappingCandidatesReadyV4
    -> Ingest + freeze SpatialPolicy + create immutable map lineage
    -> Manager reviews MappingCandidateBatch
    -> ApproveFarmBaseMapInternallyCommand
    -> IFarmBaseMapInternalApprovalOrchestrator
    -> apply reviewed decisions atomically
    -> create authoritative pole-count snapshot + items
    -> update SurveyOrder through one domain method
    -> FarmBaseMapVersion.InternalApproved
    -> MappingCandidateBatch.Applied
    -> FarmBaseMapInternallyApprovedV1 outbox event
    -> Missions consumer completes the relevant technical mission
```

Không tồn tại command/handler thứ hai có thể approve map, verify pole count hoặc phát event tương đương.

### 2.2 Hai loại mapping

```csharp
public enum MappingKind
{
    FirstBaseline = 1,
    FollowUpSnapshot = 2
}
```

`MappingCandidatesReadyV4` bắt buộc có:

- `MappingKind`
- `SurveyOrderId`
- `MissionId`
- `CaptureAttemptId`
- `FarmId`
- `FarmBoundaryVersionId`
- `HandoffId`
- `JobId`
- `ModelVersionId`
- `ExpectedCurrentFarmBaseMapVersionId`
- `ProducedAt` không nullable và UTC
- toàn bộ candidate records

Handler phải đối chiếu `MappingKind` với `SurveyOrder.RequiresBaselineMapping`; không tin payload đơn phương.

### 2.3 State machine SurveyOrder

| Mapping kind | Trước ingest | Sau ingest | Trước internal approval | Sau internal approval |
|---|---|---|---|---|
| `FirstBaseline` | `BaselineInProgress` | `AwaitingBaselineReview` | `AwaitingBaselineReview` | `AwaitingPricing` |
| `FollowUpSnapshot` | `Processing` | giữ `Processing` | `Processing` và batch đã review/approved | giữ `Processing`; pipeline mission/V3-05 quyết định bước tiếp theo |

Follow-up không được gọi `MarkBaselineCandidatesReady`, không đi qua `AwaitingBaselineReview` và không dùng `RecordBaselinePublication`.

### 2.4 Lineage và baseline pointer

- Khóa row `farm.farms` bằng `FOR UPDATE` trước khi đọc pointer/latest map. Không dựa vào lock trên pointer chưa tồn tại.
- Chỉ cho phép một map `Draft` đang mở trên mỗi `(tenant_id, farm_id)` để lineage luôn tuyến tính.
- First baseline:
  - không được có baseline pointer;
  - `IsBaseline = true`;
  - `PredecessorVersionId = null`;
  - `BaselineMapVersionId = Id`;
  - pointer chỉ được tạo trong transaction internal approval sau khi map chuyển `InternalApproved`.
- Follow-up:
  - bắt buộc pointer tồn tại và trỏ tới map baseline cùng tenant/farm;
  - `IsBaseline = false`;
  - `PredecessorVersionId = latest accepted map` của farm;
  - `BaselineMapVersionId = pointer.CurrentBaselineMapVersionId`;
  - không cập nhật baseline pointer.
- Xóa `SetLineage` và `ConfigureLineage`; lineage chỉ được tạo qua factory methods.

### 2.5 SpatialPolicy freeze

- Thời điểm chọn policy duy nhất là `payload.ProducedAt`.
- Không dùng “capture time hoặc produced time” tùy ý.
- `ProducedAt` phải được validate UTC và không được sau thời điểm nhận message ngoài clock-skew cho phép của hệ thống.
- Snapshot vào `MappingCandidateBatch`:
  - `SpatialPolicyVersionId`
  - `SpatialPolicyFingerprint`
  - `BoundaryToleranceM`
  - `MatchingDistanceThresholdM`
  - `RowBearingToleranceDeg`
  - `GridSpacingToleranceM`
- Validator chỉ đọc các giá trị đã freeze trên batch; không query policy hiện hành khi approve và không hardcode tolerance.

### 2.6 Effective candidate position

Mọi phép đo và evidence đều dùng đúng một giá trị:

```csharp
var effectivePosition = candidate.CorrectedPosition ?? candidate.OriginalPosition;
```

Giá trị này được dùng cho boundary check, geodesic distance, observation, plant mutation và snapshot item.

### 2.7 Pole count và zero count

- `0` là pole count hợp lệ nếu không candidate nào đủ điều kiện.
- Không fallback từ `0` sang candidate count hoặc plant count khác.
- Mỗi candidate tạo đúng một snapshot item, kể cả candidate bị `Reject` hoặc nằm ngoài strict boundary.
- `PlantProfileId` trên snapshot item nullable cho candidate không tạo ra resultant profile.
- Billable count:

```csharp
var billableCount = items
    .Where(x => x.IsAccepted &&
                x.IsStrictlyInBoundary &&
                x.PlantProfileId.HasValue)
    .Select(x => x.PlantProfileId!.Value)
    .Distinct()
    .Count();
```

- Strict boundary dùng cùng một quy tắc PostGIS/NTS tương đương `ST_Within(point, boundary)`; điểm nằm đúng biên không được tính billable.

### 2.8 Idempotency toàn transaction

- Orchestrator khóa farm, draft, batch và order trước khi mutate.
- Nếu draft đã `InternalApproved`, load snapshot/outbox hiện hữu và trả lại cùng result; không chạy lại mutations.
- Evidence fingerprint được tính từ input/review evidence ổn định, trước mutations:
  - order, mission, capture attempt, map, boundary, batch;
  - spatial policy fingerprint;
  - sorted candidate evidence theo `MappingCandidateId`;
  - review decision, effective position, target plant/profile ID hoặc marker `NEW`/`REJECT`;
  - strict-boundary result và exclusion reason.
- Không đưa `VerifiedAt`, `VerifiedBy`, revision number hoặc ID ngẫu nhiên vừa sinh vào fingerprint.
- Serialization dùng `CultureInfo.InvariantCulture`, thứ tự field cố định và tọa độ canonical SRID 4326.
- Unique constraints:
  - `(survey_order_id, revision_number)`;
  - `(survey_order_id, evidence_fingerprint)`;
  - một snapshot cho mỗi `farm_base_map_version_id` trong V3-04;
  - một snapshot item cho mỗi `(snapshot_id, mapping_candidate_id)`.

---

## 3. Domain model đích

### 3.1 `FarmBaseMapVersion`

- Giữ `CreateFirstBaseline(...)` và `CreateFollowUp(...)`.
- Hai factory validate tenant/farm, version number, predecessor và baseline.
- Xóa hoàn toàn public lineage setters.
- `ApproveInternal(...)` chỉ nhận `Draft`, chuyển sang `InternalApproved` và lưu actor/time.
- Không gọi `Publish(...)` hoặc `Supersede(...)` trong pipeline V3-04.

### 3.2 `FarmBaselinePointer`

- `Id` là PK và luôn bằng `FarmId`.
- Unique key `(TenantId, FarmId)`.
- FK `(CurrentBaselineMapVersionId, TenantId, FarmId)` tới `FarmBaseMapVersion(Id, TenantId, FarmId)` với `Restrict`.
- Factory validate map được trỏ là baseline cùng scope.
- Pointer chỉ được tạo sau first baseline internal approval; follow-up không mutate pointer.

### 3.3 `MappingCandidateBatch`

- Thêm `MappingKind` và sáu trường SpatialPolicy snapshot.
- Đổi `MappingCandidateBatchStatus.Published` thành `Applied`.
- Đổi `MarkPublished(...)` thành `MarkApplied(...)`.
- `MarkApplied` chỉ nhận batch `Approved` và không cho gọi lặp với dữ liệu khác.

### 3.4 `SurveyOrderPoleCountSnapshotItem`

Các field tối thiểu:

- `Id`
- `SnapshotId`
- `TenantId`
- `FarmId`
- `MappingCandidateBatchId`
- `MappingCandidateId`
- `PlantProfileId?`
- `EffectivePosition`
- `ReviewDecision`
- `IsAccepted`
- `IsStrictlyInBoundary`
- `ExclusionReason?`

Entity bất biến sau khi tạo.

### 3.5 `SurveyOrderPoleCountSnapshot`

- Giữ actor/time/revision làm audit data nhưng loại chúng khỏi fingerprint.
- Revision đầu tiên của mỗi order là `1`; correction revision không được tạo trong V3-04.
- Snapshot sở hữu collection items bất biến.
- Count trên snapshot phải bằng count tính từ items.
- Không cho tạo snapshot với item thuộc batch/order/farm khác.

### 3.6 Hai domain methods trên `SurveyOrder`

```csharp
public void RecordInitialMapApproval(
    SurveyOrderPoleCountSnapshot snapshot,
    uint expectedVersion);

public void RecordFollowUpMapSnapshot(
    SurveyOrderPoleCountSnapshot snapshot,
    Guid pinnedBaselineMapVersionId,
    uint expectedVersion);
```

`RecordInitialMapApproval` phải kiểm tra:

- order thuộc first baseline và đang `AwaitingBaselineReview`;
- snapshot revision bằng `1`;
- chưa có verified snapshot;
- order/tenant/farm/boundary/map của snapshot khớp order;
- map trên snapshot chính là baseline;
- set map IDs, verified snapshot/count/audit và chuyển `AwaitingPricing` trong một method.

`RecordFollowUpMapSnapshot` phải kiểm tra:

- order là follow-up, đang `Processing`;
- snapshot revision bằng `1` và chưa có verified snapshot cho order này;
- snapshot cùng order/tenant/farm/boundary;
- baseline pin trên order khớp `pinnedBaselineMapVersionId`;
- map snapshot là follow-up của baseline đã pin;
- ghi verified snapshot/count và follow-up map ID;
- không thay đổi baseline pin và không tự chuyển order khỏi `Processing`.

`ApplyVerifiedPoleCountCorrection` không nằm trong V3-04; để V3-05 triển khai bằng command chính thức.

---

## 4. Database invariants và foreign keys

### 4.1 Principal/alternate keys cần có

- `FarmBaseMapVersion`: `(Id, TenantId, FarmId)`.
- `FarmBoundary`: `(Id, TenantId, FarmId)`.
- `SurveyOrder`: `(Id, TenantId, FarmId)`.
- `PlantProfile`: `(Id, TenantId, FarmId)` và `(Id, FarmId)` nếu `Plant` chưa có `TenantId`.
- `MappingCandidateBatch`: `(Id, TenantId, FarmId)`.
- `MappingCandidate`: `(Id, MappingCandidateBatchId)`.
- `SurveyOrderPoleCountSnapshot`: `(Id, TenantId, FarmId)`.

### 4.2 Required foreign keys

- `Plant(PlantProfileId, FarmId) -> PlantProfile(Id, FarmId)`, `Restrict`.
- `PlantProfileObservation(PlantProfileId, TenantId, FarmId) -> PlantProfile(...)`, `Restrict`.
- `PlantProfileObservation(SurveyOrderId, TenantId, FarmId) -> SurveyOrder(...)`, `Restrict`.
- `PlantProfileObservation(FarmBaseMapVersionId, TenantId, FarmId) -> FarmBaseMapVersion(...)`, `Restrict`.
- Observation phải lưu cả `MappingCandidateBatchId` và `MappingCandidateId`; composite FK tới candidate.
- Snapshot composite FKs tới order, map, boundary và batch.
- Snapshot FK tới SpatialPolicy; FK tới mission/capture attempt được cấu hình trong cross-module schema nếu principal keys tương ứng tồn tại.
- Snapshot item composite FK tới snapshot, candidate batch/candidate và optional plant profile.
- `SurveyOrder.VerifiedPoleCountSnapshotId -> SurveyOrderPoleCountSnapshot.Id`, `Restrict`.
- Mọi FK evidence dùng `Restrict`; không dùng cascade delete.

### 4.3 Required constraints/indexes

- Unique filtered: một baseline map trên `(tenant_id, farm_id)` với `is_baseline = true`.
- Unique filtered: một open draft trên `(tenant_id, farm_id)` với `status = 'DRAFT'`.
- Lineage check:
  - baseline: predecessor null và baseline ID bằng self ID;
  - follow-up: predecessor/baseline ID đều not null.
- Snapshot unique indexes đã nêu tại mục 2.8.
- Snapshot item check: accepted item có profile; rejected item có exclusion reason.
- Observation distance không âm.

---

## 5. Migration strategy — không phá dữ liệu

### Gate M0: xác định migration lỗi đã được apply chưa

- [ ] Kiểm tra source migration cuối và `AgriDroneSchemaDbContextModelSnapshot`.
- [ ] Kiểm tra `__EFMigrationsHistory` ở mọi database thuộc phạm vi người thực thi được phép truy cập.
- [ ] Ghi lại quyết định vào execution checklist.

Nhánh A — migration `20261116145210_V3_04MappingLineageAndEvidenceRemediation` chưa apply ở bất kỳ database dùng chung nào:

- [ ] Dùng `dotnet ef migrations remove` để remove migration cuối cùng cùng designer/snapshot delta.
- [ ] Không xóa thủ công file migration mà bỏ quên model snapshot.

Nhánh B — migration đã apply ở ít nhất một database dùng chung:

- [ ] Không remove và không rewrite migration đã phát hành.
- [ ] Tạo forward-only corrective migrations.
- [ ] Giữ lịch sử migration nhất quán giữa các môi trường.

### Migration E1 — enum độc lập

- [ ] Tạo `AddFarmBaseMapStatusInternalApproved`.
- [ ] `ALTER TYPE ... ADD VALUE IF NOT EXISTS` với `suppressTransaction: true`.
- [ ] `Down()` không cố xóa enum value; ghi chú rõ PostgreSQL enum rollback limitation.
- [ ] Migration schema sử dụng enum mới phải chạy sau E1.

### Migration S1 — schema remediation

- [ ] Tạo/sửa bảng, columns, keys, constraints và indexes từ mục 4.
- [ ] `farm_baseline_pointers` có cột `id`; backfill dùng `id = farm_id`.
- [ ] Tạo snapshot items và policy snapshot columns.
- [ ] Cột batch status hiện lưu `varchar`: backfill chính xác `Published` thành `Applied`, sau đó cập nhật enum C# và configuration; không xử lý nó như PostgreSQL enum.

### Backfill rules

- [ ] Partition map theo `(tenant_id, farm_id)`.
- [ ] Sắp xếp xác định bằng `version_number`, `created_at`, `id`.
- [ ] Map đầu tiên là baseline; `baseline_map_version_id = id`; predecessor null.
- [ ] Follow-up trỏ predecessor liền trước và baseline đầu tiên.
- [ ] Chỉ insert một pointer mỗi farm, gồm đủ `id`, `farm_id`, `tenant_id`, `current_baseline_map_version_id`, audit fields.
- [ ] Trước khi tạo constraints, fail migration với thông báo rõ nếu dữ liệu hiện tại có duplicate version, nhiều baseline hoặc lineage không thể suy ra an toàn.

### SpatialPolicy seed

- [ ] Dùng đúng columns:
  `id`, `stream_key`, `version_number`, `predecessor_version_id`, `boundary_tolerance_m`, `matching_distance_threshold_m`, `row_bearing_tolerance_deg`, `grid_spacing_tolerance_m`, `policy_fingerprint`, `effective_from`, `effective_to`, `created_by`, `created_at`, `reason_code`, `evidence_reference`.
- [ ] Seed values phải khớp `SpatialPolicy.CreateDefault`.
- [ ] Fingerprint phải khớp chính xác thuật toán domain, không ghi chuỗi placeholder.
- [ ] Seed idempotent bằng conflict target ổn định và không update đè policy đã khác fingerprint.

---

## 6. File impact map

### Integration contracts và messaging

- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Mapping/MappingCandidatesReadyV4.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Mapping/FarmBaseMapInternallyApprovedV1.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/IntegrationEventTypes.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/IntegrationEventDescriptors.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/IntegrationConsumerNames.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/Validation/V3BusinessContractValidators.cs`

### Producer/consumer pipeline

- `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Persistence/MissionPlantGpsPublisher.cs`
- V4 processor/handler trong `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/`
- Internal-approved processor/handler trong `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Integration/`
- DI registrations trong Database và Missions modules.

### Domain và persistence

- `FarmBaseMapVersion`, `FarmBaselinePointer`, `MappingCandidateBatch`, `MappingCandidate`.
- `Plant`, `PlantProfile`, `PlantProfileObservation`.
- `SurveyOrder`, `SurveyOrderPoleCountSnapshot`, entity mới `SurveyOrderPoleCountSnapshotItem`.
- Tất cả configuration tương ứng.
- `MappingPublicationDbContext`, `AgriDroneSchemaDbContext`, module DbContexts và `CrossModuleRelationshipConfiguration`.

### Application/API

- `IFarmBaseMapInternalApprovalOrchestrator`.
- `FarmBaseMapInternalApprovalOrchestrator`.
- `ApproveFarmBaseMapInternallyCommand` và handler.
- `SystemManagerFarmBaseMapsController` route `/approve-internal`.
- `BaselineMappingBatchValidator`.

---

## 7. Trình tự thực thi chi tiết

### Task 0 — Đánh dấu remediation và chụp trạng thái

- [ ] Cập nhật `PROJECT_MEMORY.md`: V3-04 từ Done thành Remediation In Progress.
- [ ] Cập nhật `docs/plans/v3-04-execution-checklist.md`: bỏ Accepted/Completed trước khi code.
- [ ] Ghi branch/HEAD và danh sách file đang dirty; không ghi đè thay đổi không liên quan.
- [ ] Hoàn thành Gate M0 và chọn migration branch A hoặc B.

### Task 1 — Chuẩn hóa domain invariants và terminology

- [ ] Khóa factories/lineage của `FarmBaseMapVersion`.
- [ ] Chuẩn hóa `FarmBaselinePointer` và lifecycle pointer.
- [ ] Thêm `MappingKind`, policy snapshot fields và đổi batch `Published` thành `Applied`.
- [ ] Tạo snapshot item immutable.
- [ ] Sửa snapshot fingerprint/count invariants.
- [ ] Thêm hai SurveyOrder domain methods từ mục 3.6.
- [ ] Thêm principal/alternate keys cần thiết ở entity configurations.

Checkpoint: không còn public `SetLineage`/`ConfigureLineage`; không còn `MarkPublished` trên mapping batch.

### Task 2 — Hoàn thiện V4 contract từ producer đến handler

- [ ] Tạo `MappingCandidatesReadyV4` và validator đầy đủ.
- [ ] Producer lấy `CaptureAttemptId`, boundary, expected map và `ProducedAt` thật; không dựng dữ liệu giả/default.
- [ ] Producer xác định `MappingKind` từ mission/order context và fail nếu context mâu thuẫn.
- [ ] Đăng ký event type, descriptor, consumer name, processor, handler và DI.
- [ ] Handler kiểm tra envelope actor/tenant/correlation/causation và payload context.
- [ ] Chốt chính sách V3 compatibility:
  - xử lý message V3 tồn đọng bằng adapter rõ ràng, hoặc
  - xác nhận không có backlog rồi remove V3 registration.
- [ ] Không để V3 và V4 cùng tạo batch cho một handoff; unique `HandoffId` bảo vệ idempotency.

### Task 3 — Ingest, locking và immutable lineage

- [ ] Bắt transaction và khóa `farm.farms` row `FOR UPDATE`.
- [ ] Reject nếu farm đang có open draft khác.
- [ ] Resolve SpatialPolicy tại chính xác `payload.ProducedAt` và freeze đủ sáu field.
- [ ] Validate boundary/order/mission/capture attempt cùng tenant/farm.
- [ ] First: gọi `CreateFirstBaseline`, tạo batch và chuyển order tới `AwaitingBaselineReview`.
- [ ] Follow-up: yêu cầu pointer + pinned baseline hợp lệ, gọi `CreateFollowUp`, tạo batch và giữ order `Processing`.
- [ ] Không gọi lineage setter sau khi entity đã được tạo.

### Task 4 — Review validation và geodesic evidence

- [ ] Validator dùng toàn bộ policy snapshot values; xóa các số `0.3`, `0.5`, `1.0`, `1.5` hardcode trong flow này.
- [ ] Mọi decision dùng `effectivePosition`.
- [ ] `MatchExisting`: target plant/profile active, cùng scope; distance thật không vượt `MatchingDistanceThresholdM`; ghi observation thật.
- [ ] `Relocate`: target active và cùng scope; distance thật không vượt tolerance đã chốt; chỉ mutate sau khi tất cả validation pass.
- [ ] `Replace`: target active, new profile giữ predecessor và manager decision lineage.
- [ ] `CreateNew`: tạo plant/profile link 1-1.
- [ ] `Reject`: không tạo resultant profile; snapshot item có exclusion reason.
- [ ] Boundary exceptions query theo tenant, farm, order, mission và boundary version; còn bất kỳ unresolved row liên quan thì fail.

### Task 5 — Canonical internal approval orchestrator

- [ ] Tạo `IFarmBaseMapInternalApprovalOrchestrator` và result type không dùng từ “Published”.
- [ ] Bắt transaction qua execution strategy hiện có.
- [ ] Lock farm/draft/batch/order; xử lý retry đã hoàn tất trước mutation.
- [ ] Validate toàn bộ candidate set trước khi mutate bất kỳ Plant/Profile nào.
- [ ] Tính canonical fingerprint trước mutation.
- [ ] Apply decisions và tạo immutable change events/observations.
- [ ] Tạo đúng một snapshot item cho mỗi candidate.
- [ ] Tính distinct billable count; chấp nhận `0`.
- [ ] Tạo/reuse snapshot theo fingerprint và unique constraints.
- [ ] First: gọi `RecordInitialMapApproval` rồi tạo baseline pointer.
- [ ] Follow-up: gọi `RecordFollowUpMapSnapshot`; không đổi pointer.
- [ ] Gọi `draft.ApproveInternal`, `batch.MarkApplied`.
- [ ] Emit đúng một `FarmBaseMapInternallyApprovedV1` trong cùng transaction/outbox.

Event tối thiểu gồm:

- `ApprovalId`
- `CausationId`
- `MappingKind`
- `SurveyOrderId`
- `SourceMissionId`
- `CaptureAttemptId`
- `FarmId`
- `FarmBoundaryVersionId`
- `FarmBaseMapVersionId`
- `BaselineMapVersionId`
- `MappingCandidateBatchId`
- `PoleCountSnapshotId`
- `EvidenceFingerprint`
- `VerifiedBillablePoleCount`
- `ApprovedBy`
- `ApprovedAt`

### Task 6 — Mission consumer và downstream semantics

- [ ] Tạo handler/processor cho `FarmBaseMapInternallyApprovedV1`.
- [ ] Validate envelope/payload và mission tenant/farm/order/capture attempt.
- [ ] First baseline: hoàn tất technical baseline mapping mission và mở các mission phụ thuộc nội bộ.
- [ ] Follow-up: hoàn tất đúng source mission; không chạy logic customer publication và không giả lập baseline publication.
- [ ] Consumer idempotent theo `ApprovalId`/inbox.
- [ ] Không gọi `ApplyPublishedFarmBaseMap`; tạo domain method mang nghĩa internal approval/completion.

### Task 7 — EF configurations và migrations

- [ ] Cấu hình toàn bộ keys/FKs/checks/indexes mục 4 trên cả schema context và runtime contexts liên quan.
- [ ] Đăng ký DbSet/configuration snapshot items.
- [ ] Thực hiện Migration E1 và S1 theo nhánh M0 đã chọn.
- [ ] Review thủ công migration Up/Down, raw SQL, enum transaction boundary, seed và backfill.
- [ ] Đối chiếu model snapshot với entity configurations.
- [ ] Không chạy `database update` nếu người dùng chưa yêu cầu.

### Task 8 — Xóa bypass, route cũ và semantic “Published” sai chỗ

- [ ] Xóa `PublishFarmBaseMapCommand` khỏi V3-04 API/application path.
- [ ] Xóa `ApproveInternalBaseMapCommand` và handler bypass.
- [ ] Thu hồi/xóa `VerifyOrderBillablePolesCommand` khỏi public workflow.
- [ ] Đổi endpoint thành `/base-map-drafts/{draftId}/approve-internal`.
- [ ] Xóa V3-04 producer/consumer/DI dùng `FarmBaseMapPublishedV3`.
- [ ] Giữ contract `FarmBaseMapPublishedV3` dormant cho V3-06 nếu cần, nhưng không có V3-04 code path produce/consume nó.
- [ ] Xóa/đổi `FarmBaseMapPublicationOrchestrator`, result names, audit action names và log messages mang nghĩa publication.
- [ ] Xóa `OrderPoleEvidenceReader` fallback `billableCount == 0`.
- [ ] Không để `RecordBaselinePublication` được gọi từ canonical V3-04 flow.

Static scans bắt buộc:

```powershell
rg -n "PublishFarmBaseMapCommand|ApproveInternalBaseMapCommand|VerifyOrderBillablePolesCommand" backend/src
rg -n "FarmBaseMapPublishedV3|ApplyPublishedFarmBaseMap" backend/src
rg -n "ConfigureLineage|SetLineage|MarkPublished" backend/src
rg -n "ConfirmedPublishablePoleCount <= 0|billableCount == 0" backend/src
```

Mỗi kết quả còn lại phải được phân loại rõ: V3-06 dormant/legacy hợp lệ hoặc lỗi chưa dọn; không chấp nhận bỏ qua mơ hồ.

### Task 9 — Verification và đóng remediation

- [ ] Review diff theo module boundary và xác nhận không có thay đổi ngoài phạm vi.
- [ ] Chạy duy nhất:
  ```powershell
  dotnet build backend/AgriDrone.sln --no-restore --nologo
  ```
- [ ] Kết quả bắt buộc: `0 Warning`, `0 Error`.
- [ ] Không chạy bất kỳ test command nào.
- [ ] Cập nhật execution checklist bằng bằng chứng build và static scans.
- [ ] Chỉ khi tất cả exit gates đạt mới cập nhật `PROJECT_MEMORY.md` thành V3-04 Remediation Completed.
- [ ] Chỉ commit khi người dùng yêu cầu; commit không được chứa file ngoài phạm vi.

---

## 8. Exit gates

- [x] G1 — Chỉ còn một command/orchestrator internal approval.
- [x] G2 — V4 producer → outbox → processor → handler → batch hoạt động đầy đủ cho cả hai mapping kinds.
- [x] G3 — Follow-up không bị chặn bởi baseline state/flag và không thay đổi baseline pointer.
- [x] G4 — First-baseline race được chặn bằng farm-row lock và DB unique constraints.
- [x] G5 — Không còn public lineage setter; lineage được cố định lúc ingest.
- [x] G6 — SpatialPolicy được freeze tại `ProducedAt`; không còn tolerance hardcode/fallback.
- [x] G7 — Match/relocate dùng geodesic distance từ effective reviewed position.
- [x] G8 — Snapshot có item-level evidence, count distinct profile, zero hợp lệ và fingerprint canonical.
- [x] G9 — Retry sau commit trả cùng result và không tạo duplicate mutation/event/snapshot.
- [x] G10 — Composite FKs ngăn cross-tenant/cross-farm/cross-batch references.
- [x] G11 — V3-04 không produce/consume publication event và không gọi publication domain methods.
- [x] G12 — Migration strategy phù hợp trạng thái đã apply/chưa apply; seed/backfill đúng schema và fail-closed.
- [x] G13 — Build đạt 0 Warning/0 Error; tests not run.
- [x] G14 — Memory/checklist chỉ đánh dấu Completed sau khi toàn bộ gate đạt.

---

## 9. Definition of done

Remediation chỉ hoàn thành khi code, schema model, migration, outbox contracts, API naming, state transitions và documentation cùng mô tả một nghiệp vụ duy nhất: **V3-04 chỉ phê duyệt kỹ thuật nội bộ và chốt evidence; V3-06 mới phát hành cho khách hàng**.

Không chấp nhận trạng thái “build được nhưng còn đường bypass”, “follow-up để xử lý sau”, “zero count dùng fallback”, hoặc “migration chỉ đúng trên database trống”.
