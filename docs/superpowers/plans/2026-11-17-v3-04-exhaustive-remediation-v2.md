# V3-04 Canonical Remediation V2 — Kế hoạch khắc phục triệt để

**Checkpoint 2026-11-25.** Đã triển khai mapping, spatial policy, lineage trụ và count snapshot. Remediation G0–G18 đã đóng ở checkpoint V3-04.

- [ ] Nghiệm thu mapping/Baseline, lineage, PostGIS, migration/preflight và race ở V3-10. [Acceptance matrix](../../testing/v3-business-acceptance.md)

[Backlog V3-06 đến V3-10](../../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#4-status-và-dependency-roadmap)

<details>
<summary>Hồ sơ triển khai và evidence theo từng checkpoint</summary>

Các trạng thái, checklist và số liệu bên dưới ghi theo thời điểm lập từng phần.

> **Trạng thái:** `COMPLETED 2026-11-18` — V3-04 đã đạt G0–G18 theo Nhánh A.
> **Phạm vi:** Domain → Persistence → Application → Integration → API → Migrations → Documentation.  
> **Không chạy test:** tuyệt đối không chạy `dotnet test`; chỉ được chạy build theo quy định dự án ở bước cuối.  
> **Mục tiêu:** sửa tận gốc, xóa đường xử lý sai thay vì chồng thêm fallback hoặc pipeline thứ hai.

## 0. Tài liệu và nguyên tắc bắt buộc

Tài liệu đối chiếu:

- `PROJECT_MEMORY.md`
- `docs/Codex-Plan/BE1-Core-Business-Implementation-Plan.md`
- `docs/Codex-Plan/Business-Rules-v3.md`
- `docs/superpowers/plans/2026-11-16-v3-04-comprehensive-remediation.md`

Các nguyên tắc không được thương lượng:

1. V3-04 chỉ tạo trạng thái `InternalApproved`; `Published` thuộc V3-06.
2. Chỉ còn một pipeline ghi map: `MappingCandidatesReadyV4` → review → internal approval → immutable snapshot → outbox → Missions consumer.
3. Không dùng `Guid.NewGuid()`, `UtcNow`, `Guid.Empty`, số `0`, chuỗi rỗng hoặc ID của thực thể khác để thay cho metadata nguồn bị thiếu.
4. `PlantId` và `PlantProfileId` là hai định danh khác nhau; không được dùng thay thế cho nhau.
5. Evidence, snapshot, change event và approval history là append-only; tất cả FK dùng `Restrict`.
6. Mọi query/mutation phải giữ scope `TenantId + FarmId` và thêm `OrderId/MissionId/BatchId` khi dữ liệu có các scope đó.
7. Không tự giả định migration chưa được áp dụng. Phải hoàn thành Gate M0 trước khi xóa hoặc sửa migration.
8. Không sửa tay migration rồi để model snapshot lệch. Migration, designer và `AgriDroneSchemaDbContextModelSnapshot.cs` phải cùng phản ánh một model.
9. Lỗi nghiệp vụ dự kiến phải trả `Result/AppError` hoặc `InboxHandlerResult`; không dùng exception làm luồng điều khiển bình thường.
10. Chưa đạt toàn bộ exit criteria thì `PROJECT_MEMORY.md` và checklist phải để `Remediation In Progress`.

## 1. Những điểm sai trong bản plan cũ đã được loại bỏ

Bản plan trước không được thực thi nguyên trạng vì còn các lỗi sau:

1. Không được khẳng định hai migration chưa apply chỉ vì đang làm trên nhánh `LongTH`; hai migration đã nằm trong lịch sử Git. Phải kiểm tra `__EFMigrationsHistory` của mọi database liên quan.
2. Không được backfill `capture_attempt_id = mission_id`. Hai ID này thuộc hai aggregate khác nhau.
3. Không được backfill toàn bộ batch cũ thành `FirstBaseline`. Phải suy luận từ order + map lineage; không xác định được thì dừng migration.
4. Không được đo khoảng cách trên geometry SRID 4326 bằng `Geometry.Distance`; kết quả là độ, không phải mét.
5. Không được yêu cầu client cung cấp metadata rồi coi đó là dữ liệu có thẩm quyền. Client chỉ gửi ID tham chiếu; server phải load và xác thực bản ghi nguồn đã lưu.
6. Không dùng `throw InvalidOperationException` cho idempotency/conflict dự kiến trong consumer.
7. Unique index không chỉ dùng `FarmId`; phải dùng `(TenantId, FarmId)` để thể hiện đúng isolation.
8. Không được chỉ sửa 12 mục rồi bỏ qua bốn lỗi đã biết: boundary-exception scope, quan hệ Plant/Profile 1–1, snapshot domain invariants và tài liệu đánh dấu hoàn thành sớm.

## 2. Luồng dữ liệu canonical sau khi sửa

```text
Persisted CaptureAttempt + Mission + completed Mapping AI job
                         │
                         ▼
MissionPlantGpsPublisher (chỉ đọc và xác thực metadata thật)
                         │ MappingCandidatesReadyV4
                         ▼
MappingCandidatesReadyV4Handler
  - FOR UPDATE Farm
  - kiểm tra order/mission/capture/boundary/job/model
  - kiểm tra First/Follow-up và stale predecessor
  - resolve SpatialPolicy tại ProducedAt
  - tính boundary distance thật bằng PostGIS geography
  - tạo đúng một Draft + MappingCandidateBatch
                         │
                         ▼
Manager review tất cả candidate
                         │
                         ▼
FarmBaseMapInternalApprovalOrchestrator
  - FOR UPDATE Farm
  - PlantId → Plant → PlantProfileId → PlantProfile
  - tạo immutable PlantChangeEvent/Observation
  - tạo snapshot items + canonical fingerprint
  - InternalApproved, không Published
                         │ FarmBaseMapInternallyApprovedV1
                         ▼
Missions consumer xác thực Mission/CaptureAttempt và hoàn tất technical mission
```

## 3. Ma trận nguồn dữ liệu có thẩm quyền

| Trường | Nguồn duy nhất | Quy tắc |
|---|---|---|
| `TenantId`, `FarmId`, `SurveyOrderId` | Mission + SurveyOrder đã lưu | Payload/envelope phải khớp; không khớp là permanent conflict |
| `MissionId` | Route/mission đã lưu | Mission phải thuộc tenant/farm/order |
| `CaptureAttemptId` | `DroneMission.CaptureAttemptId` và `survey.capture_attempts` | Attempt phải có cùng tenant/farm/order/mission; không nhận fallback |
| `FarmBoundaryVersionId` | Mission đã pin | Boundary phải thuộc đúng farm và là version được mission sử dụng |
| `MappingKind` | Mission purpose + `SurveyOrder.RequiresBaselineMapping` | BaselineMapping → FirstBaseline; chỉ PlantHealth/HarvestReadiness đã pin baseline → FollowUpSnapshot; `Capture` bị từ chối |
| `HandoffId` | `Idempotency-Key` của thao tác publish | Dùng trực tiếp ID ổn định này; không sinh GUID thứ hai |
| `JobId` | Completed `AiProcessingJob` loại Mapping | Client chỉ gửi `ProcessingJobId`; server load job và xác thực mission/status/type |
| `ModelVersionId` | `AiProcessingJob.ModelVersionId` | Bắt buộc có và FK đến `AiModelVersion` |
| `ProducedAt` | `AiProcessingJob.CompletedAt` | Bắt buộc UTC; không dùng thời điểm nhấn nút publish |
| spacing/bearing + model/algorithm version | `AiModelVersion` + immutable mapping-job snapshot | Bắt buộc không rỗng/hợp lệ; không lấy default từ API |
| SpatialPolicy | policy hiệu lực tại `ProducedAt` | Consumer resolve, snapshot ID/fingerprint và bốn tolerance |
| Boundary distance/classification | PostGIS tính từ point + pinned boundary | Không tin giá trị client hoặc AI gửi lên |
| Expected current map | `DroneMission.SourceMapVersionId` | FirstBaseline phải null; FollowUp phải có và phải bằng latest accepted predecessor |

## 4. Các task thực thi tuần tự

### Task 0 — Khóa trạng thái tài liệu trước khi sửa code

**Sửa:**

- `PROJECT_MEMORY.md`
- `docs/plans/v3-04-execution-checklist.md`

**Thực hiện:**

1. Đổi V3-04 từ `Done (Remediated)` thành `Remediation In Progress`.
2. Bỏ dấu hoàn thành ở G1–G14 chưa được xác minh.
3. Ghi rõ “tests not run; build chỉ chạy ở bước cuối”.
4. Không cập nhật lại `Done` cho tới Task 13.

**Done khi:** không tài liệu nào tuyên bố V3-04 đã hoàn thành.

---

### Task 1 — Gate M0: quyết định chiến lược migration trước mọi thay đổi schema

**Liên quan:**

- `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261116145210_V3_04MappingLineageAndEvidenceRemediation*`
- `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261117050302_V3_04CanonicalRemediationFinal*`
- `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/AgriDroneSchemaDbContextModelSnapshot.cs`

**Bước bắt buộc:**

1. Kiểm tra bảng `__EFMigrationsHistory` trên local dev, shared dev, staging và mọi DB từng dùng nhánh này:

   ```sql
   SELECT "MigrationId"
   FROM system.__ef_migrations_history
   WHERE "MigrationId" IN (
       '20261116131222_V3_04MappingAndPlantIdentity',
       '20261116145210_V3_04MappingLineageAndEvidenceRemediation',
       '20261117050302_V3_04CanonicalRemediationFinal'
   );
   ```

2. Lưu kết quả vào execution checklist. Không có bằng chứng thì coi trạng thái là `UNKNOWN` và dừng việc xóa migration.
3. Chọn đúng một nhánh:

   - **Nhánh A — chưa apply ở bất kỳ DB nào:** được phép remove hai migration theo thứ tự mới nhất → cũ nhất bằng EF CLI để snapshot được rollback đúng. Không xóa tay `.cs/.Designer.cs` trước khi chạy remove.
   - **Nhánh B — đã apply ở ít nhất một DB:** giữ nguyên migration ID. Sửa migration cũ đủ để fresh-install chạy được và tạo thêm forward corrective migration idempotent cho DB đã apply. Không remove history row, không drop/recreate dữ liệu production. Ghi rõ đây là controlled migration-history repair.

4. Kiểm tra model drift trước khi tạo migration:

   - Liệt kê mọi thay đổi model chưa có migration.
   - Tách pricing/appointment/post-flight khỏi migration V3-04.
   - Nếu model hiện tại vẫn chứa thay đổi ngoài V3-04 chưa được migrate, phải tạo/khôi phục migration đúng phase cho chúng trước; không được xóa dòng khỏi migration V3-04 trong khi snapshot vẫn chứa model đó.

**Điều cấm:**

- Không giả định “nhánh local” đồng nghĩa “chưa apply”.
- Không tạo migration thứ ba để sửa một migration không thể chạy trên fresh database.
- Không dùng `dotnet ef database update` trong task lập kế hoạch này.

**Done khi:** có bằng chứng chọn A hoặc B và danh sách model drift ngoài phạm vi bằng 0.

---

### Task 2 — Sửa nguồn provenance ở Missions/API, không bịa metadata

**Sửa:**

- `backend/src/AgriDrone.Api/Contracts/Missions/MissionPlantGpsPublishPayload.cs`
- `backend/src/AgriDrone.Api/Controllers/SystemManagerMissionsController.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Mapping/MappingCandidatesReadyV4.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Mapping/MappingCandidateV3.cs`
- `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/Validation/V3BusinessContractValidators.cs`
- `backend/src/Modules/AgriDrone.Modules.Missions/Application/Abstractions/Missions/MissionPlantGpsPublishRequest.cs`
- `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Persistence/MissionPlantGpsPublisher.cs`
- `backend/src/Modules/AgriDrone.Modules.Missions/Domain/Processing/AiProcessingJob.cs` nếu chưa có factory cho Mapping job
- `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/Staging/MappingCandidate.cs`
- `backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/MappingCandidateConfiguration.cs`

**Thay đổi chính xác:**

1. Thu gọn publish API thành dữ liệu tham chiếu: `SurveyOrderId` + `ProcessingJobId`; candidates, row/plant spacing và grid bearing phải đọc từ dữ liệu GPS/job snapshot đã lưu. Nếu cần giữ các field cũ để tương thích API thì server phải so sánh exact với persisted data rồi bỏ qua chúng, không dùng chúng làm nguồn canonical.
2. Xóa khỏi request nội bộ các nullable fallback: `CaptureAttemptId`, `FarmBoundaryVersionId`, `HandoffId`, `ModelVersionId`, `MappingKind`, `ExpectedCurrentFarmBaseMapVersionId` nếu chúng chỉ dùng để override server state.
3. `MissionPlantGpsPublisher` load đồng thời:

   - Mission đúng tenant/farm/order.
   - CaptureAttempt theo `mission.CaptureAttemptId`, cùng tenant/farm/order và `MissionId == mission.Id`.
   - completed `AiProcessingJob` theo `ProcessingJobId`, cùng mission, `JobType == Mapping`, `Status == Completed`, có `ModelVersionId` và `CompletedAt`.
   - `AiModelVersion` tương ứng.

4. Derive payload:

   - `HandoffId = idempotencyKey`.
   - `JobId = processingJob.Id`.
   - `ModelVersionId = processingJob.ModelVersionId.Value`.
   - `ProducedAt = processingJob.CompletedAt.Value`.
   - `CaptureAttemptId = mission.CaptureAttemptId.Value`.
   - `FarmBoundaryVersionId = mission.FarmBoundaryVersionId.Value`.
   - `ExpectedCurrentFarmBaseMapVersionId = mission.SourceMapVersionId`.
   - `GridBearingDeg`, `RowSpacingM`, `PlantSpacingM` lấy từ immutable mapping-job snapshot.
   - model version lấy từ `AiModelVersion`; algorithm version lấy từ immutable mapping-job snapshot. Thiếu thì conflict, không default.

5. MappingKind whitelist:

   - `BaselineMapping` + order requires baseline + source map null → `FirstBaseline`.
   - `PlantHealth` hoặc `HarvestReadiness` + order không requires baseline + source map có giá trị → `FollowUpSnapshot`.
   - `Capture` hoặc tổ hợp khác → conflict.

6. Idempotency lookup chỉ công nhận event type V4; bỏ `BaselineMappingCandidatesReadyV3` khỏi điều kiện thành công.
7. Xóa `TimeProvider` khỏi publisher nếu sau thay đổi không còn dùng. Thời gian envelope có thể là thời điểm enqueue, nhưng `payload.ProducedAt` bắt buộc là job completion time.
8. Trong `MappingCandidatesReadyV4`, xóa default `yolo-v8`, `orchard-grid-v1`, `v1`; `ModelVersion` và `AlgorithmVersion` phải là trường required được server derive.
9. `BoundaryPolicyVersion` không lấy từ producer. Chọn một cách duy nhất:

   - canonical: xóa nó khỏi contract V4 và để V4 handler gán `batch.BoundaryPolicyVersion` từ `effectivePolicy.StreamKey + VersionNumber`;
   - nếu bắt buộc giữ field contract trong một release tương thích, đánh dấu obsolete, handler bỏ qua giá trị payload và vẫn persist giá trị từ resolved policy. Sau release tương thích phải xóa field.

   Không được để payload override policy đã resolve.
10. Candidate identity không được sinh ngẫu nhiên khi ingest:

   - publisher tạo `CandidateId` ổn định từ persisted source, ví dụ UUID v5 namespace `processingJob.Id` + normalized `treeId`;
   - retry cùng job/tree phải cho cùng CandidateId;
   - `SourceObservationId` chỉ có khi thật sự tồn tại observation nguồn. Nếu CSV row không có observation GUID, đổi field domain/DB thành nullable; không dùng `Guid.NewGuid()` thay thế.

**Nếu hiện tại upload CSV chưa tạo Mapping AI job:**

1. Thêm immutable `MappingProcessingSnapshot` chứa tối thiểu `SourceVersion`, `GridBearingDeg`, `RowSpacingM`, `PlantSpacingM`, `AlgorithmVersion`, input fingerprint và output fingerprint.
2. Thêm factory `AiProcessingJob.RecordCompletedMappingRun(...)` đặt `JobType = Mapping`, `Status = Completed`, model ID thật, input/output manifest thật, StartedAt/CompletedAt thật.
3. Gọi factory ở bước processing/import completion, trước publish. Publisher chỉ đọc job đã hoàn thành; không tạo job ngay lúc publish.

**Done khi static scan trong publisher không còn:**

```text
request.HandoffId ?? Guid.NewGuid()
request.JobId ?? Guid.NewGuid()
request.ModelVersionId ?? Guid.NewGuid()
ProducedAt = UtcNow
mọi non-Baseline purpose => FollowUpSnapshot
```

---

### Task 3 — Truyền `CaptureAttemptId` xuyên suốt batch

**Sửa:**

- `backend/src/Modules/AgriDrone.Modules.Farms/Domain/Maps/Staging/MappingCandidateBatch.cs`
- `backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/MappingCandidateBatchConfiguration.cs`
- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/MappingCandidatesReadyV4Handler.cs`

**Thực hiện:**

1. Thêm property `Guid CaptureAttemptId { get; private set; }` ngay sau `MissionId`.
2. Thêm tham số bắt buộc vào `MappingCandidateBatch.Create`; gọi `DomainGuard.NotEmpty` và gán property.
3. EF map `capture_attempt_id uuid NOT NULL`.
4. Tạo alternate key tối thiểu `(Id, TenantId, FarmId)` đã có; thêm index `(CaptureAttemptId, MissionId)` phục vụ truy vết.
5. V4 handler xác thực CaptureAttempt trước khi tạo draft:

   ```text
   attempt.Id == payload.CaptureAttemptId
   attempt.TenantId == order.TenantId
   attempt.FarmId == order.FarmId
   attempt.OrderId == order.Id
   attempt.MissionId == payload.MissionId
   ```

6. Payload thiếu hoặc mismatch là permanent failure; không retry vì đây là dữ liệu sai context.

**Done khi:** batch, snapshot, fingerprint và event đều đọc từ cùng `batch.CaptureAttemptId`.

---

### Task 4 — Làm V4 ingestion thật sự fail-closed và chống stale lineage

**Sửa:**

- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/MappingCandidatesReadyV4Handler.cs`
- Có thể thêm service/query chuyên tính spatial trong `AgriDrone.Database/Mapping` để handler không phình to.

**Thứ tự xử lý bắt buộc trong cùng transaction:**

1. Load order và kiểm tra envelope tenant/farm.
2. Idempotency theo `HandoffId`: nếu đã có batch thì payload identity phải khớp batch cũ; cùng handoff nhưng khác order/mission/capture/job là permanent conflict, không trả completed mù quáng.
3. Lock Farm bằng `FOR UPDATE` với cả `id` và `tenant_id`.
4. Load và xác thực Mission, CaptureAttempt, Boundary, Job/Model references.
5. Kiểm tra không có draft mở cho `(TenantId, FarmId)`.
6. Resolve SpatialPolicy hiệu lực tại `ProducedAt`; snapshot ID, fingerprint và bốn giá trị vào batch.
7. FirstBaseline:

   - order requires baseline.
   - pointer không tồn tại.
   - `ExpectedCurrentFarmBaseMapVersionId` phải null.
   - chưa có bất kỳ map version nào cho tenant/farm; `maxVersionNumber` phải bằng 0.
   - `FarmBaseMapVersion.CreateFirstBaseline` luôn tạo version 1 và self-reference `BaselineMapVersionId = Id`; không nhận một version number lớn hơn 1 từ context.

8. FollowUpSnapshot:

   - order không requires baseline.
   - pointer phải tồn tại và thuộc đúng tenant/farm.
   - `order.BaselineMapVersionId` bắt buộc có và bằng `pointer.CurrentBaselineMapVersionId`.
   - `payload.ExpectedCurrentFarmBaseMapVersionId` bắt buộc có.
   - latest accepted predecessor là map có status `InternalApproved` hoặc `Published`, version cao nhất trong tenant/farm.
   - expected current map phải bằng latest accepted predecessor.
   - mission `SourceMapVersionId` cũng phải bằng expected current map.
   - mọi mismatch trả permanent failure `StalePredecessorMap`.

9. Version number tính sau khi đã giữ Farm lock. Unique index DB vẫn là hàng rào cuối.
10. `FarmBaselinePointer` được tạo đúng một lần khi first baseline được internal approve; V3-04 không gọi `UpdateBaseline` cho follow-up. Xóa `UpdateBaseline` nếu không có use case ngoài V3-04, hoặc hạ xuống private/internal chỉ dành cho change-control phase sau; không để application handler hiện tại gọi được.

**Không được:** dùng `latestApprovedMap ?? baselineMap` nếu expected predecessor không được xác thực.

---

### Task 5 — Tính boundary evidence bằng mét và dùng đủ SpatialPolicy

**Sửa:**

- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/MappingCandidatesReadyV4Handler.cs`
- `backend/src/Modules/AgriDrone.Modules.Farms/Application/Services/BaselineMappingBatchValidator.cs`

**Quy tắc spatial canonical:**

1. Luôn tự tính lại trên server; bỏ việc ưu tiên `c.BoundaryClassification`, `c.MeasuredBoundaryDistanceM`, `c.BoundaryThresholdM`.
2. Với từng candidate SRID 4326, dùng PostGIS:

   ```sql
   strictly_inside = ST_Within(point_geometry, boundary_geometry)
   distance_m = ST_Distance(
       point_geometry::geography,
       ST_Boundary(boundary_geometry)::geography)
   ```

3. Không dùng `Geometry.Distance` trực tiếp trên SRID 4326.
4. Phân loại:

   - `distance_m <= BoundaryToleranceM` → `NEEDS_REVIEW` dù nằm trong hay ngoài.
   - `distance_m > tolerance` và `strictly_inside` → `IN_BOUNDARY`.
   - `distance_m > tolerance` và không strictly inside → `OUT_OF_BOUNDARY`.

5. `BoundaryThresholdM` luôn bằng policy snapshot; `MeasuredBoundaryDistanceM` là kết quả thật, không phải `tolerance` hoặc `0` giả.
6. `BaselineMappingBatchValidator`:

   - Nếu bất kỳ tolerance nào `<= 0`, trả validation error; bỏ fallback `0.5`.
   - `MatchingDistanceThresholdM`: chỉ dùng cho MatchExisting/Relocate target distance.
   - `GridSpacingToleranceM`: là độ lệch cho spacing, không dùng như một “duplicate distance” thay thế.
   - Plant spacing: nhóm theo row, sắp theo column; với từng cặp column kề nhau, khoảng cách geodesic phải lệch `PlantSpacingM` không quá tolerance.
   - Row spacing: nhóm theo column, sắp theo row; với từng cặp row kề nhau, khoảng cách geodesic phải lệch `RowSpacingM` không quá tolerance.
   - Row bearing: với từng cặp column kề nhau trong cùng row, tính initial bearing geodesic; circular difference modulo 180 với `GridBearingDeg` không vượt `RowBearingToleranceDeg`.
   - Không có cặp kề để đo thì ghi informational issue `InsufficientGridEvidence` nhưng không tự fail một vườn chỉ có một cây/row. Không được tạo số đo giả để pass validation.

7. Việc tính billable pole vẫn dùng strict `ST_Within`; candidate trên biên không được tính.

**Done khi:** không còn hardcode/fallback spatial và bốn policy field đều có consumer nghiệp vụ cụ thể.

---

### Task 6 — Xóa pipeline V3 khỏi runtime, không giữ implementation song song

**Sửa/xóa:**

- `backend/src/BuildingBlocks/AgriDrone.Database/DependencyInjection.cs`
- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/BaselineMappingCandidatesReadyV3Handler.cs`
- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/BaselineMappingCandidatesReadyV3Processor.cs`
- Producer/idempotency checks còn tham chiếu V3.

**Thực hiện:**

1. Gate trước khi xóa: kiểm tra inbox/outbox/broker không còn pending/retry V3 message. Ghi số lượng vào checklist.
2. Nếu backlog bằng 0: xóa đăng ký, processor và handler V3. Contract có thể giữ tạm để deserialize lịch sử nhưng không được có consumer runtime.
3. Nếu backlog khác 0: không chạy lại handler V3 cũ. Viết one-time controlled replay/adapter chuyển payload sang V4 chỉ khi có thể resolve đầy đủ metadata thật; message thiếu metadata phải quarantine, không bịa.
4. Xóa V3 khỏi điều kiện idempotency success của publisher.

**Done khi:** dependency graph/runtime chỉ có một writer là `MappingCandidatesReadyV4Handler`.

---

### Task 7 — Sửa dứt điểm `PlantId`/`PlantProfileId` và quan hệ 1–1

**Sửa:**

- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/FarmBaseMapInternalApprovalOrchestrator.cs`
- `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantConfiguration.cs`

**Orchestrator:**

1. Load `activePlantsById` key bằng `Plant.Id`, scope tenant/farm và lifecycle active/missing phù hợp.
2. Load `activeProfilesById` key bằng `PlantProfile.Id`, scope tenant/farm/status active.
3. Với MatchExisting, Relocate, Replace:

   ```text
   targetPlantId = CorrectedMatchedPlantId ?? ProposedMatchedPlantId
   targetPlant = activePlantsById[targetPlantId]
   targetProfileId = targetPlant.PlantProfileId (bắt buộc có)
   targetProfile = activeProfilesById[targetProfileId]
   ```

4. Không tra `targetPlantId` trực tiếp trong profile dictionary.
5. MatchExisting/Relocate resultant profile là target profile hiện hữu.
6. Replace tạo profile mới có predecessor là profile cũ; fingerprint không chứa GUID mới sinh.
7. Nếu Plant và Profile khác tenant/farm hoặc inactive, trả `AppError.Validation/Conflict`, không tự tạo profile thay thế.

**Persistence:**

1. Thêm unique filtered index cho một profile chỉ gắn tối đa một Plant:

   ```text
   UNIQUE (FarmId, PlantProfileId) WHERE PlantProfileId IS NOT NULL
   ```

   Nếu model có `TenantId` trên Plant thì dùng `(TenantId, FarmId, PlantProfileId)`.

2. Trước khi tạo index, migration phải query duplicate; có duplicate thì `RAISE EXCEPTION`, không tự chọn record để giữ.

**Done khi:** code dùng tên biến `targetPlantId`, `targetPlant`, `targetProfileId`, `targetProfile`, không còn biến mơ hồ `matchTargetId` dùng tra profile.

---

### Task 8 — Sửa orchestrator: CaptureAttempt, boundary scope, idempotency và không Published

**Sửa:**

- `backend/src/BuildingBlocks/AgriDrone.Database/Mapping/FarmBaseMapInternalApprovalOrchestrator.cs`

**Thực hiện:**

1. Snapshot/fingerprint/event đều dùng `batch.CaptureAttemptId`.
2. `SourceMissionId` bắt buộc có từ batch/draft; thiếu thì fail trước mutation. Xóa mọi `?? Guid.Empty` và `?? Guid.NewGuid()` cho source evidence.
3. Boundary exception query phải scope tối thiểu:

   ```text
   TenantId + FarmId + SurveyOrderId + MissionId + FarmBoundaryVersionId
   ```

   Nếu entity thiếu Order/Mission reference, bổ sung schema hoặc link qua source reference; không dùng query chỉ theo Farm + Boundary.

4. ApprovalId tạo đúng một lần trước fingerprint/outbox, dùng ổn định trong transaction và idempotency.
5. Approval retry:

   - cùng batch/map đã Applied + cùng snapshot fingerprint → trả lại kết quả cũ, không tạo item/profile/event mới.
   - batch/map đã Applied nhưng fingerprint/approval context khác → conflict.

6. Chỉ gọi `draft.ApproveInternal`; không set `PublishedBy/PublishedAt` và không gọi `Publish`.
7. Tạo snapshot items trong memory trước; chỉ add vào DbContext sau khi toàn bộ candidate đã validate để tránh state dở trong change tracker.
8. Xóa fallback dữ liệu cây/lưới:

   - không dùng `CorrectedRowIndex ?? ProposedRowIndex ?? 1`; decision tạo/di chuyển/thay thế cần row/column thì thiếu phải validation error;
   - không dùng `TreeId ?? "POLE-..."` hoặc `TreeId ?? "P-..."`; V4 producer đã yêu cầu persisted GPS row có TreeId, nên thiếu TreeId là corrupt payload;
   - plant code/pole identifier phải lấy từ TreeId đã normalize và kiểm tra unique trong farm, không sinh tên tạm trong approval.

**Done khi:** approval là atomic, retry không sinh bản ghi trùng và không còn published semantics.

---

### Task 9 — Canonical fingerprint và snapshot domain invariants

**Sửa:**

- `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderPoleCountSnapshot.cs`
- `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderPoleCountSnapshotItem.cs`
- `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs`

**Fingerprint canonical:**

1. Header bắt buộc gồm tenant, order, farm, mission, capture attempt, map, boundary, batch, policy fingerprint.
2. Sort items bằng `MappingCandidateId` theo ordinal/GUID stable order.
3. Chuẩn hóa số bằng invariant culture và precision cố định. Dùng longitude/latitude theo thứ tự rõ ràng; không đổi X/Y.
4. Token item:

   - CreateNew: `NEW|candidateId|position|row|column`.
   - MatchExisting: `MATCH|candidateId|targetPlantId|targetProfileId|position`.
   - Relocate: `RELOCATE|candidateId|targetPlantId|targetProfileId|previousPosition|effectivePosition|row|column`.
   - Replace: `REPLACE|candidateId|targetPlantId|predecessorProfileId|previousPosition|effectivePosition|row|column`.
   - Reject/excluded: `EXCLUDED|candidateId|normalizedReason`.

5. Không đưa resultant profile/plant GUID mới sinh vào token CreateNew/Replace.
6. Bổ sung chính xác các field evidence vào snapshot item:

   - `TargetPlantId` và `TargetPlantProfileId`: nullable cho CreateNew/Reject, bắt buộc cho MatchExisting/Relocate/Replace;
   - `PreviousPosition`: bắt buộc cho Relocate/Replace, null cho CreateNew/MatchExisting/Reject;
   - `EffectiveRowIndex` và `EffectiveColumnIndex`: bắt buộc cho accepted CreateNew/Relocate/Replace, dùng giá trị review thật;
   - `PlantProfileId` hiện hữu tiếp tục là resultant profile ID nhưng không được đưa vào fingerprint của CreateNew/Replace.

7. Fingerprint được tính từ snapshot-item evidence đã đóng băng trước mutation; không suy ngược target/previous position từ Plant/Profile sau khi đã relocate/replace.

**Snapshot invariants:**

1. `Create` bắt buộc mission, capture attempt và batch cho snapshot V3-04; không để nullable trên code path canonical.
2. Mọi item phải thỏa:

   ```text
   item.SnapshotId == snapshot.Id
   item.TenantId == snapshot.TenantId
   item.FarmId == snapshot.FarmId
   item.MappingCandidateBatchId == snapshot.MappingCandidateBatchId
   ```

3. Accepted item bắt buộc profile và không có exclusion reason.
4. Rejected/excluded item không có resultant profile và bắt buộc reason.
5. `RecordInitialMapApproval` xác thực map là baseline và `snapshot.FarmBaseMapVersionId` là baseline đó.
6. `RecordFollowUpMapSnapshot` yêu cầu `BaselineMapVersionId` đã được pin từ trước và bằng pointer; xóa `BaselineMapVersionId ??=` vì nó che lỗi dữ liệu.
7. Follow-up snapshot map phải có `BaselineMapVersionId == pinnedBaselineMapVersionId` và đúng predecessor expected.

**Done khi:** chạy fingerprint hai lần trên cùng input cho cùng kết quả dù GUID output được tạo khác nhau.

---

### Task 10 — Khóa DB invariants và evidence FKs

**Sửa:**

- `backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Persistence/Configurations/FarmBaseMapVersionConfiguration.cs`
- `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderPoleCountSnapshotConfiguration.cs`
- `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderPoleCountSnapshotItemConfiguration.cs`
- `backend/src/Modules/AgriDrone.Modules.Plants/Infrastructure/Persistence/Configurations/PlantProfileObservationConfiguration.cs`
- Cross-module relationship configuration nếu dự án tập trung FK ở đó.

**Farm map constraints:**

```sql
(is_baseline = TRUE
 AND version_number = 1
 AND predecessor_version_id IS NULL
 AND baseline_map_version_id = id)
OR
(is_baseline = FALSE
 AND version_number > 1
 AND predecessor_version_id IS NOT NULL
 AND baseline_map_version_id IS NOT NULL
 AND baseline_map_version_id <> id)
```

Indexes phải dùng `(tenant_id, farm_id)`:

- unique one baseline: filter `is_baseline = TRUE`.
- unique one open draft: filter `status = 'DRAFT'::system.farm_base_map_status`.
- unique version number: `(tenant_id, farm_id, version_number)`.
- unique one published nếu business rule vẫn cần: `(tenant_id, farm_id)` filter Published.

**Evidence relationships, tất cả `DeleteBehavior.Restrict`:**

1. Snapshot → SurveyOrder cùng `(Id, TenantId, FarmId)`.
2. Snapshot → FarmBaseMapVersion cùng scope.
3. Snapshot → FarmBoundaryVersion cùng farm.
4. Snapshot → MappingCandidateBatch cùng `(Id, TenantId, FarmId)`.
5. Snapshot → DroneMission/CaptureAttempt cùng tenant/farm/order; nếu EF không map được navigation cross-module, cấu hình composite FK trong shared DB context.
6. Item → Snapshot bằng `(SnapshotId, TenantId, FarmId)`; bỏ FK chỉ `SnapshotId` và bỏ Cascade.
7. Item → MappingCandidateBatch cùng scope.
8. Item → MappingCandidate; đồng thời candidate phải thuộc cùng batch.
9. Item → PlantProfile cùng tenant/farm khi có profile.
10. PlantProfileObservation → MappingCandidate theo candidate + batch, không chỉ FK profile.

**DB checks cho snapshot item:**

```sql
(is_accepted = TRUE AND plant_profile_id IS NOT NULL AND exclusion_reason IS NULL)
OR
(is_accepted = FALSE AND plant_profile_id IS NULL
 AND exclusion_reason IS NOT NULL AND btrim(exclusion_reason) <> '')
```

Thêm check fingerprint đúng 64 ký tự hex và revision >= 1.

**Done khi:** database không thể chèn cross-tenant/cross-farm evidence hoặc xóa cascade evidence.

---

### Task 11 — Sửa semantics của DroneMission và Missions consumer

**Sửa:**

- `backend/src/Modules/AgriDrone.Modules.Missions/Domain/Missions/DroneMission.cs`
- `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Persistence/Configurations/DroneMissionConfiguration.cs`
- `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Integration/FarmBaseMapInternallyApprovedV1Handler.cs`
- API XML/response text còn gọi internal approval là publish.

**Domain fields:**

- Thêm `InternallyApprovedMapVersionId`.
- Thêm `MapInternallyApprovedAt`.
- Giữ `MappingApprovalId` làm ID quyết định internal approval nếu tên này đã canonical.
- `PublishedMapVersionId/MapPublishedAt` chỉ V3-06 sử dụng.

**Mutation outcome không dùng exception cho conflict dự kiến:**

1. Cùng `SurveyOrderId + ApprovalId + InternallyApprovedMapVersionId` và mission đã ở trạng thái kết quả → `NoChange`.
2. Đã có approval nhưng ID/map khác → `Conflict`.
3. State không cho phép apply → `Conflict`.
4. Hợp lệ → `Applied`.

Có thể dùng result/outcome enum nội bộ; handler map `Conflict` thành `InboxHandlerResult.PermanentFailure`. Không return `false` cho mọi trường hợp vì sẽ nuốt state sai.

**Consumer validation trước mutation:**

```text
sourceMission.Id == payload.SourceMissionId
sourceMission.TenantId/FarmId/SurveyOrderId khớp envelope/payload
sourceMission.CaptureAttemptId == payload.CaptureAttemptId
sourceMission.FarmBoundaryVersionId == payload.FarmBoundaryVersionId
payload MappingKind khớp Purpose
```

Dependent missions của FirstBaseline chỉ được bỏ `RequiresBaselineCompletion` và pin `SourceMapVersionId`; không được ghi rằng chúng đã publish map.

`FarmBaseMapVersion.Publish` chỉ cho phép từ `InternalApproved`; Draft → Published phải bị từ chối.

**Done khi static scan:** internal approval code không gán `PublishedMapVersionId` hoặc `MapPublishedAt`.

---

### Task 12 — Tạo migration sạch và backfill có kiểm chứng

Task này chỉ bắt đầu sau Task 1–11 để migration phản ánh model cuối cùng.

#### 12A. Nhánh A — hai migration chưa apply ở bất kỳ DB nào

1. Remove migration mới nhất rồi migration cũ bằng EF CLI để rollback snapshot; không xóa tay trước.
2. Tạo migration enum E1 riêng, chỉ có:

   ```csharp
   migrationBuilder.Sql(
       "ALTER TYPE system.farm_base_map_status " +
       "ADD VALUE IF NOT EXISTS 'INTERNAL_APPROVED' BEFORE 'PUBLISHED';",
       suppressTransaction: true);
   ```

3. `Down` của enum migration phải no-op có comment vì PostgreSQL không hỗ trợ xóa enum value an toàn; không dựng lại enum khi rollback.
4. Tạo migration S1 bằng EF từ model cuối cùng. Không tự đặt lại timestamp trùng migration đã xóa.

#### 12B. Nhánh B — đã apply ở ít nhất một DB

1. Không remove migration ID/history.
2. Sửa migration cũ để fresh-install từ predecessor chạy được: enum dùng `suppressTransaction`, pointer insert đủ `id`, seed đúng schema.
3. Tạo forward corrective migration idempotent để:

   - bổ sung/đổi tên cột thiếu;
   - backfill dữ liệu trên DB đã apply;
   - thêm constraints/index/FK sau khi dữ liệu sạch.

4. Ghi migration repair rationale trong file plan/checklist. Existing DB bỏ qua file cũ theo history; fresh DB chạy được chuỗi đã sửa; forward migration làm hai trạng thái hội tụ.

#### 12C. Backfill `mapping_candidate_batches` — tuyệt đối không đoán

Quy trình expand → backfill → validate → constrain:

1. Thêm các cột mới nullable, không default giả.
2. Backfill `capture_attempt_id` bằng exact relational join. Schema hiện tại không persist `CaptureAttempt.MissionId`, vì vậy phải đi qua mission đã bind attempt:

   ```text
   mission.id = batch.mission_id
   mission.survey_order_id = batch.survey_order_id
   mission.tenant_id = batch.tenant_id
   mission.farm_id = batch.farm_id
   mission.capture_attempt_id = attempt.id
   attempt.order_id = batch.survey_order_id
   attempt.tenant_id = batch.tenant_id
   attempt.farm_id = batch.farm_id
   ```

   Mỗi batch phải match đúng một attempt. 0 hoặc >1 → `RAISE EXCEPTION` và rollback migration.

3. Backfill `mapping_kind`:

   - order requires baseline + linked map `is_baseline = true` → `FirstBaseline`.
   - order không requires baseline + linked map `is_baseline = false` → `FollowUpSnapshot`.
   - mọi tổ hợp khác → `RAISE EXCEPTION`.

4. Backfill policy bằng lateral join policy có hiệu lực tại `batch.produced_at`, chọn version cao nhất trong stream `DEFAULT`. Không gán cố định default policy cho batch thuộc thời điểm policy khác.
5. Copy policy ID, fingerprint và bốn tolerance từ đúng policy row.
6. Validate:

   - không null;
   - không `Guid.Empty`;
   - tolerance > 0;
   - fingerprint đúng 64 hex;
   - mapping kind chỉ có hai giá trị hợp lệ;
   - capture attempt đúng scope.

7. Chỉ sau validate mới alter `NOT NULL`, thêm FK/check/index.
8. Không để default tạm trên các cột provenance.

#### 12D. Pointer và dữ liệu hiện hữu

1. `farm_baseline_pointers.id = farm_id` theo domain hiện tại.
2. Chỉ tạo pointer từ map thỏa `is_baseline = true` và trạng thái được nghiệp vụ chấp nhận.
3. Nếu một farm có nhiều baseline, không tự lấy `MAX`; migration phải dừng để sửa dữ liệu có chủ đích.
4. Trước khi thêm unique indexes, chạy duplicate guards và `RAISE EXCEPTION` nếu có vi phạm.

#### 12E. Migration scope gate

SQL/generated diff của S1 chỉ được có các object V3-04:

- farm map lineage/pointer/batch/candidate constraints;
- SpatialPolicy;
- Plant/Profile/Observation lineage;
- Survey pole snapshot/item;
- Mission internal-approval fields;
- enum `INTERNAL_APPROVED` ở E1.

Nếu có pricing, appointment, post-flight settlement hoặc phase khác: dừng, sửa model drift rồi generate lại. Không xóa riêng SQL khỏi migration mà để snapshot còn drift.

---

### Task 13 — Static verification, build và cập nhật trạng thái

**Không chạy test.**

1. Static scans phải không còn các pattern sai:

   ```powershell
   rg -n "CaptureAttemptId:\s*draft\.SourceMissionId|captureAttemptId:\s*null" backend/src
   rg -n "request\.(HandoffId|JobId|ModelVersionId)\s*\?\?\s*Guid\.NewGuid" backend/src
   rg -n "GridSpacingToleranceM\s*>\s*0\s*\?.*0\.5" backend/src
   rg -n "BaselineMappingCandidatesReadyV3Handler|BaselineMappingCandidatesReadyV3Processor" backend/src/BuildingBlocks/AgriDrone.Database
   rg -n -C 8 "ApplyInternallyApprovedFarmBaseMap|PublishedMapVersionId\s*=|MapPublishedAt\s*=" backend/src/Modules/AgriDrone.Modules.Missions/Domain/Missions/DroneMission.cs
   ```

2. Inspect migration SQL, không apply DB:

   - enum E1 đứng trước S1 và dùng suppressed transaction;
   - không `capture_attempt_id = mission_id`;
   - không hardcode mọi batch thành FirstBaseline;
   - không provenance default giả;
   - không schema ngoài V3-04.

   Kết quả scan `PublishedMapVersionId/MapPublishedAt` có thể còn assignment của luồng publish thật thuộc V3-06; điều kiện đạt là không có assignment nào trong internal-approval method/consumer, không phải xóa các field publish hợp lệ khỏi toàn domain.

3. Chạy duy nhất:

   ```powershell
   dotnet build backend/AgriDrone.sln --no-restore --nologo
   ```

   Yêu cầu `0 Warning, 0 Error`.

4. Review lại `git diff` theo thứ tự Domain → Persistence → Application → Integration → API → Migration.
5. Chỉ sau khi toàn bộ exit matrix bên dưới đạt mới cập nhật `PROJECT_MEMORY.md` và checklist thành Done. Ghi đúng “tests not run; deferred to V3-10”.

## 5. Danh sách file tối thiểu phải chạm

| Nhóm | File |
|---|---|
| Docs | `PROJECT_MEMORY.md`, `docs/plans/v3-04-execution-checklist.md` |
| Contract/API | `MappingCandidatesReadyV4.cs`, `MissionPlantGpsPublishPayload.cs`, `MissionPlantGpsPublishRequest.cs`, `SystemManagerMissionsController.cs` |
| Missions producer | `AiProcessingJob.cs`, `MappingProcessingSnapshot.cs` (mới nếu cần), `MissionPlantGpsPublisher.cs` |
| Batch/Map domain | `MappingCandidateBatch.cs`, `MappingCandidate.cs`, `FarmBaseMapVersion.cs`, `FarmBaselinePointer.cs` |
| Farms persistence | `MappingCandidateBatchConfiguration.cs`, `FarmBaseMapVersionConfiguration.cs`, `PlantConfiguration.cs` |
| Ingestion | `MappingCandidatesReadyV4Handler.cs`, `DependencyInjection.cs`, V3 handler/processor |
| Approval | `FarmBaseMapInternalApprovalOrchestrator.cs`, `BaselineMappingBatchValidator.cs` |
| Snapshot domain | `SurveyOrderPoleCountSnapshot.cs`, `SurveyOrderPoleCountSnapshotItem.cs`, `SurveyOrder.cs` |
| Snapshot persistence | snapshot/item configurations, cross-module relationship configuration |
| Plant evidence | `PlantProfileObservationConfiguration.cs` |
| Missions consumer | `DroneMission.cs`, `DroneMissionConfiguration.cs`, `FarmBaseMapInternallyApprovedV1Handler.cs` |
| Migration | E1/S1 hoặc forward corrective migration theo Gate M0; model snapshot |

Đường dẫn thực tế phải được xác nhận bằng `rg --files`; không dùng đường dẫn viết tắt `backend/...` khi thực thi.

## 6. Exit matrix — không được tự đánh dấu nếu chưa có bằng chứng

| Gate | Điều kiện đạt | Bằng chứng cần ghi |
|---|---|---|
| G0 | Biết chính xác migration đã apply ở đâu | Kết quả `__EFMigrationsHistory` |
| G1 | Chỉ V4 có runtime consumer/writer | DI + static scan + backlog count |
| G2 | Không metadata giả | Static scan publisher + provenance matrix |
| G3 | CaptureAttempt truyền end-to-end | Contract → batch → snapshot → event → consumer |
| G4 | PlantId được resolve qua Plant trước Profile | Orchestrator diff |
| G5 | Follow-up chống stale predecessor | Handler guards + exact error codes |
| G6 | Bốn SpatialPolicy field đều được dùng đúng nghĩa | Handler/validator diff |
| G7 | Boundary distance là mét thật | PostGIS geography query; không NTS degree distance |
| G8 | Một baseline và một draft mỗi tenant/farm | Filtered unique indexes |
| G9 | Evidence FK đầy đủ và Restrict | EF config + migration constraints |
| G10 | Plant/Profile 1–1 | Unique filtered index + duplicate guard |
| G11 | Snapshot/fingerprint deterministic | Canonical tokens không chứa output GUID |
| G12 | Internal approval không ghi Published fields | Domain + consumer static scan |
| G13 | Migration backfill không đoán | Exact joins + `RAISE EXCEPTION` guards |
| G14 | Migration không lẫn phase khác | Generated migration diff |
| G15 | Boundary exception đúng scope | Tenant/farm/order/mission/boundary query |
| G16 | Snapshot/order domain invariants đầy đủ | Domain guards + DB checks |
| G17 | Build sạch | `0 Warning, 0 Error` |
| G18 | Không chạy test | Checklist ghi `TESTS NOT RUN — deferred V3-10` |

## 7. Definition of Done

### Kết quả thực thi ngày 2026-11-18

- Chủ dự án xác nhận local là database duy nhất; preflight local cho thấy không migration V3-04 nào đã apply, nên chọn Nhánh A.
- Đã loại `20261116145210` và `20261117050302`; chain canonical mới là migration gốc `20261116131222` → E1 `20261118040000` → S1 `20261118042702`.
- E1 dùng `suppressTransaction: true`, `Down` no-op. Generated SQL xác nhận enum chạy ngoài transaction và trước S1.
- S1 dùng expand → exact backfill → validate → constrain; không có default provenance giả, có duplicate guards trước filtered unique indexes và pointer chỉ sinh từ baseline đã được chấp nhận.
- G8 được sửa tận gốc bằng ba EF model-index name khác nhau; snapshot và generated SQL đều có đủ one-baseline, one-draft và one-published.
- Static scope scan/generated SQL không có pricing, appointment hoặc post-flight settlement.
- `SurveyPayments:Enabled = false` ở Development; build đạt 0 Warning, 0 Error.
- `dotnet test` không chạy; toàn bộ test integration/acceptance tiếp tục để V3-10.

V3-04 chỉ được ghi `Done (Remediated)` khi đồng thời:

- G0–G18 đều có bằng chứng cụ thể.
- Không còn migration hỏng trên fresh migration chain.
- Existing database và fresh database hội tụ về cùng schema theo nhánh migration đã chọn.
- Không còn pipeline V3 runtime.
- Không còn GUID/time/default giả cho provenance.
- Không còn nhầm PlantId/ProfileId.
- Không còn stale follow-up hoặc cross-tenant/cross-farm evidence.
- Snapshot/fingerprint tái lập được và không phụ thuộc ID output sinh lúc approval.
- Internal approval không dùng bất kỳ trường/endpoint/message text mang nghĩa Published.
- Build đạt 0 warning, 0 error.
- Không chạy `dotnet test`; toàn bộ integration/acceptance test để V3-10.

Nếu một gate chưa đạt, trạng thái vẫn là `Remediation In Progress`; không chuyển sang V3-05 dựa trên `VerifiedBillablePoleCount` chưa được chứng minh canonical.

</details>
