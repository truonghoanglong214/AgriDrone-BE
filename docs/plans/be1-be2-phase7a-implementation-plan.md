# BE1–BE2 Phase 7A — Plant Health V4 Pending Ingestion Implementation Plan

> Ngày audit source: 2026-11-09
> Phạm vi: Step 7 / UC09 / Business Phase 12, chỉ phần 7A
> Trạng thái triển khai: `CodeComplete — AwaitingFinalTest` (2026-11-09), chưa apply database hoặc chạy acceptance tests
> Mốc sau triển khai: `CodeComplete — AwaitingFinalTest`; `Done` sau Final Test Phase

## 1. Mục tiêu và ranh giới

Hoàn thành luồng `PlantHealthAnalysisReadyV4` → RabbitMQ → Inbox → pending import
để 7B có dữ liệu review. Một handoff hợp lệ phải lưu được result, suspected findings,
proposed zones/memberships, guidance suggestions, boundary exceptions và provenance.
Inbox, dữ liệu pending, audit và transition `SurveyOrder.InProgress → PendingReview`
phải nằm trong cùng transaction.

7A không approve/publish, không cập nhật `LatestReviewedPlantHealth` hoặc
`Plant.HealthLevelId`, không complete Order/Mission và không tạo owner portal API.
Pending data phải giữ được AI original và các trường hợp chưa resolve.

Giữ phân công trong master plan:

| Bên sở hữu | Trách nhiệm |
|---|---|
| BE1 | Target domain/schema, typed pending-import port và implementation nghiệp vụ, transition Order, invariant phục vụ 7B–7E |
| BE2 | Producer, V4 transport processor/handler, Inbox coordination, retry/DLQ, gọi pending-import port, DI và RabbitMQ wiring |
| BE1 + BE2 | Khóa payload/identity/version, transaction boundary, golden examples, migration/handoff evidence |

BE2 chịu trách nhiệm hoàn tất capability ingestion 7A; phần ghi target domain đi
qua port BE1. Vị trí file trong `AgriDrone.Database` không thay đổi owner nghiệp vụ.
Evidence triển khai và các giới hạn runtime được ghi riêng tại
[`docs/operations/be1-be2-phase7a-completion-report.md`](../operations/be1-be2-phase7a-completion-report.md).
ADR khóa thiết kế tại [`0013`](../adr/0013-plant-health-v4-pending-ingestion.md).
Baseline mục 2 là snapshot trước triển khai; không dùng nó làm trạng thái code hiện tại.

## 2. Baseline đã kiểm tra trong source

| Thành phần | Hiện trạng và cách xử lý |
|---|---|
| `IntegrationContracts/Health/PlantHealthAnalysisReadyV4.cs` | Đã có DTO no-severity, finding version, nullable Plant/candidate references, zones/guidance/provenance; tái sử dụng |
| Event descriptor/type/consumer name | Đã có V4; giữ routing `health.analysis-ready.v4` |
| `AgriDrone.Api/appsettings.json` | Đã có consumer name `be1-plant-health-analysis-ready-v4`, queue `agridrone.be1.plant-health-analysis-ready.v4`; cấu hình chưa thay thế cho consumer implementation |
| `Missions/Infrastructure/Persistence/MissionPlantHealthPublisher.cs` | Đã tạo Outbox event; còn hardcode provenance/guidance, version 1, JobId mới, boundary exceptions rỗng, fallback class và suggested Plant |
| Consumer/validator/DI V4 | Chưa thấy processor, handler hoặc validator V4 chuyên biệt và đăng ký consumer tương ứng |
| `SurveyResultPublicationDbContext` | Có Order/Result/Plants/legacy zones/Inbox/Outbox/Audit; chưa có target pending model đầy đủ |
| `SurveyResultConfiguration` | Unique một result/Order và quan hệ one-to-one; không thể tạo thêm result cho mỗi lần reprocess mà không đổi model |
| `SurveyOrder.MarkAnalysisReady(...)` | Đã có transition `InProgress → PendingReview`; tái sử dụng trong pending import |
| Legacy `ConditionDetection`/`DiseaseZone` | Còn bắt buộc severity/health-level và recommendation legacy; không dùng nguyên trạng làm target V4 |
| `InboxExecutionCoordinator` | Sở hữu transaction; dedup theo `(ConsumerName, MessageId)`; chưa thay thế cho business dedup theo handoff/source version |
| Shared JSON serializer | Đang skip unknown members; DTO không có severity chưa đủ để bảo đảm payload có severity bị từ chối |
| Tài liệu bàn giao consumer trong `Codex-Plan` | Có payload mẫu/code mẫu nhưng giao consumer cho BE1 và hướng dẫn gán MODERATE; cần đồng bộ lại |

Audit này chỉ đọc source; chưa xác minh queue/database đang chạy và chưa chạy build/test.

## 3. Quyết định thiết kế để triển khai

### 3.1. Contract, source identity và provenance

- Giữ contract/routing V1–V3 cho compatibility/drain; không map severity legacy vào V4.
- Khóa wire value V4 hiện tại `ResultNature = PRELIMINARY_SUSPECTED`,
  `ClassificationNature = SUSPECTED`; domain biểu diễn Preliminary/Suspected tương ứng.
  Không dùng giá trị wire `Preliminary` khác producer mà thiếu quyết định versioning.
- Service lấy từ Order và bắt buộc Plant Health; payload không có service field.
- Khóa `FindingId` là stable source identity và `FindingVersion` là output revision
  tăng khi reprocess; không tái tạo identity chỉ để né dedup. BE2 phải lưu mapping
  với source observation/job nếu bảng observations hiện tại tạo row mới qua mỗi job.
- `JobId`, model/dataset/annotation/threshold/boundary policy phải từ processing run
  thực tế, không phát sinh metadata giả tại thời điểm publish. Handoff retry dùng lại
  identity; payload trong Outbox được giữ ổn định.
- Chốt 2–3 team-labelled classes từ configured catalogue và annotation rules.
  Không tự coi SUNBURN là disease chỉ vì code mẫu; nhãn ambiguous/non-disease phải
  giữ đúng semantics. Không map unknown/Healthy/Soft_Rot sang bệnh khác bằng fallback.
- Khi schema V4 không diễn đạt được source identity hoặc loại uncertain output cần
  hỗ trợ, chốt thay đổi versioned contract trước khi code phụ thuộc; không nhét field
  bắt buộc vào metadata không có quy ước. Không âm thầm sửa nghĩa wire đã sử dụng.
- Tạo golden examples gồm full envelope, normal finding, unmatched/candidate-only,
  boundary exception, zone/guidance, duplicate và reprocess. Ví dụ payload riêng
  trong tài liệu bàn giao không thay thế full envelope.

### 3.2. Pending model và reprocess

Tên entity/table dưới đây là đề xuất cần khóa ở PR 7A.0; dùng chung foundation PR
7.0–7.1, không tạo một bộ finding khác trùng với Step 7.

| Model | Dữ liệu/invariant cần có |
|---|---|
| `SurveyResult` | Một aggregate/Order theo unique hiện tại; thêm source Mission, boundary/base-map reference, result nature, limitation, observed time và active import revision reference cần cho nghiệp vụ |
| `PlantHealthImportBatch` | Tenant/Order/Farm/Mission, HandoffId/JobId, source versions, provenance snapshot, payload fingerprint, imported time, revision/SupersedesBatchId, concurrency |
| `PlantHealthFinding` | Stable source ID/version, batch/result, original class/position/confidence/uncertainty/evidence/observed time, nullable Plant/candidate reference, resolution state, SupersedesFindingId và concurrency |
| Pending boundary exception | Source reference, original point, classification/distance/threshold, evidence và blocking state; không bị bỏ qua khi finding chưa gắn Plant |
| Target `DiseaseZone` + memberships | Source proposal, batch/result/map references, original geometry, suspected class, membership version, nullable Plant/candidate references cho pending và evidence |
| Pending sourced-guidance suggestion | Giữ ID/code/version/source từ producer; chưa coi là guidance đã chọn, chưa publish |

- Tạo result lần đầu, lần sau thêm batch/finding revision; không INSERT result thứ
  hai để vượt `uq_survey_results_order`. Không overwrite original của revision trước.
- Batch mới được kích hoạt bằng expected version của result; review 7B phải gắn
  batch/finding version để không dùng decision cũ cho output mới.
- Chỉ nhận revision mới khi result còn `PendingReview`. Nếu `Approved`/`Published`
  hoặc Order completed/cancelled, từ chối handoff mới bằng `ResultLocked`; duplicate
  hợp lệ vẫn trả outcome cũ. Workflow reopen sau approval thuộc thay đổi riêng.
- Source version cũ đến muộn được ghi nhận theo policy history nhưng không thay
  active revision mới hơn; không dùng `ProducedAt` đơn lẻ để suy ra version.
- Unique tối thiểu: `(TenantId, HandoffId)` cho batch; source revision key
  `(TenantId, MissionId, FindingId, FindingVersion)` cho finding canonical;
  `(BatchId, SourceProposalId)` và `(ZoneId, MembershipVersion, SourceMemberKey)`.
  Nếu handoff mới dùng lại finding revision y hệt, link tới finding canonical;
  không tạo bản sao hoặc chuyển batch owner làm mất lịch sử.
- Dùng fingerprint chuẩn hóa nội dung nghiệp vụ để phát hiện cùng identity/version
  nhưng payload khác. Trùng giống nhau là idempotent; khác nhau là conflict.
- Composite FK/check constraint giữ cùng Tenant/Farm/Order/result và map references;
  không bắt buộc PlantId cho unresolved item và không tự tạo Plant mới trong 7A.
- Point/Polygon dùng SRID 4326; kiểm tra tọa độ, topology, membership và boundary
  bằng PostGIS. Geometry malformed bị từ chối; near/outside/ambiguous hợp lệ được
  giữ pending/blocking, không tự clip hay gán sang Farm khác.
- Mở rộng legacy `DiseaseZone` theo discriminator/constraint target rõ ràng: field
  severity/health-level legacy có thể nullable cho V4, bắt buộc null trên target;
  giữ invariant legacy cho dữ liệu cũ. Thêm `SuspectedClassCode` và sourced-guidance
  references riêng, không alias sang `TreatmentRecommendation`.
- Tạo migration mới theo expand/validate; không sửa migration đã phát hành, không
  tự điền MODERATE để qua NOT NULL và không drop legacy trong 7A.

### 3.3. Typed port và transaction

- Public port đề xuất: `IntegrationContracts/Health/IPlantHealthPendingImport.cs`,
  input/output là typed DTO, không nhận/trả DbContext hoặc domain entity.
- BE1 triển khai import service tại integration persistence layer
  `AgriDrone.Database/Health`, dùng target factories và validation của Plants/Surveys.
  BE2 handler chỉ điều phối Inbox và gọi port; không viết SQL vào bảng domain.
- Port implementation và handler resolve cùng scoped `SurveyResultPublicationDbContext`.
  `InboxExecutionCoordinator` là owner duy nhất của transaction. Import service
  không mở transaction/DbContext khác và không commit độc lập.
- Mission context xác minh bằng typed query do Missions cung cấp: Tenant/Farm/Order,
  purpose, source job và capture map versions; không cho handler đọc Missions repository.
  Dùng explicit authoritative farm/map/Plant snapshot để kiểm tra association.
- Validate toàn bộ batch trước khi stage mutations. Coordinator hiện commit cả
  tracked changes nếu callback trả PermanentFailure: không stage một nửa batch rồi
  return failure. Failure sau khi bắt đầu ghi phải rollback toàn transaction; nếu
  cần lưu permanent error, ghi bằng transaction sạch sau rollback.
- Coordinator có thể gọi `SaveChanges` nhiều lần để claim Inbox; yêu cầu là một
  transaction atomic, không phải ép tất cả chỉ một lần `SaveChanges`.

Luồng thành công:

1. Processor kiểm tra envelope/schema và V4 payload; chạy bằng integration/background
   execution context, không giả mạo SystemManager từ ActorId trong envelope.
2. Coordinator mở transaction và claim Inbox `(ConsumerName, MessageId)`.
3. Kiểm tra business dedup handoff/source revision và fingerprint trước state gate;
   duplicate đã xử lý được ACK cả khi Order đã chuyển trạng thái.
4. Lock/reload Order/result/import revisions theo thứ tự ổn định. Validate context,
   service, source, map snapshot và toàn bộ items; quyết định retry/failure trước ghi.
5. Tạo/reuse result PendingReview; lưu batch, findings, exceptions, zones/memberships,
   guidance suggestions và provenance. Không gộp mù findings của nhiều job/model.
6. Lần import đầu gọi `MarkAnalysisReady` khi Order `InProgress`; reprocess hợp lệ
   ở `PendingReview` không chạy lại transition. Dùng import time từ TimeProvider cho
   transition, giữ ObservedAt/ProducedAt riêng để event đến muộn không vi phạm timestamp.
7. Ghi audit actor integration/source identity và Inbox outcome gồm IDs/counts,
   unresolved/blocking summary; không phát publication/completion outcome.
8. Coordinator lưu Inbox Completed cùng business changes, commit rồi ACK. Exception
   trước commit rollback cả claim/data/audit/transition.

## 4. Validation và lỗi cần khóa

- Required IDs khác empty, UTC timestamps, non-null collections, text/collection/body
  limits, confidence [0,1], positive versions, unique source keys và valid geometry.
- Kiểm tra Tenant của envelope khớp Order/Farm; Mission phải là paid Plant Health
  đúng Order và source processing run. Không nhận Harvest Readiness/baseline output.
- Boundary/base-map phải đúng version dùng lúc capture/job và thuộc Farm; không
  fallback sang draft/old/random map. Version không còn current không tự động sai
  nếu đúng historical snapshot của Mission; đánh dấu cần review theo policy đã khóa.
- Plant/candidate/membership references khác Farm hoặc map snapshot là context error;
  references chưa resolve hợp lệ phải giữ pending, không skip item.
- Guidance phải là reference có provenance, không nhận free-form advice. Không tìm
  được catalogue entry: giữ suggestion unresolved/blocking cho 7B, không tự chọn
  guidance khác; class/source/version mâu thuẫn được đánh dấu rõ.
- Reject severity/health-level/diagnosis fields tại V4 JSON boundary trước khi
  shared serializer bỏ unknown fields. Áp dụng kiểm tra chuyên biệt V4; không đổi
  shared serializer sang strict toàn hệ thống làm vỡ compatibility V1–V3.

| Outcome | Trường hợp | Xử lý |
|---|---|---|
| Completed/ACK | Import mới hoặc duplicate cùng payload | Trả IDs/outcome ổn định; không nhân bản data/audit |
| Retry | Event đến trước dependency dự kiến, DB/transient failure, lock/concurrency có thể retry | Rollback, retry hữu hạn theo hạ tầng hiện có, sau đó DLQ |
| PermanentFailure/DLQ | Payload/schema sai, context/service/version mâu thuẫn, identity reused khác payload, result đã khóa | Không có pending writes dở; lưu mã lỗi và correlation |
| Pending/blocking | Unmatched, candidate-only, near/outside boundary, ambiguous hoặc unresolved guidance hợp lệ | Commit bản gốc và blocking state cho 7B |

Mã lỗi đề xuất prefix `PlantHealthAnalysisReadyV4`: `InvalidPayload`,
`ContextMismatch`, `UnsupportedService`, `SourceVersionConflict`, `MapVersionMismatch`,
`InvalidGeometry`, `IdentityPayloadConflict`, `ResultLocked`, `DependencyNotReady`.
Thiếu dependency không được retry vô hạn; phân biệt chưa tới với identity không tồn tại.

## 5. Thứ tự PR và đầu ra

PR ở đây là đơn vị công việc, chưa tạo GitHub PR hoặc commit. 7A.0–7A.5 đã triển
khai trong working tree; 7A.6 vẫn deferred theo Final Test Phase. Wire V4 giữ nguyên;
source revision lấy từ job snapshot qua typed query. Unknown/confusing label giữ
raw original và target class null. Producer không còn tạo zone/guidance giả khi
AI source chưa cung cấp proposal. Chi tiết các quyết định cuối cùng ở ADR 0013.

| PR | Owner | Công việc | Exit gate |
|---|---|---|---|
| 7A.0 — Contract/ADR | BE1 + BE2 | Khóa quyết định mục 3–4; golden envelopes; source/version mapping; typed ports; sửa tài liệu bàn giao và BE2 Step 13 cho cùng ownership/no-severity | Không còn MODERATE/fallback disease/transaction owner mơ hồ; review manifest rõ |
| 7A.1 — Foundation | BE1 | Dùng chung PR 7.1: import batch, finding revision, exception, zone/guidance target, result links, constraints/index và expand migration; cập nhật DbContexts | Target V4 lưu được unresolved và không severity; một result/Order; build/EF model hợp lệ |
| 7A.2 — Producer | BE2 | Sửa publisher lấy job/provenance/map versions thật, stable source identity/revision, resolved vs suggested, unknown/non-disease, exceptions, real guidance references hoặc empty | Payload đủ semantics để consumer chấp nhận; Outbox/idempotency không mất source version |
| 7A.3 — Pending import | BE1 | Implement typed import, validation/context query, dedup/fingerprint/reprocess, atomic pending writes + Order transition + audit | BE2 gọi port không chạm entity/repository; không publish/latest-state writes |
| 7A.4 — Consumer/wiring | BE2 | V4 processor/validator/raw JSON guard/handler, coordinator, DI, cấu hình retry/DLQ và diagnostics | Chỉ một registration V4; handler/import cùng transaction; không đăng ký legacy health writes mới |
| 7A.5 — CodeComplete | BE1 + BE2 | Source review, targeted/solution build, EF pending-model check, migration/runbook review, checklist và test backlog | `CodeComplete — AwaitingFinalTest`, không tuyên bố runtime/E2E đã pass |
| 7A.6 — Final Test | BE1 + BE2 | Viết/chạy toàn bộ backlog mục 7 trên PostgreSQL/PostGIS và RabbitMQ theo Final Test Phase | Evidence đạt đủ acceptance mới ghi 7A `Done` |

Thứ tự phụ thuộc: 7A.0 → 7A.1/7A.2 → 7A.3 → 7A.4 → 7A.5 → Final Test.
Producer có thể triển khai sau khi khóa contract, nhưng không merge integration gate
nếu target schema/import chưa tương thích. Review/publication 7B–7E dùng tiếp cùng
foundation; không chờ triển khai toàn bộ 7B–7E mới hoàn tất code 7A.

## 6. File manifest dự kiến

Các đường dẫn sau tương đối với `backend/src`; tên mới được chốt ở 7A.0.

| Action | File/nhóm file |
|---|---|
| Add | `BuildingBlocks/AgriDrone.IntegrationContracts/Health/IPlantHealthPendingImport.cs` và typed outcomes/context query contracts |
| Add | `BuildingBlocks/AgriDrone.IntegrationContracts/Messaging/Validation/V4BusinessContractValidators.cs` |
| Add | `BuildingBlocks/AgriDrone.Database/Health/PlantHealthAnalysisReadyV4Processor.cs`, `PlantHealthAnalysisReadyV4Handler.cs`, `PlantHealthPendingImport.cs` và V4 raw JSON validation adapter |
| Add/modify | `Modules/AgriDrone.Modules.Plants/Domain/Health/` và staging/configurations cho findings, batch links, exceptions, guidance suggestions; vị trí batch có thể thuộc Surveys theo ADR |
| Modify | `Modules/AgriDrone.Modules.Plants/Domain/DiseaseZones/` và configurations cho no-severity target/memberships |
| Modify | `Modules/AgriDrone.Modules.Surveys/Domain/Results/SurveyResult.cs` và configuration, import batch/result revision associations |
| Modify | `BuildingBlocks/AgriDrone.Database/SurveyResultPublicationDbContext.cs`, `DependencyInjection.cs`, schema DbContext/model snapshot và module DbContexts liên quan |
| Add | Migration expand mới cho 7A; nếu PR 7.1 foundation đã merge thì chỉ tạo migration delta, không sinh schema trùng |
| Modify | `Modules/AgriDrone.Modules.Missions/Infrastructure/Persistence/MissionPlantHealthPublisher.cs` và source identity/provenance persistence/query adapter cần thiết |
| Verify/modify | `AgriDrone.Api/appsettings.json`, messaging bootstrap/options; giữ consumer name/queue/routing hiện tại |
| Modify | Master plan mục 15/27, tài liệu bàn giao consumer, BE2 specification Step 13 |
| Add | `docs/operations/be1-be2-phase7a-completion-report.md` sau triển khai, gồm build/model evidence, migration và replay/DLQ runbook |

## 7. Backlog Final Test và điều kiện hoàn thành

Theo master plan, trong phase code chỉ ghi backlog, build và EF model check; chưa
viết/chạy backlog test 7A trước Final Test Phase. Không dùng build thay evidence runtime.

- [ ] Golden envelope round-trip và producer/consumer thống nhất wire constants,
  nullable references, UTC, limits; severity/diagnosis field bị reject tại JSON boundary.
- [ ] Normal import lưu đủ result/batch/findings/zones/memberships/guidance/exceptions,
  full source provenance và audit; Order InProgress → PendingReview.
- [ ] Same MessageId, different MessageId/same HandoffId, repeated source revision
  qua handoff khác, concurrent delivery đều không nhân bản business data/audit.
- [ ] Same source key/version hoặc HandoffId khác payload trả conflict, không ACK
  mù và không overwrite bản trước.
- [ ] Reprocess version mới giữ original/history/Supersedes, một result/Order;
  older version đến sau không thay active revision; result Approved/Published bị khóa.
- [ ] Unmatched/candidate-only/ambiguous/boundary exception và unresolved guidance
  được giữ pending/blocking, không skip, không tự gán Plant hoặc disease class.
- [ ] Sai Tenant/Farm/Order/Mission/service/job/map version bị từ chối;
  historical capture version hợp lệ không bị đổi sang map current tùy ý.
- [ ] Confidence/class/catalogue/provenance/media/geometry/membership validation;
  SRID/topology và same-Farm FK đúng trên PostgreSQL/PostGIS thật.
- [ ] Failure tại item thứ N hoặc trước commit rollback Inbox/pending/audit/Order;
  PermanentFailure sau validation không vô tình commit các tracked mutations dở.
- [ ] Crash sau commit trước ACK rồi redelivery trả outcome cũ; retry dependency
  đến muộn, retry exhausted/DLQ và replay theo runbook có evidence.
- [ ] Background actor không được giả mạo user; 7A không publish zone/guidance,
  không đổi latest-reviewed/Plant.HealthLevelId hoặc complete Order/Mission.
- [ ] Owner published-only query không thấy pending/rejected data.
- [ ] Fresh/upgrade migration, constraints/concurrency và EF pending-model check đạt;
  không sửa/drop migration hoặc legacy data ngoài scope.
- [ ] Legacy observation review/resolve không thể ghi health-level hoặc tạo Plant
  từ V4 source job; dùng pending-result workflow. Migration Down chặn khi có 7A data.
- [ ] Source architecture và runtime chỉ có một consumer V4 đúng registration;
  V1–V3 compatibility/drain không tạo severity target writes.

### Mốc CodeComplete

- [x] PR 7A.0–7A.4 hoàn tất, manifest/ADR/handoff đồng bộ với master plan.
- [x] Targeted/solution build và EF pending-model check đạt, lưu command/output thật.
- [x] Migration có preflight, upgrade/roll-forward notes; queue/DI/retry/DLQ được review.
- [x] Có completion report và backlog Final Test; không đánh dấu checklist acceptance
  đã pass nếu chưa thực thi.

### Mốc Done

- [ ] Toàn bộ backlog Final Test 7A đạt với database/broker thật và evidence lưu được.
- [ ] BE1 nhận được pending data đầy đủ để bắt đầu review 7B, không cần import thủ công
  hoặc sửa trực tiếp database.
- [ ] Handoff ghi rõ revision/current-batch/expected-version semantics cho 7B và còn
  giữ pending visibility boundary tới khi 7D publish.
