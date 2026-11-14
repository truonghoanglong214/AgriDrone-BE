# ADR 0016 — Atomic preliminary Plant Health publication

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

Date: 2026-11-09
Status: Accepted for implementation; runtime acceptance deferred to Final Test

## Decision

The existing `SurveyResultPublicationDbContext` implements
`ISurveyResultPublicationUnitOfWork`. The typed `IPlantHealthResultPublisher`
orchestrates one transaction and one `SaveChangesAsync` through that unit of work.
It shares the 7B/7C authorization, locking and validation implementation instead of
creating an alternative approval policy.

Only a current primary active/available SystemManager may publish. Authorize and
lock Order then result, check the actor-scoped operation receipt, require Order
PendingReview/result Approved, and match result xmin, batch, review revision and
approval fingerprint. Replay reauthorizes before returning its stored response;
another operation on an already published result conflicts.

Recompute live blockers, compare every required head ID/version/decision/value
fingerprint with the immutable approval, and recheck the original handoff,
source job/input images, evidence ownership, capture map/boundary, reviewed
geometry/membership and current catalogue/guidance effectiveness. Lock these
dependencies through the commit. Expired guidance requires an explicit future
revision workflow; publication does not edit or automatically reopen approval.

## Published values and history

Create one immutable `survey.plant_health_publications` row per result. Its payload
copies only accepted/corrected finding values and approved zone geometry/members,
guidance decision and the selected cited guidance text/version. Retain source
finding/review IDs, original observed time/confidence/uncertainty/limitations,
reviewer/time, source batch/job/handoff/maps, model/dataset/annotation provenance,
approval fingerprint, publication actor/time and preliminary/team-label limitation.
Rejected/non-disease/boundary decisions remain in immutable internal review history;
the existing approval snapshot retains their exact references and evidence IDs.
Do not place pending AI bodies, raw media/URLs, severity or diagnosis in the event.

Internal persisted payload JSON uses PascalCase; wire events use the existing
camelCase envelope serializer. The fingerprint hashes the typed payload with the
existing V4 camelCase fingerprint helper. This format is immutable; changes require
an explicit versioned contract/migration, including its database guard.

Accepted zones become Published. Existing membership snapshots are retained.
No new memberships are synthesized during publication and no historical zone from
another result is overwritten. New results already have separate zone identities.
Publication history, published V4 result/zone metadata and zone memberships are
protected against updates/deletes. Deferred constraints require publication,
its frozen result metadata and Completed Order to coexist at commit.

## Latest-reviewed ordering

`plant.latest_reviewed_plant_health` is derived state, separate from Plant and its
legacy HealthLevel. One row per Plant retains the complete accepted finding set
for that Plant from a single publication, including multiple suspected classes.
It carries preliminary nature and limitation explicitly.

The ordering key is `(max source ObservedAt for this Plant in this result,
PublicationVersion, SurveyResultId)`, lexicographically ascending; only a strictly
greater key replaces current state. PublicationVersion is a positive monotonic
sequence within Tenant/Farm, allocated under a transaction advisory lock on that
context before loading dependencies/latest rows. Existing latest rows also take
ordered row locks and xmin concurrency protection. A database guard verifies the
exact published Plant finding set/time/context and rejects backward ordering.

Normalize observed timestamps to PostgreSQL microsecond precision before building
the published payload/order key. Compare persisted approval actor time at that same
precision; the immutable 7C JSON can retain finer .NET ticks. Original source/approval
JSON remains unchanged. This avoids treating timestamp storage precision as a review change.

An older result still gets publication history, published zones, completed Order,
audit and events even when no latest row advances. Empty/all-rejected results do
not erase an earlier suspected state or imply HEALTHY. They carry the approved
`NO_ACCEPTED_SUSPECTED_FINDINGS` conclusion and still require real processed imagery.

## Atomic outcomes and rollback

The commit contains publication history, changed latest rows, zone/result Published,
Order Completed, user audit, operation receipt, preliminary publication outcome
Outbox and owner notification-intent Outbox. No broker call or notification is
made inside the request. Any failure, cancellation or conflict rolls the transaction
back; tracked changes are cleared before returning/rethrowing an error.

`IPlantHealthPublicationCheckpoint.AfterPlantStagedAsync` is a replaceable failure
seam called after each Plant is staged. Production is a cancellation-aware no-op.
Final Test can throw at Plant N and verify that no business/audit/receipt/Outbox
rows survive. No test endpoint, fault configuration or tests are added in 7D.

The existing integration serializer's 4 MiB message bound remains in force. An
oversized event fails publication atomically; there is no partial/chunked outcome.

## Boundaries and rollout

7D writes `health.preliminary-result-published.v1` and
`health.publication-notification-requested.v1`. 7E/BE2 must wire/deduplicate their
consumers, validate published outcomes, complete Mission and resolve current owner
recipients/build the published-only read model. 7D never mutates Mission directly
and does not claim email delivery or owner-portal readiness.

Apply 7A → 7B → 7C → `20261109061911_Step7DPlantHealthAtomicPublication` before enabling
the combined application. The migration expands schema and installs guards; it
refuses existing V4 Published rows without explicit history reconciliation and
does not manufacture publication records. Down refuses populated publication/latest
evidence. Prefer roll-forward recovery. No migration is applied by this implementation.
