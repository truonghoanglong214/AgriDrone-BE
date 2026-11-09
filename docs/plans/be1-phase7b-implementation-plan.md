# BE1 Phase 7B — Plant Health Human Review Implementation Plan

> Ngày audit source: 2026-11-09
> Phạm vi: UC09 / Step 7 / Business Phase 12, chỉ human review 7B
> Trạng thái: `CodeComplete — AwaitingFinalTest` (2026-11-09), chưa apply database
> Mốc sau triển khai: `CodeComplete — AwaitingFinalTest`; `Done` sau Final Test Phase

## 1. Mục tiêu, ownership và dependency

Implementation evidence: [completion report](../operations/be1-phase7b-completion-report.md),
[ADR 0014](../adr/0014-plant-health-human-review.md) và
[API contract](../contracts/plant-health-review.md).
Theo yêu cầu người dùng, không viết/chạy test trong 7B; giữ toàn bộ ở phase cuối.
Audit mục 2 là baseline trước triển khai, không phải trạng thái code hiện tại.

Assigned SystemManager đọc pending output từ 7A, quyết định từng finding hoặc batch,
giải quyết unmatched/boundary exceptions, review zone geometry/memberships và chọn
hoặc từ chối sourced guidance. Mỗi thay đổi giữ AI original, correction, reviewer,
reason/evidence và concurrency token; đầu ra đủ rõ để 7C kiểm tra approval gate.

BE1 sở hữu toàn bộ 7B. BE2 giữ source observations/job snapshots/transport và typed
source/media context; không đưa review V4 trở lại các legacy observation endpoints.
Result và Order vẫn `PendingReview`. Approval result thuộc 7C; publication,
latest-reviewed state, Order/Mission completion và owner notifications thuộc 7D–7E.

Dependency sử dụng:

- [Kế hoạch 7A](be1-be2-phase7a-implementation-plan.md),
  [completion report](../operations/be1-be2-phase7a-completion-report.md) và
  [ADR 0013](../adr/0013-plant-health-v4-pending-ingestion.md).
- 7A hiện `CodeComplete — AwaitingFinalTest`, chưa apply database hoặc chạy runtime
  acceptance. Có thể code 7B từ foundation này; không coi dependency E2E đã pass.
- Dùng đúng một result/Order, ActiveImportBatchId và batch-finding links của 7A;
  không tạo bộ finding hoặc consumer khác. Giữ working-tree 7A hiện tại.
- Muốn chạy review runtime phải apply migrations theo thứ tự 7A → 7B và có matching
  Order/Mission/capture map/catalogue. Đây là deployment gate, không tự apply DB khi code.

## 2. Source audit và khoảng trống

| Thành phần | Hiện trạng | Delta của 7B |
|---|---|---|
| `PlantHealthFinding` | Immutable original, class/Plant/candidate/source version, xmin; chỉ PendingReview/NeedsReview | Thêm append-only review và mutable current-review head, không sửa original |
| `PlantHealthBatchFinding` | Link batch tới canonical finding revision | Review key phải có BatchId; không reuse decision giữa batches dù cùng canonical finding |
| `PlantHealthImportBatch` | OriginalPayload/provenance, SourceVersion, BlockingCount tại import | BlockingCount giữ nguyên snapshot; live review summary tính riêng |
| `SurveyResult` | ActiveImportBatchId, source/map/nature, xmin; chưa có review orchestration | Thêm review revision/touch method, kiểm tra expected version và active batch |
| `PlantHealthBoundaryException` | Original geometry/JSON, NeedsReview, xmin | Thêm append-only resolution và current decision, không gọi nhầm Farm boundary exception aggregate |
| `DiseaseZone` V4 | Original proposal, nullable health level, guidance suggestions; legacy Review bị chặn | Dedicated V4 review lifecycle và history; không gọi severity/treatment review |
| Zone lifecycle constraint | Reviewed/Published vẫn phụ thuộc legacy treatment; rejected yêu cầu membership version 1 | Migration mới tách constraint legacy/V4; hỗ trợ source membership version bất kỳ dương |
| `SourcedGuidance` | Có cited catalogue/version/effective window và lifecycle | Query valid choices theo class/revision/time; lưu selection/rejection snapshot |
| `ISystemManagerAccessService` | Kiểm tra active user/profile, CanOperate và primary assignment cùng Tenant/Farm | Tái sử dụng; thêm commit-time guard cho mutation/revocation race |
| Controller/queries/commands | Chưa thấy target result controller, finding review history hoặc target review features | Viết typed application ports, manager queries/mutations, API/DI/error mapping |
| Mapping review examples | Có mẫu single/batch nhưng batch có thể skip ID lạ, thiếu target expected-version contract | Chỉ tham khảo wiring; 7B không sao chép skip-item hoặc thiếu atomicity |

Audit trong turn này chỉ đọc source; không chạy lại build/test của 7A và không coi
schema/entity có sẵn là 7B đã triển khai.

## 3. Khóa quyết định ở 7B.0

### 3.1. Decisions và finality

| Decision finding | Ý nghĩa | Điều kiện để hết blocking |
|---|---|---|
| `Approve` | Chấp nhận original suspected finding | Configured disease class hợp lệ; resolved existing Plant/capture context và boundary đạt policy |
| `Correct` | Dùng reviewed class/position/Plant thay cho original | Corrected values explicit, class catalogue/version hợp lệ, existing Plant và geometry/context được validate |
| `Reject` | Loại AI finding khỏi candidate | Reason/evidence và actor hợp lệ; không được giữ trong approved zone membership |
| `NonDisease` | Xác nhận confusing/non-disease theo annotation rules | Không gán suspected disease class, không suy ra cây khỏe hoặc expert diagnosis |
| `Ambiguous` | Chưa đủ căn cứ | Luôn blocking; cần quyết định cuối sau này, không dùng để lách 7C |

- Pending chưa review luôn blocking, kể cả original ResolutionState là PendingReview.
- Unknown/SUNBURN/scarring/lighting/occlusion được xử lý theo configured label kind
  và annotation rules; không tự chuyển sang disease class hoặc auto Approve.
- Finding giữ original nullable Plant/candidate. `Correct` có thể liên kết existing
  Plant đúng Farm/capture evidence; không tạo/relocate/replace Plant hoặc sửa FarmBoundary.
  Nếu thiếu historical association evidence, giữ blocking thay vì chọn current map tùy ý.
- Reject/NonDisease có thể kết thúc finding không cần gán Plant. Boundary exception
  liên quan vẫn cần resolution explicit; zone chứa finding đó phải sửa hoặc reject.
- Correction không sửa AI confidence/observed time/source fingerprint. Không gọi
  một reviewed confidence mới là model confidence; nếu cần certainty của reviewer,
  lưu field riêng có semantics đã khóa, không thêm tùy tiện vào MVP.

### 3.2. Review history và current heads

Tên model dưới đây là mô hình logic đề xuất ban đầu. ADR 0014 chốt dùng chung
`PlantHealthReviewEntry`/`PlantHealthReviewState` với typed Kind/subject/snapshot cho
finding, boundary, zone và guidance; `PlantHealthReviewOperation` giữ receipt riêng.
Findings/history vẫn thuộc Plants; Surveys điều phối result context, không sở hữu
một bản sao finding. Subject/context FKs, immutable triggers và zone-batch insert
guard giữ các invariant tương ứng.

| Model/delta | Dữ liệu và invariant |
|---|---|
| `PlantHealthFindingReview` | Append-only decision, BatchId/FindingId/source revision, previous review ID, effective reviewed class/Plant/point, correction flags, reviewer/time/reason/evidence, catalogue revision và annotation basis |
| `PlantHealthFindingReviewState` | Unique `(BatchId, FindingId)`, CurrentReviewId, xmin; phân biệt chưa review và đã review; current head là projection, history là evidence |
| `PlantHealthBoundaryExceptionReview` + state | Batch/exception ID, AcceptCorrectedPosition/ExcludeEvidence/NeedsEvidence, linked finding/review IDs, original vs corrected association và decision history |
| `DiseaseZoneReview` + state | Append-only Approve/Correct/Reject/Ambiguous, original proposal reference, reviewed geometry, effective suspected class và member finding-review snapshot; current head có xmin |
| `DiseaseZoneGuidanceReview` | Select/RejectAll/NeedsEvidence, bound ZoneReviewId, exact guidance ID/code/version/source/effective-window snapshot, reviewer/reason/evidence; không free-form advice |
| `PlantHealthReviewOperation` | Unique `(TenantId, ActorId, OperationId)`, normalized request fingerprint, Order/result/batch, serialized outcome; cùng transaction với review/audit |
| `SurveyResult` | ReviewRevision/review updated time để invalidation/7C snapshot; mỗi mutation thành công touch result xmin |
| `PlantHealthReviewSummary` | Typed query DTO theo active batch + result/review revision; không tạo aggregate hoặc cache authoritative độc lập |

- Composite keys/FKs giữ đúng Tenant/Farm/result/batch/finding, current pointer phải
  trỏ đúng history của item. Chống orphan/cross-batch history bằng DB constraints.
- Current head create lần đầu dùng unique key; update dùng xmin. Source Finding.Version
  là token của original immutable, không đủ phát hiện concurrent review.
- Lịch sử chỉ INSERT; muốn sửa quyết định phải append revision mới trỏ decision cũ.
  Không DELETE/UPDATE history để “undo”. Kiểm tra actor hiện tại cho mọi revision.
- Original geometry/membership và source fingerprint giữ nguyên. Reviewed membership
  lưu snapshot mới, tham chiếu canonical FindingId + source version + ReviewId;
  mỗi member phải có resolved Approve/Correct cùng effective disease class.
- Zone class correction dùng reviewed fields/head riêng (ví dụ ReviewedSuspectedClassCode
  và ReviewedPlantConditionId); không overwrite source SuspectedClassCode/PlantConditionId.
  Guidance và summary dùng effective reviewed class, vẫn truy được class gốc.

### 3.3. Active batch, concurrency, reprocess và retry

Mọi mutation yêu cầu OrderId, BatchId, ExpectedResultVersion và item/source/review
versions thích hợp. SourceVersion, xmin và ReviewRevision là ba khái niệm riêng,
không hoán đổi chúng trong API.

1. Resolve Order/Farm ở server và authorize current primary Manager; không tin TenantId
   hoặc reviewer/time từ request. Unauthorized resource không lộ data/count/history.
2. Mở một transaction, lock Order → Result theo thứ tự dùng chung 7A/7C/7D. Giữ guard
   authorization của active user/profile/assignment tới commit bằng typed application
   port/adapter dùng cùng transaction; rà lock order của revoke/deactivate handlers.
   Chỉ check hai lần trên Identity DbContext khác không đủ chứng minh race đã được xử lý.
3. Kiểm tra operation receipt sau authorization. Retry cùng OperationId/fingerprint
   trả outcome cũ; khác payload/context trả conflict. Nếu batch hiện đã đổi, replay
   chỉ trả receipt lịch sử với IsCurrentBatch=false, không ghi review vào batch cũ.
4. Với command mới: Order/result đều PendingReview, BatchId bằng ActiveImportBatchId,
   expected result/source/head versions khớp; lock item heads theo ID ổn định.
5. Validate toàn bộ request và dependencies trước ghi. Append reviews, cập nhật heads,
   touch result review revision, audit và operation receipt trong cùng transaction.
6. Save/commit; exception/conflict rollback cả batch/history/heads/audit/receipt.
   Response trả versions đã persist, không tự đoán xmin bằng phép cộng phía client.

7A reprocess phải dùng cùng result lock/version discipline. Batch mới không kế thừa
review cũ; batch cũ chỉ read-only history. Nếu review và reprocess chạy đồng thời,
một phía thắng serialization; command cũ nhận BatchChanged/VersionConflict. Ghi rõ
lock/touch delta nhỏ cần sửa trong 7A nếu implementation audit thấy chưa đủ.

Batch review MVP: tối đa 100 items trong một Order/active batch, atomic all-or-nothing;
reject request chứa duplicate/unknown/foreign IDs hoặc item stale. Không skip ID lạ,
không trả success khi chỉ một phần được review. Chỉ lưu receipt khi commit thành công.

### 3.4. Boundary exception resolution

- Queue đọc đúng `PlantHealthBoundaryException` của active batch, không dùng chung
  bảng Farm boundary exceptions chỉ vì có tên tương tự.
- AcceptCorrectedPosition phải giữ original, chỉ ra effective point/existing Plant
  và reviewed finding; PostGIS xác minh capture FarmBoundary/SRID/topology và policy.
  Near-boundary vẫn cần lý do/evidence theo policy; ngoài boundary không được miễn trừ
  bằng một boolean approve. Boundary/version thay đổi cần workflow khác.
- ExcludeEvidence chỉ hợp lệ nếu referenced finding đã Reject/NonDisease hoặc loại
  source evidence có căn cứ explicit. Exception không map chắc tới finding vẫn
  blocking cho tới khi resolved evidence association được ghi nhận.
- NeedsEvidence luôn blocking. Không tự đóng exception vì Manager review finding
  khác; cập nhật nhiều item liên quan phải có transaction/decision explicit.

### 3.5. Zone và sourced-guidance lifecycle

- Query/review chỉ nhận SourceSchemaVersion=4 và ImportBatchId của active result.
  Không gọi legacy `DiseaseZone.Review(...TreatmentRecommendation...)` hoặc dùng
  `RecommendationsRejected` làm guidance decision mới.
- Geometry phải valid Polygon WGS84, nằm trong capture approved boundary; members
  thuộc cùng result/base map, có accepted finding-review và nằm trong reviewed zone.
  Không auto clip geometry hoặc auto prune member đã Reject/NonDisease.
- Zone Approve/Correct lưu reviewed candidate + members vào review history/head.
  Trước khi có valid guidance decision, zone còn pending review; không chuyển
  sang Reviewed bằng fabricated recommendation hoặc guideline.
- Select sourced guidance qua ID; server resolve class/condition revision/code/
  version/source và effective window tại review time. Snapshot citation/version;
  không nhận text lời khuyên từ AI/client thay catalogue.
- RejectAll là quyết định final có lý do, không tương đương “chưa chọn”. NeedsEvidence
  hoặc thiếu guidance decision vẫn blocking. Guidance không còn active/effective
  phải được đánh dấu lại khi query/gate, không âm thầm dùng suggestion đã hết hạn.
- Khi geometry/membership đã quyết định và guidance choice final hợp lệ, dedicated
  V4 method project accepted head vào ReviewedGeometry/new membership snapshot và
  Status=Reviewed. Reject zone project Status=Rejected; không cần chọn guidance.
- Split lifecycle constraints theo SourceSchemaVersion trong migration 7B. V4
  Reviewed dùng SourcedGuidance decision; legacy vẫn giữ severity/treatment rules.
  Membership reviewed version phải lớn hơn max source/current version, không hardcode 2.
- Finding review bị sửa sau zone approval làm member-review binding stale: giữ
  history nhưng live zone validity chuyển blocking cho tới khi re-review. Sửa class/
  geometry/membership của zone làm guidance decision cũ stale nếu binding đổi.
  Các thao tác này serialize qua result và trả invalidated dependents trong response.

## 4. Queries, API và error contract

Routes dưới đây là mục tiêu 7B, giữ prefix/master routes đã có trong kế hoạch:

```text
GET  /api/system-manager/survey-orders/{orderId}/health-findings
GET  /api/system-manager/survey-orders/{orderId}/health-findings/{findingId}/reviews
POST /api/system-manager/survey-orders/{orderId}/health-findings/{findingId}/review
POST /api/system-manager/survey-orders/{orderId}/health-findings/review-batch
GET  /api/system-manager/survey-orders/{orderId}/health-boundary-exceptions
POST /api/system-manager/survey-orders/{orderId}/health-boundary-exceptions/{exceptionId}/review
GET  /api/system-manager/survey-orders/{orderId}/disease-zones
POST /api/system-manager/survey-orders/{orderId}/disease-zones/{zoneId}/review
GET  /api/system-manager/survey-orders/{orderId}/disease-zones/{zoneId}/guidance-options
POST /api/system-manager/survey-orders/{orderId}/disease-zones/{zoneId}/guidance-review
GET  /api/system-manager/survey-orders/{orderId}/health-results/review-summary
```

- Default queue chỉ active batch; history của older batch phải explicit batch filter,
  read-only và cùng authorization. Pagination ổn định theo ObservedAt + ID; giới hạn
  page size, filter Decision/ResolutionState/class/zone; tránh N+1 evidence/memberships.
- DTO có original, effective reviewed value, decision/history IDs, blocking reasons,
  result/batch/source/review versions, suspected/preliminary/team-label limitation.
  Resolve source/media qua typed query và reauthorize; không trả storage credentials.
- Input không nhận TenantId/reviewer/ReviewedAt/status derived/severity/health-level/
  diagnosis/free-form advice. Khóa unknown-field policy cho target request DTOs.
- OperationId là non-empty GUID; finding batch dùng một OperationId cho toàn command.
  Correct yêu cầu corrected fields explicit; Approve/Reject không nhận corrections
  bị lờ đi. Domain guard và API validator thống nhất error codes.
- Chốt 401/404 hoặc 403 theo actor/resource policy hiện hữu; dùng 409 cho stale version,
  inactive batch, locked result, reused operation khác payload; decision/
  geometry/catalogue invalid dùng 422 theo error mapper hiện hữu (400 cho binding/
  malformed input). Không leak resource detail qua error khi unauthorized.

Error prefix đề xuất `PlantHealthReview`: `NotFound`, `AccessDenied`, `BatchChanged`,
`VersionConflict`, `ResultLocked`, `InvalidDecision`, `InvalidAssociation`,
`InvalidGeometry`, `CatalogueMismatch`, `GuidanceNotApplicable`, `DependencyChanged`,
`OperationConflict`. Error có current versions/reload hint chỉ sau authorization.

## 5. Live summary và đầu ra cho 7C

`PlantHealthReviewSummary` lấy consistent snapshot của active batch/result revision,
đếm unreviewed/ambiguous/unmatched findings, unresolved boundary exceptions,
undecided/rejected/approved zones, missing/invalid guidance và stale review bindings.

- `PlantHealthImportBatch.BlockingCount`, `DiseaseZone.HasBlockingItems` và original
  finding ResolutionState là ingestion diagnostics, không phải authoritative live gate.
- ReadyForApproval là derived DTO khi mọi required decision final và dependencies
  hợp lệ; client không gửi cờ này và 7B không gọi `SurveyResult.Approve`.
- Result/Order vẫn PendingReview khi tất cả findings bị Reject/NonDisease; summary
  ghi zero accepted disease findings, không tự diễn giải thành HEALTHY. 7C khóa policy
  empty/no-disease result trước khi approve; 7B cung cấp đầy đủ decision evidence.
- 7C phải reload/recheck assignment, active batch, heads/member/guidance bindings,
  catalogue/effective windows và versions trong transaction; không tin summary cũ.

## 6. Thứ tự PR và exit gates

7B.0–7B.6 đã triển khai trong working tree; đây là đơn vị công việc, chưa tạo GitHub
PR hoặc commit. 7B.7 vẫn deferred. Build chỉ target production API/dependencies để
không build test projects trong phase này theo yêu cầu người dùng.

| PR | Phạm vi | Đầu ra/exit gate |
|---|---|---|
| 7B.0 — ADR/contracts | Decisions, finality, request/response examples, errors, lock order, operation replay và 7C summary | Khóa những đề xuất mục 3–5; map với PR 7.2–7.3/7.6 của master plan |
| 7B.1 — Domain/schema | Review histories/heads/receipts, result review revision, V4 zone lifecycle và migration `Step7BPlantHealthHumanReview` | Original bất biến; DB keys/FKs/concurrency đúng; không sửa migration 7A đã bàn giao |
| 7B.2 — Queues + single finding | Authorized queries/history, single review, context validation và transactional audit/idempotency | Manager chỉ đọc/ghi đúng assigned active batch; response trả versions thật |
| 7B.3 — Batch + exceptions | Atomic 100-item review, unresolved association và boundary decisions | Unknown/stale item rollback toàn batch; exception không được bypass |
| 7B.4 — Zones + guidance | V4 review, corrected geometry/member snapshots, selection/rejection và dependency invalidation | Không legacy severity; stale member/guidance binding làm summary blocking |
| 7B.5 — API + summary | Controller/contracts, DI, error mapping, OpenAPI; consistent summary/7C handoff | Tất cả routes 7B dùng application ports; result/Order chưa approve/publish |
| 7B.6 — CodeComplete | Source/security/transaction review, production API build, EF pending-model check, migration/runbook/evidence/backlog | `CodeComplete — AwaitingFinalTest`; không coi build là runtime evidence |
| 7B.7 — Final Test | Viết/chạy backlog mục 8 theo Final Test Phase | Đủ acceptance/security/concurrency/migration evidence mới ghi 7B Done |

Thứ tự: 7B.0 → 7B.1 → 7B.2 → 7B.3 → 7B.4 → 7B.5 → 7B.6.
7B.7 thuộc phase kiểm thử cuối. Phân công là BE1; BE2 chỉ cung cấp nguồn/context cần
thiết và xác nhận compatibility 7A. Không đổi ownership 7A hoặc implement 7C–7E trong 7B.

## 7. File manifest dự kiến

Đường dẫn tương đối với `backend/src`; tên mới chốt tại 7B.0.

| Action | File/nhóm file |
|---|---|
| Add | `Modules/AgriDrone.Modules.Plants/Domain/Health/PlantHealthFindingReview.cs`, review state, boundary review/resolution, zone/guidance review histories và domain policies |
| Add | Plants persistence configurations cho reviews/heads, composite FKs/checks/indexes; typed repository/query ports cần thiết |
| Add | `Modules/AgriDrone.Modules.Surveys/Application/Features/PlantHealthReview/` cho queues/single/batch/exceptions/zones/guidance/summary và validators |
| Add | Surveys application abstractions cho review UoW/query/command orchestration, operation receipts và authorization guard |
| Add | `BuildingBlocks/AgriDrone.Database/Health/PlantHealthReviewService.cs`, query adapter, authorization/receipt adapters; không đưa DbContext vào API |
| Modify | `SurveyResult` + configuration, `DiseaseZone`/memberships + configurations cho dedicated V4 review projection/lifecycle; original sources giữ nguyên |
| Modify | `SurveyResultPublicationDbContext`, `AgriDroneSchemaDbContext` relationships, Plants/Surveys runtime configurations và DI registrations |
| Verify/modify | `PlantHealthPendingImport` lock/version coordination với reprocess; chỉ delta cần để serialize review, không đổi V4 wire semantics |
| Add | Migration mới `Step7BPlantHealthHumanReview` + model snapshot; preflight/backfill/roll-forward notes và Down guard khi có review history |
| Add | `AgriDrone.Api/Controllers/SystemManagerSurveyResultsController.cs`, `Contracts/Surveys/PlantHealthReviewContracts.cs`, error mapping/OpenAPI |
| Add | `docs/adr/0014-plant-health-human-review.md`, golden review requests/responses, `docs/operations/be1-phase7b-completion-report.md` sau implementation |
| Modify | Master plan mục 15/27 và report 7A nếu cần ghi rõ integration delta; không ghi lại 7A runtime acceptance chưa chạy |

## 8. Final Test backlog và Definition of Done

Trong phase code chỉ build/EF model checks và ghi backlog; không viết/chạy backlog
test 7B trước Final Test Phase theo master plan.

- [ ] Assigned active/available Manager được query/review; wrong role/Tenant/Farm,
  revoked assignment/inactive user/profile không đọc được data hoặc mutate review.
- [ ] Revocation/deactivation chạy đồng thời với review có serialization/evidence
  rõ ràng; command sau revocation không commit nhờ stale cached authorization.
- [ ] Approve/Correct/Reject/NonDisease/Ambiguous đúng finality; unknown/confusing label
  không biến thành accepted disease hoặc HEALTHY bằng fallback.
- [ ] Single review giữ original/fingerprint/provenance; correction/re-review append
  history, current head/reviewer/reason/evidence và versions nhất quán.
- [ ] Hai reviewer/device sửa cùng head: một commit, một 409; không last-write-wins.
- [ ] Batch atomic với unknown/duplicate/foreign/stale item và failure tại item N:
  rollback toàn history/heads/result revision/audit/receipt, không partial success.
- [ ] Operation replay cùng content trả outcome cũ; khác content/context conflict;
  revoked actor không replay lách auth; old-batch replay chỉ trả historical receipt.
- [ ] Reprocess chạy trước/giữa/sau review: active batch discipline đúng, stale review
  không attach sang source mới; canonical finding dùng chung không carry decisions.
- [ ] Unmatched/candidate-only chỉ resolve existing Plant có đúng capture evidence;
  không tạo/relocate/replace Plant hoặc sửa FarmBoundary/official health.
- [ ] Boundary exception raw/corrected/outside/near/unmapped-source cases đúng policy;
  NeedsEvidence vẫn blocking; không tự close exception bằng decision không liên quan.
- [ ] Zone geometry/member snapshot hợp lệ PostGIS; member sai class/context/boundary,
  rejected/non-disease/unreviewed finding bị chặn; source membership version >1 xử lý đúng.
- [ ] V4 Reviewed/Rejected/needs-guidance lifecycle persist đúng; không yêu cầu/gán
  severity/treatment recommendation; legacy constraints không bị nới ngoài phạm vi.
- [ ] Guidance Select/RejectAll/NeedsEvidence đúng; fake ID, wrong class/revision/source,
  retired/expired entry, free-form advice và expert claim không qua gate.
- [ ] Re-review finding/zone làm stale membership/guidance bindings bị phát hiện;
  summary không giữ ReadyForApproval cũ, audit/history không bị ghi đè.
- [ ] Summary consistent theo active batch/review revision, ingestion BlockingCount
  không dùng làm gate; all-rejected result không tự approve hoặc gọi là cây khỏe.
- [ ] No pending/rejected data lộ owner; 7B không approve/publish/update latest-reviewed,
  không complete Order/Mission hoặc phát publication/notification event.
- [ ] Fresh/upgrade migration, populated-history Down guard, composite FKs, xmin,
  source-data checksums và EF pending-model check đạt trên PostgreSQL/PostGIS thật.
- [ ] API golden contracts/pagination/page/batch limits/strict invalid fields/error
  mappings và architecture boundaries đạt; queue không N+1, deep links reauthorize.

### CodeComplete checklist

- [x] 7B.0–7B.5 implementation/ADR/contracts/OpenAPI/migration hoàn tất; layout thực tế theo ADR 0014/report.
- [x] Production API/dependency build 0 errors; EF pending-model check sạch; command/output ghi trong report.
- [x] Review auth/reprocess/atomicity/legacy bypass, migration recovery và 7C handoff có evidence source review.
- [x] Completion report/backlog cập nhật; không viết/chạy test hoặc đánh dấu runtime acceptance đã pass.

### Done checklist

- [ ] Final Test 7B.7 đạt đủ backlog trên database thật với failure/concurrency/security evidence.
- [ ] Manager đi từ active pending batch tới final reviewed candidate qua API, giữ
  original/history/provenance và summary đủ để 7C quyết định mà không sửa DB thủ công.
- [ ] Result/Order vẫn PendingReview và owner visibility chưa đổi; 7C/7D có gate riêng.
