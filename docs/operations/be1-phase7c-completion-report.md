# Phase 7C — Plant Health result approval completion report

> Date: 2026-11-09
> Status: `CodeComplete — AwaitingFinalTest`
> Scope: approve reviewed preliminary candidate; publication remains 7D–7E

## Implemented

Runtime follow-up 2026-11-09: migrations 7A–7E are now applied and local API/consumers
are ready. See [Step 7 closure](be1-step7-implementation-closure-report.md).
Any "not applied/not exercised" statement below describes the original implementation checkpoint.

- Manager-only POST health-results/approve and GET health-results/approval through
  the existing typed application service. Strict request DTO and server-side identity.
- Reauthorize active/available primary Manager and lock Order/result with the same
  discipline used by 7A/7B. Require matching active batch, result xmin and review revision.
- Recompute all blockers and revalidate source context/provenance, complete immutable
  handoff item set, current reviewed members/guidance, capture geometry and evidence.
- Lock review heads, zone/Plant associations, catalogue/guidance, Mission/job,
  capture map/boundary and evidence rows during approval. PostGIS geometry validation
  uses bulk point/zone queries; temporal guidance validity is checked again before save.
- Require actual job input media and positive TotalImagesScanned, including for
  zero-finding candidates. Internal source context reads those manifests without
  changing V4 wire/routing. Missing legacy manifests require reprocess.
- Freeze exact required review IDs/decisions/head versions/value fingerprints and
  source/provenance/evidence into approval snapshot. Align result Provenance with
  the active approved revision while retaining import history.
- Atomic Approved transition + reviewed metadata + approval fingerprint + audit +
  idempotency receipt. Generic Approve cannot bypass V4 candidate approval.
- Empty/all-rejected/non-disease candidates use NO_ACCEPTED_SUSPECTED_FINDINGS,
  never HEALTHY. Manager approval reason and real source-analysis evidence remain required.
- Immutable approval DB guard, same-context approved batch FK, complete metadata
  checks and populated-evidence Down guard in a new migration.

Result becomes Approved; Order stays PendingReview. No finding/zone publication,
latest-reviewed state, owner visibility, notification/Outbox outcome or Mission
completion is introduced by 7C.

Details: [ADR 0015](../adr/0015-plant-health-result-approval.md),
[API contract](../contracts/plant-health-result-approval.md).

## Verification — no tests

No test files were written and no tests were run. All acceptance/security/contract/
concurrency/rollback/PostGIS/E2E work remains in the final phase, per user instructions.
Production build targets API/dependencies and excludes test projects.

Commands run from backend:

| Check | Result |
|---|---|
| `dotnet build src/AgriDrone.Api/AgriDrone.Api.csproj --no-restore --nologo -v minimal` | Succeeded, 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext --no-build` | No pending model changes |
| `dotnet ef migrations script Step7BPlantHealthHumanReview Step7CPlantHealthResultApproval ... --no-build --output ../docs/operations/phase7c-result-approval.sql` | SQL generated, not executed |

Build/model alignment is not runtime approval evidence. Database migrations were
not applied, and HTTP/broker/database business flows were not exercised.

## Migration/deployment/recovery

New migration: `20261109054451_Step7CPlantHealthResultApproval`.
Reviewable [upgrade SQL](phase7c-result-approval.sql).

- Adds nullable approval snapshot/fingerprint/batch/revision fields, context FK and
  state-dependent checks. Legacy null-nature rows remain supported; V4 Approved/
  Published rows require complete approval evidence bound to their active revision.
- Trigger freezes approval metadata and source context after approval while allowing
  later publication fields/status to advance. Approved evidence cannot be deleted.
- Down refuses to discard populated approval evidence. Prefer roll-forward fixes.
- Apply schema in order 7A → 7B → 7C before deploying/enabling this combined build.
  No historical migration was edited and no fake approval/provenance backfill was added.
- Preflight any existing V4 Approved/Published rows without real snapshots. They
  require explicit reconciliation/review; do not manufacture evidence to pass constraints.
- Verify source job manifests retain registered MediaIds and actual positive
  TotalImagesScanned; older incomplete jobs need reprocess. Keep source/evidence
  records needed by the retained approval snapshot.

## Handoff to 7D and deferred checks

7D reads ApprovedBatchId, ApprovedReviewRevision, ApprovalSnapshot and its fingerprint,
then fetches the frozen immutable reviews. It must reauthorize/recheck active
dependencies and actual catalogue/guidance time windows before atomic publication.
Approval remains preliminary and is not owner-visible by itself.

If a dependency expires after approval, publication must fail closed. This phase
does not implement automatic reopen or mutate an existing approval snapshot.
Any such revision workflow must preserve previous approval evidence explicitly.

Deferred Final Test cases are recorded in master plan section 27: incomplete/stale
review/source/provenance rejection, same-actor operation replay/conflict, concurrent
review/reprocess/revocation/approval, atomic rollback/receipt/audit, zero findings vs
zero images, immutable snapshot and fresh/upgrade/Down migration guards. 7C is not
marked Done until that phase supplies actual acceptance evidence.
