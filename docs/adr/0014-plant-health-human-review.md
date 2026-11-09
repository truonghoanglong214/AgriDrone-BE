# ADR 0014 — Plant Health human review, active batches và immutable decisions

- Date: 2026-11-09
- Status: Accepted for implementation; runtime acceptance deferred to Final Test Phase
- Scope: UC09 / Step 7B; builds on [ADR 0013](0013-plant-health-v4-pending-ingestion.md)

## Quyết định triển khai

1. BE1 review chỉ qua `IPlantHealthReviewService` và SystemManager result API. API
   dùng application DTOs, không dùng DbContext/entity. Actor lấy từ HTTP execution
   context, Tenant/Farm resolve từ Order; supporting/integration actors không review.
2. Dùng chung append-only `PlantHealthReviewEntry` và current `PlantHealthReviewState`
   với Kind=Finding/Boundary/Zone/Guidance thay cho bốn bộ bảng có cấu trúc giống nhau
   được đề xuất trong plan. Domain factory giới hạn decision theo Kind. Nullable
   subject FKs + check constraint giữ đúng kind/subject; composite FKs giữ batch,
   Tenant/Farm/result, canonical finding và exception context. Typed snapshot lưu
   effective values, member-review bindings và guidance citation/version.
3. Head key `(BatchId, Kind, SubjectId)`, xmin là review concurrency token. History
   có PreviousReviewId và FK cùng context; current pointer trỏ đúng history. AI
   source Version/SourceVersion không bị dùng thay head xmin, và original không đổi.
   Zone member-review IDs trong immutable typed snapshot được validate ở service;
   không có một bảng membership-review thứ hai trùng với original zone memberships.
4. Mutations lock Order → Result, dùng cùng transaction cho review/history/heads,
   result ReviewRevision, zone projection, audit và operation receipt. 7A import
   dùng cùng Order/result lock discipline. Không nối nhiều transaction hoặc gọi HTTP
   nội bộ để ghi các module. SaveChanges trước receipt lấy xmin thực từ PostgreSQL;
   receipt và commit vẫn trong transaction đó.
5. Authorization tái dùng SystemManager access service và bổ sung typed guard dùng
   cùng publication connection/transaction. Guard `FOR SHARE` user, profile, current
   assignment và Farm, kiểm tra active + available, same Tenant/Farm, Farm chưa xóa.
   `CanOperate` hiện không kiểm tra Availability nên guard không dựa riêng vào nó.
   Revoke/suspend/unavailable phải chờ lock hoặc thắng trước và command bị deny.
   Deadlock/serialization/concurrency errors được trả conflict để reload/retry.
6. Review batch tối đa 100 unique findings, một active batch; validate tất cả trước
   append, unknown/stale/foreign item không bị skip. Một lỗi rollback toàn command.
   Requests có ExpectedResultVersion, BatchId và expected item/head/source versions.
7. Operation receipt unique `(TenantId, ActorId, OperationId)`, fingerprint gồm Order,
   action/subject và normalized request. Replay vẫn phải authorize. Cùng content trả
   persisted outcome với IsReplay; batch cũ chỉ trả historical receipt với
   IsCurrentBatch=false. New command không review batch inactive; khác content conflict.
8. Approve/Correct cần configured suspected class, existing Plant có capture map
   association và effective point trong boundary. Correct giữ corrected values
   trong review snapshot; không sửa original/confidence/observed time, không tạo cây.
   Reject/NonDisease không tạo state HEALTHY; Ambiguous vẫn blocking. Near-boundary
   cần evidence. NonDisease/unknown/confusing label không fallback sang disease.
9. Boundary decisions bind finding + current finding review. GUID source reference
   phải khớp SourceFindingId. Với external non-GUID reference, association explicit
   cần supporting media + reason. AcceptCorrectedPosition dùng accepted reviewed
   point/Plant; ExcludeEvidence chỉ dùng rejected/non-disease finding; NeedsEvidence
   vẫn blocking. Re-review finding làm boundary binding cũ stale.
10. V4 zone review tách khỏi legacy treatment/severity method. Geometry/member/class
    correction nằm ở history; Original/Proposed fields giữ nguyên. Parent lưu reviewed
    projection riêng. Accepted zone chưa final guidance vẫn Proposed; valid Select
    hoặc RejectAll mới cho Reviewed. Reviewed membership append version lớn hơn max
    source/current; không hardcode 2 hoặc sửa original snapshot.
11. Zone membership bind accepted finding ReviewId + SourceVersion + Plant, cùng
    effective class và nằm trong reviewed geometry. Finding review thay đổi làm zone
    stale và trả InvalidatedZoneIds; không auto prune hoặc carry decision sang batch mới.
    Zone review mới làm guidance binding cũ stale. Reject zone không cần guidance.
12. Guidance Select resolve server-side theo ID, exact class/condition revision,
    code/version/source/effective window. Citation/version được snapshot. RejectAll
    là final explicit choice; NeedsEvidence chưa final. Query/summary phát hiện retired/
    expired/changed catalogue dù projection cũ đã Reviewed. Không nhận free-form advice.
13. Live summary tính từ active batch/heads/dependencies trong consistent read,
    không dùng ingestion BlockingCount/ResolutionState/HasBlockingItems làm gate.
    ReadyForApproval là derived DTO; Order/result vẫn PendingReview, không gọi
    Approve/Publish/Complete hoặc ghi latest-reviewed state. Zero accepted findings
    không có nghĩa là HEALTHY; policy approve no-disease result thuộc 7C.
14. Input DTOs (kể cả nested GeoJSON/member DTOs) reject unknown fields. Giữ HTTP
    error mapping hiện hữu: malformed/binding 400, domain validation 422, unauthorized
    401, concealed resource/access 404, stale/context/operation conflict 409. Không
    thay global serializer hoặc error mapper của các capability cũ.
15. Migration mới expand schema, split V4/legacy zone constraints, append-only DB
    triggers cho reviews/receipts và guarded Down. Không sửa migration 7A đã bàn giao.
    V4 Published/Superseded lifecycle thuộc migration/publication implementation 7D.

## Bàn giao và kiểm chứng

- Query mặc định active batch; history cần explicit BatchId và current assignment.
  Pagination tối đa 100, stable ObservedAt/ID; history chỉ hỗ trợ batch + pagination.
- Response mutation có result xmin, ReviewRevision, review head xmin, subject row xmin
  và IDs của zone cần reload. Historical review entry không có current xmin thì trả null.
- Review reads dùng repeatable-read transaction; mutations dùng row locks và head/result
  optimistic concurrency. Catalogue rows được share-lock khi mutation.
- Không viết hoặc chạy test trong phase này theo yêu cầu người dùng. Build production
  và EF model checks là verification code/schema, không phải runtime acceptance.
- Final Test giữ backlog security, concurrent revocation/review/reprocess, rollback,
  PostGIS, legacy compatibility và owner visibility trong master plan mục 27.
