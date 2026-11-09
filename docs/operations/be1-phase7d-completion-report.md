# Phase 7D — Plant Health atomic publication completion report

Date: 2026-11-09
Status: `CodeComplete — AwaitingFinalTest`

## Implemented

Runtime follow-up 2026-11-09: migrations 7A–7E are now applied and local API/consumers
are ready. See [Step 7 closure](be1-step7-implementation-closure-report.md).
Any "not applied/not exercised" statement below describes the original implementation checkpoint.

- Typed manager POST health-results/publish, strict DTO and shared authorization.
- Exact approval/head/version/fingerprint matching and current source, evidence,
  geometry, catalogue, membership and guidance validation under transaction locks.
- Immutable publication payload/history with published reviewed findings, approved
  zones and cited guidance snapshots. Existing immutable approval/review history
  retains all internal decisions/evidence.
- LatestReviewedPlantHealth per Plant, retaining multiple accepted findings.
  Monotonic Farm publication version under advisory lock; observed time takes
  precedence, so late older results remain historical without replacing newer state.
- One publication UoW save/commit for result Published, zones Published, Order
  Completed, history/latest, audit, actor-scoped idempotency receipt and two Outbox
  events. Mission is unchanged by this request.
- Replaceable per-Plant staging checkpoint for Final Test failure injection; no
  runtime fault endpoint/configuration.
- Expand migration with same-context FKs, unique result/Farm-version keys,
  latest xmin and ordering/value guards, immutable publication/result/zone/membership
  guards and deferred publication/Completed-Order consistency.
- Extend the existing V4 DiseaseZone lifecycle constraint to accept Published only
  with complete reviewed/guidance/publication metadata. Earlier 7A–7C migration
  files are unchanged; the new 7D migration restores the earlier constraint on Down.
- No Plant.HealthLevel mutation, HEALTHY inference, owner controller, direct broker
  publication or delivered notification claim. Zero accepted findings preserve
  earlier latest-reviewed rows and retain the approved preliminary conclusion.

Details: [ADR 0016](../adr/0016-plant-health-atomic-publication.md),
[API/event handoff](../contracts/plant-health-result-publication.md).

## Verification

No test files were written and no tests were run, per the user's instruction.
All runtime/security/rollback/contract/PostGIS/concurrency/E2E acceptance work remains
in Final Test. Production builds target API/dependencies, excluding test projects.

Commands run from backend:

| Check | Result |
|---|---|
| `dotnet build src/AgriDrone.Api/AgriDrone.Api.csproj --no-restore --nologo -v minimal` | Succeeded, 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext --no-build` | No pending model changes |
| `dotnet ef migrations script Step7CPlantHealthResultApproval Step7DPlantHealthAtomicPublication ... --no-build --output ../docs/operations/phase7d-atomic-publication.sql` | SQL generated, not executed |
| `git diff --check` | Passed; existing LF/CRLF notice only |

These are compilation/schema-alignment checks, not runtime acceptance evidence.
No migration/database update, business HTTP request or broker action is executed.

## Deployment and recovery

New migration: `20261109061911_Step7DPlantHealthAtomicPublication`.
Reviewable [upgrade SQL](phase7d-atomic-publication.sql).
Apply 7A → 7B → 7C → 7D before enabling the combined build. Existing V4 Published rows
must be explicitly reconciled first; migration preflight rejects them rather than
generating invented history. Legacy null-nature rows remain compatible.

Down rejects any publication/latest evidence or V4 Published result. Prefer
roll-forward fixes; never remove published history to make Down succeed.
The existing Outbox dispatcher must run for event delivery. Event size remains
bounded by the existing 4 MiB serializer limit; oversize publication rolls back.

## Remaining 7E / BE2 handoff

- Wire and validate/deduplicate the new preliminary outcome on BE2 before completing
  Mission. This phase provides the producer contract/Outbox, not that consumer.
- Consume notification intent, resolve owner recipients, deduplicate and deliver
  after publication. Consumer arrival order must not expose pending data.
- Build the published-only owner read model/portal under BE2 ownership.
- Implement previous-compatible completed/published Plant Health comparison and
  record compatibility reason, as scoped in 7E.
- Supply Final Test evidence before changing Step 7 from CodeComplete to Done.

No commit, deployment or database migration application is performed here.
