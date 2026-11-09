# Phase 7A — Plant Health V4 pending ingestion completion report

> Date: 2026-11-09
> Status: `CodeComplete — AwaitingFinalTest`
> Scope: Step 7A implementation, not whole Step 7 or runtime release

## 1. Implemented

Runtime follow-up 2026-11-09: migrations 7A–7E are now applied and local API/consumers
are ready. See [Step 7 closure](be1-step7-implementation-closure-report.md).
Any "not applied/not exercised" statement below describes the original implementation checkpoint.

- Typed `IPlantHealthPendingImport` and `IPlantHealthSourceContextQuery`; BE2 handler
  coordinates Inbox while BE1 import uses the same scoped publication DbContext.
- V4 strict JSON processor, payload validator, integration execution identity and
  single consumer registration. Old contract serialization policy remains intact.
- Immutable import batches, canonical finding revisions, batch links and boundary
  exceptions; one result/Order and explicit active batch/source/map/nature metadata.
- No-severity V4 DiseaseZone path, proposed Plant/candidate memberships, sourced
  guidance suggestions and blocking state; legacy severity constraints remain for
  legacy rows. Raw/unknown/confusing labels are retained separately from target
  suspected class and remain NeedsReview.
- Atomic Inbox/pending/audit/Order InProgress → PendingReview. Permanent validation
  errors occur before business mutations; exceptions after staging roll back.
- Message/Handoff/job/source revision dedup with normalized fingerprints, identity
  conflict detection, source Supersedes and older-output history without activation.
- Source job context verifies purpose/Order/Tenant/Farm/map/provenance/limitation
  and evidence-media ownership. PostGIS validates boundary association and zone
  membership geometry. Finding boundary checks use one set-based query per batch.
- Detector records completed AI jobs and immutable source snapshots, retains old
  observations, uses capture/flight observed time and keeps GPS matches as suggestions.
  Publisher uses real source job/provenance/version; old jobs require reprocess.
- Legacy observation review/resolve rejects V4 jobs so those endpoints cannot bypass
  the pending-result publication gate or create/alter official Plant state from V4.
- ADR 0013, updated handover/specification, six golden envelopes, implementation
  plan/backlog, expand migration and reviewable SQL script.

No approval/publication/latest-reviewed update, owner result portal or Mission
completion was introduced. These remain 7B–7E/BE2 portal responsibilities.

## 2. Verification performed

Commands run from `backend` unless stated otherwise:

| Command/check | Observed result |
|---|---|
| `dotnet build AgriDrone.sln --no-restore --nologo -v minimal` | Succeeded, 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext --no-build` | `No changes have been made to the model since the last migration.` |
| `dotnet ef migrations script UnlockPlantHealthConditionCodes Step7APlantHealthPendingIngestion ... --no-build --output ../docs/operations/phase7a-pending-ingestion.sql` | SQL generated successfully; not executed |
| `git diff --check` at repository root | No whitespace errors |
| Parse six golden JSON files and inspect generated upgrade SQL | All JSON parsed; no DROP TABLE/DROP COLUMN or physical xmin column; source-job unique and Tenant-context FK present |

EF CLI 10.0.10 was restored from the existing local tool manifest. It uses the
Database project as startup because API does not reference EF Design.
Tests were not written/run in this phase, per master plan. Build/model checks do
not establish correctness of PostgreSQL concurrency, rollback or RabbitMQ behavior.
Those acceptance scenarios remain unchecked in the Final Test backlog.

`Codex-Plan/BE2-Use-Case-Specification.md` và tài liệu bàn giao consumer hiện bị
`.gitignore` loại khỏi Git; chúng đã được cập nhật ở workspace. Handoff có thể
review/commit trong repo nằm ở report này, ADR 0013 và `docs/contracts`, không phụ
thuộc việc thêm các tài liệu IDE đang ignored vào Git.

## 3. Migration and preflight

- New migration:
  `20261109040913_Step7APlantHealthPendingIngestion`.
- Reviewable [upgrade SQL](phase7a-pending-ingestion.sql).
- Adds four target tables, nullable source metadata and V4 columns; expands legacy
  nullable fields with explicit legacy/V4 constraints. Does not rewrite old
  migrations or populate fake severity/provenance.
- Adds unique handoff/job/source revision keys and same-context result/batch/finding
  FKs. PostgreSQL xmin is used for optimistic concurrency.
- Generated Down was guarded: populated 7A history/active batch or unresolved
  nullable legacy fields prevents rollback. No Guid.Empty default is used to
  manufacture Plant/health-level identities during rollback.

Preflight queries for the deployment operator (not executed in this session):

```sql
-- The baseline should be applied before this new migration.
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;

-- Legacy data must satisfy the retained legacy constraints.
SELECT count(*) AS legacy_zones_missing_health_level
FROM plant.disease_zones WHERE health_level_id IS NULL;
SELECT count(*) AS legacy_memberships_missing_plant
FROM plant.disease_zone_memberships WHERE plant_id IS NULL;

-- Verify configured labels/provenance before enabling the consumer.
SELECT code, plant_health_label_kind, dataset_version, annotation_rule_version
FROM plant.plant_conditions WHERE is_active AND plant_health_label_kind IS NOT NULL;

-- Verify registry metadata against the actual model/pipeline; do not invent it.
SELECT id, model_name, version, is_active,
       metrics->>'datasetId' AS dataset_id,
       metrics->>'datasetVersion' AS dataset_version,
       metrics->>'annotationRuleVersion' AS annotation_rule_version,
       metrics->>'boundaryPolicyVersion' AS boundary_policy_version,
       metrics->>'teamLabelLimitation' AS team_label_limitation
FROM mission.ai_model_versions WHERE model_type = 'DISEASE_DETECTION' AND is_active;
```

Deploy the expand migration before deploying/enabling the new V4 consumer. For a
populated deployment, prefer roll-forward fixes while keeping source/Inbox history.
Do not run Down to discard pending data. A schema rollback requires explicit data
reconciliation/archive and verification that the guard is satisfied.

## 4. Runtime prerequisites and limitations

- This session did not connect to or update the business database or RabbitMQ.
  Base `appsettings.json` has RabbitMq.Enabled=false, Outbox.Enabled=true,
  prefetch=8 and retry delays 5/30/120 seconds. Deployment overrides were not audited.
- Registry must contain exactly one active DiseaseDetection model matching the
  detector's returned ModelName, with actual dataset/annotation/boundary/limitation
  strings in Metrics. This code does not seed or fabricate training provenance.
- Catalogue dataset/annotation must match the source snapshot; two or three active
  suspected disease labels are required. Confusing labels remain unresolved originals.
- Mission must reference its capture map/boundary with published/approved evidence.
  Capture/media or flight time is required for observed-time provenance.
- Existing legacy jobs have no trustworthy source snapshot; run detection again.
  No automatic backfill claims old model/dataset evidence.
- Source IDs must be stable from the detector for the same observation across
  reprocess. New IDs are new sources, not automatically linked by proximity.
- Plant/candidate association requires an explicit matching map reference. If
  a Plant has since moved map and historical membership cannot be established,
  ingestion rejects the association instead of selecting a convenient current map.
- The detector response currently has no authoritative zone/guidance proposals;
  the producer emits an empty zones collection. The consumer imports non-empty
  V4 proposals from a valid source, retaining unresolved guidance as blocking.
  Previous synthetic clustering guidance IDs are no longer emitted.
- Legacy Manager raw observation views may show historical processing runs now
  that sources are retained. V4 review mutations must use 7B, not legacy actions.
  No owner publication visibility is granted by this implementation.

## 5. Retry, DLQ and replay runbook

1. Inspect consumer `be1-plant-health-analysis-ready-v4`, queue
   `agridrone.be1.plant-health-analysis-ready.v4`, routing `health.analysis-ready.v4`.
   Correlate MessageId, CorrelationId, Tenant, Order, Mission, HandoffId and JobId.
2. Transient DB/dependency failures roll back and use existing bounded transport
   retry delays before DLQ. Invalid JSON/context/provenance/version and locked
   result use permanent error codes, not indefinite retry.
3. For a transient dependency failure that exhausted retry, restore the dependency
   and replay the unchanged envelope with the same IDs. A previously committed
   success returns its stable outcome instead of importing again.
4. A failed Inbox record is terminal for its MessageId. After correcting invalid
   business content, use a new MessageId and new HandoffId, and a new source job/
   version if AI output changed. Keep the old failed record for audit.
5. Do not reuse a successful HandoffId/job/source version with changed content.
   `IdentityPayloadConflict` requires investigating the producer, not deleting
   Inbox or overwriting the stored pending data.
6. Approved/Published results are locked. An ingestion replay cannot reopen or
   replace them; route any such requirement through a separate business workflow.

## 6. Handoff to 7B and Final Test

7B reads ActiveImportBatchId → batch links → canonical findings and proposed zones,
with original evidence/provenance and blocking summary. Review decisions must bind
to expected result/batch/finding versions and keep AI originals unchanged.

All acceptance/negative scenarios remain in
[implementation plan, section 7](../plans/be1-be2-phase7a-implementation-plan.md#7-backlog-final-test-và-điều-kiện-hoàn-thành)
and master plan section 27. This includes PostgreSQL/PostGIS fresh/upgrade, delivery
concurrency, crash-after-commit, partial-write rollback, DLQ/replay, legacy-bypass
negative tests and owner published-only visibility. Phase 7A becomes Done only
after these tests produce evidence in Final Test Phase.
