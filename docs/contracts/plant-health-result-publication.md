# Plant Health result publication — Phase 7D

> **V3 scope notice — 2026-11-10.**
> Versioned runtime-baseline contract: retain the original payload, hashes and phase behavior below. For v3, service/context changes need version review; approval alone cannot release an unpaid result, and durable unlock notice/window belongs to the release transaction.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](survey-business-v3.md). Documentation-only update; no runtime change.

Date: 2026-11-09
Implementation: `CodeComplete — AwaitingFinalTest`

## Manager command

`POST /api/system-manager/survey-orders/{orderId}/health-results/publish`

```json
{
  "operationId": "11111111-1111-4111-8111-111111111111",
  "batchId": "22222222-2222-4222-8222-222222222222",
  "expectedResultVersion": 12345,
  "expectedReviewRevision": 8,
  "approvalFingerprint": "<exact 64-character approval fingerprint>"
}
```

Use the result version returned by approval and the same batch/review revision/
approval fingerprint. OperationId is unique within tenant and actor across review,
approval and publication. Unknown request properties are rejected. Actor/tenant/
publication time and status are resolved server-side.

200 response includes OperationId, PublicationId, ResultId, BatchId,
PublicationVersion, Status=PUBLISHED, OrderStatus=COMPLETED, ResultNature,
TeamLabelLimitation, Conclusion, PublishedFindingCount, PublishedZoneCount,
AdvancedPlantIds, PublishedBy, PublishedAt, PublicationFingerprint and IsReplay.
Wire property names use the application's camelCase JSON policy.

PublicationVersion is a durable logical version within Tenant/Farm; it is not an
xmin or a count of advanced Plants. AdvancedPlantIds may be empty for an older
result or zero accepted findings. Conclusion remains the approval's
HAS_REVIEWED_SUSPECTED_FINDINGS or NO_ACCEPTED_SUSPECTED_FINDINGS; neither implies HEALTHY.

Retry the identical body/OperationId after a lost response. Replay rechecks current
assignment and returns the original result with IsReplay=true, without additional
writes. Reusing OperationId for another request/action conflicts. A new OperationId
cannot republish an already Published result.

The existing error mapper returns 404 for hidden/unassigned resources, 409 for
PlantHealthReview.ResultLocked/VersionConflict/OperationConflict/
ApprovalSnapshotChanged/PublicationBlocked/ProvenanceIncomplete, and 422 for domain
validation such as InvalidRequest. Authentication/policy and malformed JSON use
the existing 401/403/400 behavior.

## Preliminary publication outcome

Routing/event type: `health.preliminary-result-published.v1`, schemaVersion=1.
Typed payload: `PlantHealthPreliminaryResultPublishedV1` in IntegrationContracts/Health.
Envelope contains tenantId, actorId, correlationId and a unique messageId;
payload causationId is the publish OperationId.

Payload includes PublicationId/version, HandoffId, JobId/source version,
Order/Mission/Farm/result/batch and capture boundary/base-map IDs,
approved review revision/fingerprint, outcome=PUBLISHED, result nature,
team-label limitation, conclusion, dataset/model/annotation provenance,
approval actor/time and publication actor/time.

Findings contain only accepted/corrected suspected values, source observation
identity/version, reviewed Plant/position/class/catalogue revision, immutable
review reference/reviewer/time and original observation time/confidence/
uncertainty/limitations. Confidence is the source model value, including after a
human correction. Zone entries contain reviewed polygon/membership version,
exact member finding/review/Plant IDs, guidance review decision and optional
frozen cited guidance text/source/version/effective window. RejectAll is explicit
with null guidance. Rejected/non-disease findings and rejected zones are excluded.
No raw/pending payload, media URLs, severity or diagnosis field is emitted.

Published ObservedAt uses PostgreSQL microsecond precision, consistent with the
latest ordering key; full original source precision remains in the internal import snapshot.

PublicationFingerprint in the command response identifies the stored typed payload.
Recompute with PlantHealthV4Fingerprint.ForValue(deserialized typed payload), not a
hash of arbitrary JSON whitespace/property order. Consumers should apply envelope
and business validation and use message/publication IDs for deduplication.

## Notification intent

Routing/event type: `health.publication-notification-requested.v1`, schemaVersion=1.
Typed payload: `PlantHealthPublicationNotificationRequestedV1`.
Carries publication/Order/result/Farm/version, causation, preliminary limitation
and PublishedAt. It does not accept client-supplied recipients or deliver mail.
The future consumer resolves current authorized owner recipients and deduplicates
by PublicationId. Use only published references for owner-facing content.

Both events enter Outbox in the business commit and can be delivered at least once
after commit, in either consumer arrival order. Notification consumers must retry
until their published projection/reference is available. 7D does not register the
BE2 Mission-completion/read-model or notification consumers; their wiring remains
7E/BE2 work. The legacy result-review-state-changed V2 event is not repurposed.
