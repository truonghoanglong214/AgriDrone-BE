# Plant Health 7C approval API

> **V3 scope notice — 2026-11-10.**
> Versioned runtime-baseline contract: retain the original payload, hashes and phase behavior below. For v3, service/context changes need version review; approval alone cannot release an unpaid result, and durable unlock notice/window belongs to the release transaction.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](survey-business-v3.md). Documentation-only update; no runtime change.

Only the active, available primary SystemManager of the Farm can approve/read
approval metadata. TenantOwner has no access through these routes.

```text
POST /api/system-manager/survey-orders/{orderId}/health-results/approve
GET  /api/system-manager/survey-orders/{orderId}/health-results/approval
```

Request (IDs/versions are illustrative; take actual values from the review summary):

```json
{
  "operationId": "11000000-0000-4000-8000-000000000002",
  "batchId": "22000000-0000-4000-8000-000000000001",
  "expectedResultVersion": 100,
  "expectedReviewRevision": 12,
  "reason": "Đã rà soát các quyết định, nguồn dữ liệu và các mục còn chặn của đợt khảo sát."
}
```

Unknown fields are rejected. Clients cannot supply status, reviewer/time, Tenant,
ReadyForApproval, result nature or an approval snapshot. Reason is required, <=2000 characters.

Response includes ResultId/BatchId, result xmin, ApprovedReviewRevision, Status=APPROVED,
ResultNature=PRELIMINARY_SUSPECTED, limitation, reviewer/time, approval fingerprint,
accepted finding count and conclusion. It does not expose the full internal snapshot.

- Retry the exact request/OperationId after a response is lost. It returns IsReplay=true
  without another audit/approval; CurrentStatus may now be APPROVED or PUBLISHED.
- A different request with an existing OperationId returns OperationConflict.
- New approval requests for an approved result return ResultLocked. GET approval
  returns current result version and the original approval metadata; 404 if not approved.
- Zero accepted findings yields NO_ACCEPTED_SUSPECTED_FINDINGS, not HEALTHY. Even an
  empty result needs a completed source run, actual registered input media and a
  positive processed-image count. Empty input/zero processed images cannot be approved.
- Successful approval leaves Order PendingReview and does not publish anything.

Error mapper: 400 binding/unknown fields, 401 unauthenticated, 404 concealed resource,
422 invalid request, 409 stale/locked/blocking/source/provenance/operation conflict.
Codes use the PlantHealthReview prefix, including ApprovalBlocked, ProvenanceIncomplete,
SourceContextChanged, DependencyChanged, ApprovalSnapshotChanged, BatchChanged,
VersionConflict, ResultLocked and OperationConflict.

Approval is a gate for 7D; publication must recheck dependencies and use the frozen
review set. An old review summary is not an approval authorization or evidence snapshot.
