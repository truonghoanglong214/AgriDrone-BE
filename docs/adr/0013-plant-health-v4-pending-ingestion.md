# ADR 0013 — Plant Health V4 pending ingestion và source revisions

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

- Date: 2026-11-09
- Status: Accepted for implementation; runtime acceptance awaits Final Test Phase
- Scope: UC09 / Step 7A. Supersedes severity-dependent ingestion guidance for V4.

## Quyết định

1. BE2 sở hữu producer/transport/Inbox coordination. BE1 sở hữu no-severity domain,
   pending-import port và implementation tại integration persistence layer. Handler
   không dùng repository/entity nội bộ của Plants/Surveys; Missions cung cấp typed
   source context query. Một scoped publication DbContext và một Inbox-owned
   transaction bao gồm pending, audit và Order transition.
2. Giữ wire V4 `PRELIMINARY_SUSPECTED`/`SUSPECTED`, event/routing
   `health.analysis-ready.v4` và consumer/queue đã có. V1–V3 giữ compatibility.
   V4 processor deserialize strict payload và reject duplicate JSON keys trước
   shared reader; không đổi unknown-field policy của các contract cũ.
3. Một `SurveyResult`/Order; thêm import batch, canonical finding revisions và
   batch-finding links. Handoff/job/source revision dedup có fingerprint. Fingerprint
   bỏ transport causation, handoff và produced time; không coi delivery mới là AI
   revision mới. Cùng identity khác nội dung là conflict; duplicate được ACK cả sau
   khi result đã khóa. MessageId reuse cũng kiểm tra fingerprint/Tenant trước ACK.
4. SourceVersion là revision tăng theo DiseaseDetection run được commit của Mission,
   lưu trong AiProcessingJob snapshot. FindingVersion phải bằng source run version.
   FindingId được suy ra ổn định từ Mission + image name + detector source ID,
   không chứa class/model/confidence. Detector phải giữ source IDs ổn định giữa
   các lần reprocess cùng observation; nếu upstream đổi ID thì đó là source mới,
   không tự suy diễn quan hệ supersedes theo GPS.
5. Mỗi batch giữ nguyên full payload/provenance; source finding giữ original riêng,
   geometry, observed time và revision. Reprocess thêm dữ liệu mới/Supersedes, không
   DELETE observations cũ. Active batch chỉ chuyển tới source version cao hơn,
   chỉ khi result PendingReview; older output có thể vào history. Approved/Published
   hoặc Order closed không nhận output mới qua ingestion.
6. Target finding chỉ gán SuspectedClassCode khi code thuộc configured suspected
   classes (2–3). Unknown/Healthy/SUNBURN nếu là confusing label giữ nguyên trong
   Original, target class null và NeedsReview; không fallback sang bệnh khác.
   PlantId null/candidate-only và near/outside boundary giữ blocking. Boundary
   review threshold của V4 là 1 m; distance được tính PostGIS geography trên capture
   boundary. Không tự map suggested Plant thành resolved Plant.
7. Expand DiseaseZone hiện hữu: discriminator SourceSchemaVersion=4, HealthLevelId
   null, typed batch link, suspected class, guidance suggestions riêng và pending
   memberships cho Plant/candidate. Legacy vẫn yêu cầu HealthLevelId. V4 không đi
   qua legacy treatment/severity Review method. Sourced guidance chưa resolve vẫn
   giữ original và blocking; ingestion không chọn hoặc publish guidance.
8. Payload map versions phải trùng immutable source job snapshot, có published/approved
   evidence và cùng Tenant/Farm. Không chọn current map tùy ý. Hiện membership
   association được kiểm tra bằng Plant.FarmBaseMapVersionId/candidate batch map;
   nếu Plant đã chuyển map mà không có historical membership snapshot phù hợp,
   trả context conflict thay vì tự cho qua. Không nhận nhầm map để giữ availability.
9. Producer lấy model/dataset/annotation/limitation/boundary policy từ model registry
   và ghi snapshot vào completed job; threshold ghi giá trị thực tế của invocation.
   Registry phải có đúng một active DiseaseDetection model với ModelName đúng
   detector response. Thiếu metadata trả lỗi rõ ràng và yêu cầu reprocess; không
   sinh fake provenance. Capture boundary/map phải từ Mission, không fallback draft.
10. AI response hiện chưa có authoritative zone proposals/guidance. Producer phát
    collection zones rỗng thay vì tự tạo guidance UUID hoặc gán disease cho nhãn
    ambiguous. Consumer vẫn nhập đầy đủ V4 proposed zones/memberships/guidance từ
    producer có nguồn hợp lệ. Phần clustering cũ không được coi là source evidence.
11. Validate toàn batch trước mutation. PermanentFailure của coordinator có commit
    Inbox: không trả lỗi permanent sau khi đã stage business changes. Lỗi sau stage
    phải throw/rollback. SaveChanges nhiều lần vẫn trong cùng transaction; không
    được commit độc lập ở import port.

## Hệ quả và bàn giao 7B

- 7B đọc ActiveImportBatchId, links và Original, dùng expected result/batch/finding
  versions. Không dùng decisions của revision cũ cho revision mới.
- Audit actor là System/integration với Tenant/Farm/SourceJobId, không giả mạo user.
- Result/Order chỉ tới PendingReview; không có publication event hoặc Mission complete.
- Schema expand và migration mới; không apply database hoặc chạy acceptance tests
  trong phase code. CodeComplete và Done có gate riêng trong implementation plan.
- Fixtures trong `docs/contracts/plant-health-v4` dùng dữ liệu giả để khóa shape;
  không phải production seed hoặc evidence integration đã chạy.
