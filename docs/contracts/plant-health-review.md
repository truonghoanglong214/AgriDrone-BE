# Plant Health 7B Manager API contract

Prefix: `/api/system-manager/survey-orders/{orderId}`. Actor phải là active,
available primary SystemManager của Farm. Tenant/reviewer/time lấy server-side.

## Read → review → reload

1. `GET health-findings` lấy ResultVersion, BatchId, finding SourceVersion,
   SourceRowVersion và Review.Version nếu đã có review.
2. `POST health-findings/{findingId}/review` với operation GUID mới và versions vừa đọc.
3. Dùng versions trong response cho command tiếp theo. Nếu 409, reload; không đổi
   OperationId để replay payload đã commit. OperationId mới dùng cho quyết định mới.
4. GET exceptions/zones/guidance options và review theo dependencies. Cuối cùng GET
   `health-results/review-summary`; 7B không có endpoint approve/publish.

Single finding body mẫu (IDs chỉ minh họa, phải dùng IDs/versions thật từ queue):

```json
{
  "operationId": "11000000-0000-4000-8000-000000000001",
  "batchId": "22000000-0000-4000-8000-000000000001",
  "expectedResultVersion": 100,
  "expectedSourceVersion": 1,
  "expectedReviewVersion": null,
  "decision": "Correct",
  "reason": "Đối chiếu ảnh nguồn và liên kết lại với trụ đã có trên bản đồ khảo sát.",
  "evidenceMediaIds": [],
  "correctedClassCode": "ANTHRACNOSE",
  "correctedPlantId": "33000000-0000-4000-8000-000000000001",
  "correctedPosition": { "type": "Point", "coordinates": [106.12345, 10.12345] }
}
```

`expectedReviewVersion=null` nghĩa là chưa có review head. SourceVersion là revision
AI; ResultVersion/Review.Version/SubjectRowVersion là concurrency tokens, không cộng
thủ công. SubjectRowVersion của zone dùng cho ExpectedZoneVersion của guidance command.
Near-boundary cần evidence thực; các quyết định khác vẫn xác minh media IDs nếu có.

## Decisions và dependencies

| Subject | Decisions | Bindings cần giữ |
|---|---|---|
| Finding | Approve, Correct, Reject, NonDisease, Ambiguous | Batch + canonical finding + source revision + current head version |
| Boundary exception | AcceptCorrectedPosition, ExcludeEvidence, NeedsEvidence | Exception row version; resolving decision bind FindingId + FindingReviewId |
| Zone | Approve, Correct, Reject, Ambiguous | Zone row/head versions; Correct có geometry/class/members explicit |
| Guidance | Select, RejectAll, NeedsEvidence | ZoneReviewId hiện tại, zone row version và guidance head version |

Batch finding endpoint `POST health-findings/review-batch` nhận OperationId, BatchId,
ExpectedResultVersion và Items gồm FindingId cùng các field review như single body.
Tối đa 100 items; toàn command commit hoặc rollback. Body API tối đa 1 MiB.

Zone member body: `{ "findingId": "...", "sourceVersion": 1, "findingReviewId": "..." }`.
Selected findings phải accepted/resolved, cùng class/context và nằm trong zone.
Approve không nhận corrected fields; Correct bắt buộc có ít nhất một corrected field.
Select guidance chỉ nhận catalogue GuidanceId; code/version/source/text resolve từ server.

## Query và response

- Queues: `health-findings`, `health-boundary-exceptions`, `disease-zones`.
- Filters: BatchId (default active), PageNumber, PageSize 1–100, Decision,
  ResolutionState. ClassCode áp dụng findings/zones; ZoneId chỉ findings.
- Finding history: `health-findings/{findingId}/reviews`, chỉ BatchId/pagination.
- Zones trả cả Review và GuidanceReview để đọc hai head versions riêng.
- Response giữ original riêng với effective review snapshot, reviewer/reason/evidence,
  blocking reasons, suspected/preliminary limitation. Current original remains unchanged.
- Reprocess đổi active batch: command mới trả BatchChanged. Receipt replay cũ chỉ
  trả historical outcome với IsReplay=true; kiểm tra IsCurrentBatch và reload khi cần.
- Guidance options: `disease-zones/{zoneId}/guidance-options`; selection action:
  `disease-zones/{zoneId}/guidance-review`. Boundary review dùng suffix `/{id}/review`.

Errors theo mapper hiện hữu: 400 malformed/binding/unknown fields, 401 unauthenticated,
404 hidden resource/unassigned actor, 409 stale/locked/reused operation/dependency,
422 domain/geometry/catalogue/evidence validation. Error code prefix `PlantHealthReview`.
Không có severity/health-level/status derived/reviewer/time/TenantId/free-form-advice
fields trong mutation DTOs; unknown fields bị từ chối cả ở nested request DTOs.
