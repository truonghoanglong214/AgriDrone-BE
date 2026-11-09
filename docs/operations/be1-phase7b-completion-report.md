# Phase 7B — Plant Health human review completion report

> Date: 2026-11-09
> Status: `CodeComplete — AwaitingFinalTest`
> Scope: human review 7B; approval/publication 7C–7E remain separate

## 1. Implemented

Runtime follow-up 2026-11-09: migrations 7A–7E are now applied and local API/consumers
are ready. See [Step 7 closure](be1-step7-implementation-closure-report.md).
Any "not applied/not exercised" statement below describes the original implementation checkpoint.

- Eleven manager API routes for finding queues/history/single/batch review,
  boundary exceptions, zone review, guidance options/decisions and live summary.
- Typed application service/DTO boundary. Input rejects unknown fields, including
  nested geometry/membership DTOs; no client Tenant/reviewer/time/status/severity fields.
- Shared append-only review history with kind-specific subjects/decisions/snapshots,
  current review heads and operation receipts. Original AI values/fingerprints,
  source versions, observations and ingestion BlockingCount remain unchanged.
- Approve/Correct/Reject/NonDisease/Ambiguous finding decisions; Correct can associate
  an existing capture-map Plant, not create/relocate/replace it or change official health.
- Atomic batch of up to 100 unique findings. Unknown/foreign/stale item fails the
  whole command. History/heads/audit/result review revision/receipt share one transaction.
- Expected result/source/subject/head versions, persisted xmin tokens in responses,
  operation replay/conflict detection and old-batch historical receipt behavior.
- Current primary Manager authorization, active user/profile + available state,
  same Tenant/Farm and commit-time SHARE locks. Background execution cannot review.
- Boundary resolution bound to a current finding decision; explicit evidence for
  external source associations, corrected-position validation and rejected-evidence exclusion.
- V4 zone geometry/class/member review, no-severity lifecycle split and reviewed
  membership snapshots. Original class/geometry/memberships are preserved; dedicated
  reviewed fields project accepted decisions without using legacy treatment fields.
- Sourced guidance Select/RejectAll/NeedsEvidence, exact class/condition revision,
  source/version/effective window checks, immutable citation snapshot and live expiry detection.
- Finding re-review invalidates dependent zone/member bindings; zone re-review invalidates
  old guidance binding. Live summary derives blockers from current heads/active batch,
  not ingestion diagnostics. All-rejected results are not labelled HEALTHY.
- 7A import and 7B mutation use Order → Result lock discipline; new active batches
  do not inherit old review decisions. Result and Order remain PendingReview.

Implementation decisions and API usage:
[ADR 0014](../adr/0014-plant-health-human-review.md),
[API contract](../contracts/plant-health-review.md),
[implementation plan](../plans/be1-phase7b-implementation-plan.md).

## 2. Verification performed — no tests

As explicitly requested, **no test files were written and no tests were run**.
All unit/contract/integration/concurrency/security/E2E work stays in Final Test Phase.
Production build was targeted at API and its dependencies, not test projects.

| Command from `backend` | Observed result |
|---|---|
| `dotnet build src/AgriDrone.Api/AgriDrone.Api.csproj --no-restore --nologo -v minimal` | Succeeded, 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext --no-build` | No changes have been made to the model since the last migration |
| `dotnet ef migrations script Step7APlantHealthPendingIngestion Step7BPlantHealthHumanReview ... --no-build --output ../docs/operations/phase7b-human-review.sql` | Upgrade SQL generated; not executed |

These results establish compilation/model alignment, not runtime PostgreSQL locks,
API correctness or rollback evidence. No business database migration was applied,
no HTTP review calls were made, and no RabbitMQ operations were executed in this phase.

## 3. Migration and deployment notes

New migration: `20261109052104_Step7BPlantHealthHumanReview`.
Reviewable [upgrade SQL](phase7b-human-review.sql).

- Creates `plant.plant_health_reviews`, `plant.plant_health_review_states`,
  `survey.plant_health_review_operations`; adds result review metadata and V4 zone
  reviewed projection fields. Existing 7A sources are retained.
- Composite FKs/checks enforce kind/subject/context/current-head/previous-history
  relationships. Zone-batch insert trigger enforces the V4 import batch association
  without making nullable legacy ImportBatchId into a mandatory key.
- Review history and operation receipts reject UPDATE/DELETE through DB triggers;
  publication DbContext also guards immutable history. Current heads remain mutable.
- Legacy DiseaseZone lifecycle constraints are retained within the legacy branch;
  V4 Reviewed/Rejected use sourced-guidance decisions and arbitrary positive source
  membership versions. V4 Published/Superseded implementation belongs to 7D.
- Down refuses to drop populated review history/heads/receipts, active review projections
  or nonzero result ReviewRevision. Prefer roll-forward recovery after human work exists.

Deployment preflight (operator actions; not executed here):

1. Confirm supported database baseline and apply migrations in order 7A → 7B before
   enabling this build. The combined source now expects both schema changes.
2. Confirm active/available primary Manager and active Farm, plus matching source
   job/map/catalogue prerequisites from the 7A report.
3. Confirm V4 zones on the upgrade baseline are still proposed/pending as produced
   by 7A; investigate manually inserted reviewed/published V4 data before changing constraints.
4. Review SQL/functions/triggers and migration history. Do not manufacture severity,
   placeholder Plant IDs or review history to pass constraints.
5. For rollback after human decisions exist, reconcile/archive explicitly first;
   do not bypass append-only or Down guards just to remove the schema.

## 4. Operational behavior and limits

- Queues default to active batch; explicit older BatchId is authorized read-only
  history. Mutation replay still requires current authorization and returns its
  original receipt, not a newly computed current outcome.
- Revoke/profile availability changes serialize with the review guard. PostgreSQL
  deadlock/serialization/unique races return 409 and require reload/retry; runtime
  race behavior remains on the final test backlog.
- Existing `CanOperate` does not check availability; 7B adds that rule locally in
  its authorization guard without changing authorization of unrelated capabilities.
- Mutation request body limit is 1 MiB; finding batch <=100, page size <=100.
  SourceVersion is AI output revision; row/head/result versions are concurrency
  tokens and must not be incremented manually by clients.
- Unknown/confusing raw classes remain visible in original data and class filters.
  Accepted reviewed class must be from the active configured suspected catalogue.
- Plant capture association must be explicit. If current Plant map references no
  longer prove the historical capture relationship, review stays blocked rather
  than guessing a different map or creating a Plant.
- Guidance choice must cite an existing applicable catalogue revision. Retired/
  expired guidance or changed member reviews makes live readiness false even if
  an earlier stored projection was Reviewed.
- Root summary counts current blockers; it is not an approval command. 7C must
  reload/recheck everything, including assignment and effective dates, inside its
  own approval transaction before locking the candidate.
- Shared review tables replace the per-kind table names proposed in the plan;
  kind-specific domain decisions and typed snapshots remain explicit. No independent
  authoritative review cache or duplicate finding aggregate was introduced.

## 5. Handoff to 7C / Final Test

7C can read result ActiveImportBatchId/ReviewRevision, current review heads, immutable
decision snapshots, zone member-review bindings, guidance decisions and live blockers.
It must not trust only `DiseaseZone.HasBlockingItems` or ingestion `BlockingCount`.
No approve/publish/notification/latest-reviewed/Order-or-Mission completion behavior
was added to 7B.

Final Test scenarios remain in implementation plan section 8 and master plan section 27.
They are intentionally unimplemented/unexecuted here. Phase 7B is not marked Done
until that final phase produces acceptance evidence.
