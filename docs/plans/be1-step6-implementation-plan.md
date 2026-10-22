# BE1 Step 6 — Farm Base Map và Persistent Plant Identity Implementation Plan

> Ngày audit source: 2026-10-21  
> Phạm vi: Phase 11 / UC08 trên `backend/AgriDrone.sln`  
> Trạng thái mục tiêu sau khi triển khai: `CodeComplete — AwaitingFinalTest`

## 1. Kết luận audit hiện trạng

Step 6 chưa thể xem là đã bắt đầu ở mức business capability, dù nhiều foundation đã có sẵn.

### Đã có và được tái sử dụng

- `BaselineMappingCandidatesReadyV3` và `FarmBaseMapPublishedV3`, descriptor,
  validator, event type và consumer name đã tồn tại.
- Consumer V3 đã được đăng ký. Handler hiện tại deduplicate bằng Inbox, kiểm tra
  Order/Farm cơ bản và chuyển Order từ `BaselineInProgress` sang
  `AwaitingBaselineReview`.
- `MappingPublicationDbContext` đã gom được Farm, FarmBoundary, FarmBaseMapVersion,
  ZoneMapVersion, Plant, PlantChangeEvent, SurveyOrder, Inbox, Outbox và Audit trong
  một DbContext; `IMappingPublicationUnitOfWork` đã có transaction seam.
- `FarmBaseMapVersion` đã có Draft/Published và unique constraint bảo đảm tối đa
  một map Published cho mỗi Farm. `ZoneMapVersion` đã có Draft/Confirmed/Superseded.
- `Plant` đã hỗ trợ tạo từ mapping và cập nhật vị trí map mà vẫn giữ Plant ID.
- `SurveyOrder.RecordBaselinePublication(...)` đã có để ghi map version, confirmed
  pole count và chuyển Order sang `AwaitingPricing`.
- BE2 đã có consumer cho `FarmBaseMapPublishedV3`.
- Step 4 và Step 5 đã có completion report. Build source hiện tại ngày 2026-10-21
  pass với 0 warning và 0 error.

### Còn thiếu để đạt Step 6

- Chưa có producer nào tạo `BaselineMappingCandidatesReadyV3`; source chỉ có DTO,
  validator và consumer. E2E còn phụ thuộc BE2 phát event thật.
- Handler V3 chưa lưu draft, Zone slice hoặc candidate; hiện chỉ đổi Order status.
- Chưa có aggregate/table cho import batch, pending mapping candidate, original và
  corrected decision, evidence, reviewer, reason hoặc optimistic concurrency.
- Chưa có review queue/API cho assigned SystemManager.
- Chưa có `BaselineMappingBatchValidator` chạy toàn Farm/Zone bằng PostGIS.
- Chưa có `FarmBaseMapPublicationOrchestrator` cho publication toàn Farm.
- Chưa có code tạo `FarmBaseMapPublishedV3` Outbox event.
- V1 handler hiện publish từng Zone và phát `ZoneMapPublishedV1`; không được dùng làm
  target Step 6 vì không có pending review, Farm-level atomicity hoặc count handoff.
- `Plant` chưa có đầy đủ semantics `Replace` và replacement relation. Missing
  observation cũng chưa được biểu diễn rõ như một quyết định không retire Plant.
- Chưa nối atomic publication vào `SurveyOrder.RecordBaselinePublication(...)`.
- Chưa có metric/drain evidence cho V1/V2.

Theo quy tắc của master plan, Step 0R–5 chỉ nên được xem là
`CodeComplete — AwaitingFinalTest`; không đổi thành `Done` trước Final Test Phase.

## 2. Dependency gate trước khi code

1. Giữ nguyên payload/routing V1 và V2; Step 6 chỉ bổ sung V3 target path.
2. Chốt với BE2 rằng producer phát đúng `BaselineMappingCandidatesReadyV3`, gồm
   `SurveyOrderId`, `MissionId`, `FarmBoundaryVersionId`, expected current map,
   Zone slices và stable Candidate IDs. Không chờ producer hoàn tất mới code BE1,
   nhưng không tuyên bố E2E complete khi producer chưa tồn tại.
3. Chốt một consumer owner duy nhất cho V3. Chuyển authoritative V3 handling vào
   `AgriDrone.Database/Mapping` để Inbox + draft import + Order status cùng một
   transaction; không giữ hai consumer cùng consumer name ở Surveys và Database.
4. Giữ các thay đổi working tree hiện tại ngoài phạm vi Step 6. Không sửa/chèn vào
   các file System Survey Order query/controller đang có thay đổi dở nếu không bắt buộc.

## 3. Thiết kế dữ liệu cần khóa

### 3.1. Pending import model

Thêm model staging thuộc mapping capability:

- `MappingCandidateBatch`: HandoffId, Order, Mission, Farm, boundary version,
  expected current Farm map, model/algorithm/policy version, status và row version.
- `MappingCandidateZone`: Zone, expected current Zone map, bearing/spacing và batch.
- `MappingCandidate`: CandidateId, source observation, evidence IDs, original point,
  proposed grid, boundary classification/distance/threshold, proposed matched Plant,
  review decision, corrected point/grid/Plant, reviewer, reason, reviewed time và row version.

Các khóa/constraint tối thiểu:

- unique `HandoffId` và unique `(BatchId, CandidateId)`;
- unique `(BatchId, SourceObservationId)`;
- candidate/zone bắt buộc cùng Farm với batch;
- point dùng `geometry(Point,4326)` và GiST index khi cần query không gian;
- review decision và các corrected fields phải nhất quán bằng check constraint;
- không cho hai open batch cùng source Order/Mission trừ retry đúng HandoffId.

### 3.2. Publication model delta

- Bổ sung source `MissionId`, `FarmBoundaryVersionId` và `HandoffId` rõ nghĩa vào
  `FarmBaseMapVersion`; không repurpose âm thầm `SourceMissionGroupId`.
- Thêm factory cho `ZoneMapVersion` gắn trực tiếp với Farm map draft; V1 factory cũ
  được giữ cho drain.
- Bổ sung replacement provenance cho Plant bằng relation/table rõ ràng hoặc
  append-only PlantChangeEvent có old/new Plant IDs. Không dùng notes/free text làm
  quan hệ identity.
- Thêm index/constraint để một active planted pole không có hai Plant identity và
  một Farm chỉ có một current Published map.

Tạo migration mới theo expand → validate → contract; không sửa migration đã phát hành.

## 4. Kế hoạch triển khai theo phase

### 6A — V3 ingestion và pending draft

1. Tạo `BaselineMappingCandidatesReadyV3Consumer/Processor` trong
   `AgriDrone.Database/Mapping` và tháo registration V3 hiện tại khỏi Surveys sau
   khi consumer mới đã được wire.
2. Validate envelope/schema trước khi mở transaction.
3. Claim Inbox theo MessageId/HandoffId.
4. Load authoritative Order, Mission, Farm, current boundary và current map; kiểm tra:
   Baseline purpose, Order/Farm/Mission khớp, `RequiresBaselineMapping`, Order ở
   `BaselineInProgress`, mission đã tới review state và boundary version còn current.
5. Tạo một Farm map Draft, Zone slices và toàn bộ pending candidates; không tạo Plant.
6. Chuyển Order sang `AwaitingBaselineReview`, hoàn tất Inbox và commit cùng transaction.
7. Permanent payload/context errors đi DLQ; missing dependency/network là retry;
   duplicate completed message trả idempotent success.

Exit gate 6A: nhận lại cùng message không nhân đôi draft/candidate; invalid message
không tạo business row; TenantOwner chưa có đường đọc draft.

### 6B — Assigned-manager review và batch validation

Tạo các MediatR feature và API qua một application port, controller không dùng
DbContext trực tiếp:

- `GET /api/system-manager/farms/{farmId}/base-map-drafts/{draftId}`;
- `POST .../candidates/{candidateId}:review`;
- `POST .../candidates:review-batch`;
- `POST .../{draftId}:validate`;
- `POST .../{draftId}:publish`.

Decision nghiệp vụ phải explicit:

- `MatchExisting`: giữ Plant ID và vị trí nếu không đổi;
- `Relocate`: giữ Plant ID, bắt buộc corrected position/grid;
- `CreateNew`: tạo Plant ID mới khi publish;
- `Replace`: tạo Plant ID mới và liên kết Plant cũ;
- `Reject/Unmatched/Ambiguous`: không tạo Plant;
- `MissingObservation`: không retire Plant và không phải candidate được publish.

`BaselineMappingBatchValidator` chạy theo batch, không query N+1, và kiểm tra:

- duplicate candidate/source/grid;
- Zone active và thuộc đúng Farm;
- point SRID 4326, nằm trong approved FarmBoundary và đúng Zone;
- `OutOfBoundary/NeedsReview` bắt buộc manager sửa hoặc reject có reason;
- matched Plant active, cùng Farm/Zone và thuộc current-map context;
- mọi candidate có terminal review decision;
- expected Farm/Zone map version chưa stale;
- tất cả Zone cùng header/order/mission/boundary version.

Mọi mutation dùng expected version. Assigned SystemManager bị revoke hoặc manager
khác Farm nhận lỗi authorization trước khi thấy draft content.

Exit gate 6B: batch chỉ chuyển `ReadyToPublish` khi review và validation toàn Farm
đều đạt; không có partial/unreviewed Zone được publish.

### 6C — Atomic Farm-level publication và count handoff

Tạo `FarmBaseMapPublicationOrchestrator` dùng `IMappingPublicationUnitOfWork`:

1. Re-authorize manager và lock Order/current Farm map/draft bằng expected version.
2. Re-run batch validation bên trong transaction để chống stale review.
3. Tạo/confirm tất cả ZoneMapVersion thuộc cùng FarmBaseMapVersion.
4. Áp dụng toàn bộ Plant decisions và append PlantChangeEvent/replacement provenance.
5. Không retire Plant chỉ vì không thấy trong observation.
6. Tính `ConfirmedSurveyPoleCount` từ active published Plant decisions phía server.
7. Supersede current map khi là amendment; publish draft thành current map.
8. Gọi `SurveyOrder.RecordBaselinePublication(...)` để ghi map/count và chuyển
   `AwaitingBaselineReview → AwaitingPricing`.
9. Ghi audit và Outbox `FarmBaseMapPublishedV3`; có thể phát V2 compatibility event
   riêng trong drain window nhưng không sửa payload V2.
10. Commit một lần; chỉ ACK/return success sau commit.

Nếu lỗi ở Plant thứ N, count handoff, audit hoặc outbox thì rollback map, Zone maps,
Plants, history và Order. Inbox của transaction import 6A vẫn Completed vì draft đã
được nhập hợp lệ; publication command idempotency được bảo vệ bằng PublicationId và
draft status, không giả vờ rollback một Inbox thuộc transaction trước đó.

Exit gate 6C: chỉ một concurrent publisher thắng; retry trả cùng publication; Order,
map, Plant count và V3 event không bao giờ lệch nhau.

### 6D — Persistent identity và amendment rules

1. Hoàn thiện `Plant` behavior cho relocate, replace và lifecycle transition có
   timestamp/reason/reviewer.
2. `MatchExisting`/`Relocate` giữ ID; `CreateNew`/`Replace` tạo ID mới.
3. Owner-reported new/replacement chỉ được apply khi có later-survey candidate và
   manager review liên kết đúng `PlantInventoryChangeReport`.
4. Repeat Plant Health chỉ tham chiếu current map/Plant IDs, không tạo map version mới.
5. Mọi sửa map sau initial publication đi qua `BaseMapAmendment`, lưu expected version,
   reason, reviewer, before/after và audit.
6. Invalidate Plant reference cache chỉ sau commit.

Exit gate 6D: cùng một real pole không sinh duplicate active identity; history của
Plant cũ vẫn truy xuất được sau replacement/removal.

### 6E — Compatibility, observability và evidence

1. Giữ V1 consumer nguyên payload và chỉ dùng cho drain/replay dữ liệu cũ.
2. Bổ sung metric/log có consumer version, received/completed/retry/DLQ và correlation;
   không log raw evidence/PII.
3. Cập nhật OpenAPI, migration report, event catalogue, rollback/runbook và tạo
   `docs/operations/be1-step6-completion-report.md`.
4. Chỉ xóa V1/V2 ở Phase 14 sau retention/drain evidence.
5. Build full solution. Theo master plan, chỉ bổ sung scenario vào mục 27 trong Step 6;
   unit/contract/PostGIS/integration/E2E tests được viết và chạy ở Final Test Phase.

## 5. File/change set đề xuất

### Change set 1 — Schema và domain

- Thêm pending batch/zone/candidate entities và EF configurations.
- Mở rộng `FarmBaseMapVersion`, `ZoneMapVersion`, `Plant`, `PlantChangeEvent`.
- Tạo migration Step 6 theo expand strategy và cập nhật model snapshot.

### Change set 2 — V3 import

- Chuyển V3 consumer ownership sang `AgriDrone.Database/Mapping`.
- Implement Inbox + authoritative validation + pending import transaction.
- Giữ V1/V2 registrations độc lập.

### Change set 3 — Review surface

- Thêm query/command/application port cho draft detail, candidate review, batch review
  và validation.
- Thêm `SystemManagerFarmBaseMapsController` cùng request/response contracts.
- Khóa assignment, tenant isolation và optimistic concurrency.

### Change set 4 — Publication

- Thêm `BaselineMappingBatchValidator`.
- Thêm `FarmBaseMapPublicationOrchestrator`.
- Thêm `FarmBaseMapPublishedV3OutboxFactory`.
- Nối `SurveyOrder.RecordBaselinePublication(...)` và confirmed count.

### Change set 5 — Identity/amendment/drain

- Hoàn thiện replace/relocate/change-report linkage.
- Thêm amendment path và cache invalidation sau commit.
- Thêm compatibility metrics, OpenAPI và completion/migration reports.

Mỗi change set phải build xanh trước khi sang change set tiếp theo; không gộp cả
ingestion, review, publication và identity vào một commit lớn.

## 6. Acceptance checklist trước khi chuyển Step 7

- [ ] V3 duplicate/retry không tạo batch, draft hoặc candidate trùng.
- [ ] Invalid Order/Farm/Mission/purpose/boundary/current-map bị fail closed.
- [ ] Unassigned manager và TenantOwner không đọc hoặc review draft.
- [ ] Toàn bộ candidate của mọi Zone được review/validate trước publication.
- [ ] Outside/near-boundary candidate không tự gán Farm/Plant.
- [ ] First multi-zone publication atomic; stale/concurrent publisher chỉ một thắng.
- [ ] Lỗi giữa publication rollback map/Zone/Plant/history/Order/audit/outbox.
- [ ] `MatchExisting` và `Relocate` giữ Plant ID; `Replace` có relation; missing không retire.
- [ ] `ConfirmedSurveyPoleCount` được tính phía server và khớp V3 payload.
- [ ] Order chuyển chính xác sang `AwaitingPricing` bằng cùng transaction publication.
- [ ] BE2 nhận lại `FarmBaseMapPublishedV3` và gắn đúng source Mission/Order/map version.
- [ ] V1/V2 compatibility còn hoạt động trong drain window và có metric.
- [ ] Build full solution 0 warning/0 error; backlog test Step 6 trong mục 27 đã đủ.

## 7. Rủi ro cần theo dõi

- BE2 chưa phát V3 nên chỉ có thể đạt BE1 code-complete, chưa đạt E2E gate.
- Existing V1 publisher đang thao tác Plant trực tiếp theo từng Zone; tái sử dụng nó
  cho V3 sẽ phá Farm-level atomicity và manager review semantics.
- `Plant` chưa có replacement relation nên không nên encode `Replace` bằng lifecycle
  status hoặc notes tạm thời.
- Batch có thể tới 10.000 candidate; review query và publication phải batch-load,
  tránh một query/SaveChanges cho mỗi Plant.
- Việc defer test theo master plan làm tăng rủi ro cho transaction phức tạp; mọi
  failure seam và expected assertion phải được ghi ngay vào mục 27 để Final Test
  Phase không bỏ sót.
