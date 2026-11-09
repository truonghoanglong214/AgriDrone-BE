# Plant Health Phase 7E API/output handoff

Date: 2026-11-09
Implementation: `CodeComplete — AwaitingFinalTest`

## Manager published read

`GET /api/system-manager/survey-orders/{orderId}/health-results/publication`

Returns 200 with `publication` (typed `PlantHealthPreliminaryResultPublishedV1`)
and `fingerprint`. Requires the current primary active/available SystemManager.
Pending/Approved and hidden resources return 404. A persisted payload/hash mismatch
returns 409 `PlantHealthReview.PublicationSnapshotChanged`.

Publication contains accepted/corrected suspected findings, approved zones, frozen
sourced guidance, preliminary/team-label limitation and source/review provenance.
It includes `comparison` for new 7E publications; older 7D snapshots omit it.
Do not derive comparison from the newest order at read time.

The existing six routes plus batch/boundary/guidance/history/summary routes remain
on `SystemManagerSurveyResultsController`. This is a Manager API, not an owner portal.

## Stored comparison

```json
{
  "policyVersion": "plant-health-publication-comparison.v1",
  "reason": "COMPATIBLE",
  "previousOrderId": "<previous completed Order ID>",
  "previousResultId": "<previous Published result ID>",
  "previousPublicationId": "<immutable publication ID>",
  "previousPublicationVersion": 3,
  "previousObservedAt": "2026-11-01T06:00:00+00:00"
}
```

For no comparison, all previous fields are null. Reasons:

- CURRENT_OBSERVATION_TIME_UNAVAILABLE
- NO_ACCEPTED_FINDINGS_FOR_COMPARISON
- NO_EARLIER_PUBLISHED_PLANT_HEALTH_ORDER
- CAPTURE_MAP_OR_BOUNDARY_MISMATCH
- PROVENANCE_SEMANTICS_MISMATCH

Select only same Tenant/Farm/service + Completed/Published V4 history, matching
capture versions and full provenance, with strictly earlier observations. Search
past incompatible recent candidates for an older compatible candidate. Exact
cross-model/cross-map policy and no-health-inference limits are in [ADR 0017](../adr/0017-plant-health-completion-output-comparison.md).
The `comparison` property is optional and ignored when null during hash serialization;
do not add explicit null to raw-byte hashing. Recompute using the typed fingerprint helper.

## Consumers now registered

| Consumer | Queue | Routing key |
|---|---|---|
| be2-plant-health-preliminary-result-published-v1 | agridrone.be2.plant-health-preliminary-result-published.v1 | health.preliminary-result-published.v1 |
| plant-health-publication-notification-v1 | agridrone.notifications.plant-health-publication.v1 | health.publication-notification-requested.v1 |

Use the existing integration exchange, manual ACK, retry/DLQ and Inbox coordinator.
Both are configured in appsettings; RabbitMq.Enabled stays false by default.
No production broker is contacted during implementation.

Mission completion verifies the **entire event hash** against a persisted published
proof, source job and Mission capture context; structural validation alone cannot
complete it. Durable Mission publication ID/result/version/hash reject conflicting
replays. Notify creates one **in-app** notification per publication for the current
active tenant owner at first successful consumption, with its own Inbox/audit.
It does not send email or wait for Mission completion.

Unavailable references/eligible owner retry. Envelope/context/hash/state mismatch
is permanent and enters DLQ. Valid duplicate message IDs are acknowledged through
Inbox; different message IDs for the same publication produce no duplicate effect.
Notification source uniqueness and Mission publication fields survive Inbox cleanup.
Legacy review-state V1 is rejected for V4 Missions.

## BE2 owner read-model boundary

BE2 can consume the immutable publication event or use the internal
`IPlantHealthPublicationQuery` published-proof port to build/reconcile its
published-only projection. Scope queries to the authenticated owner Tenant/Farm;
preserve preliminary/team-label limitation and source confidence/uncertainty.
Use stored comparison IDs/reason, handle absent comparison on 7D history, and
do not read pending raw Surveys/Plants repositories.

Owner portal controllers/query APIs, notification UI and projection performance
remain Step 9/12 under BE2 ownership. Their runtime delivery/read-model acceptance
is not claimed by the BE1 Step 7 CodeComplete status.
