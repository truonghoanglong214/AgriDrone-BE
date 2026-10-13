# BE1 Step 1E — Business master data report

- Date: 2026-10-09
- Scope: disease/severity/recommendation and Harvest Readiness criteria versioning
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- Preserved the existing fixed health levels (`UNKNOWN`, `HEALTHY`, `MILD`,
  `MODERATE`, `SEVERE`) and versioned `PlantCondition` lifecycle. The selected
  catalogue remains `BROWN_SPOT`, `ANTHRACNOSE`, `SUNBURN` and
  `MECHANICAL_SCAR`; no duplicate catalogue was introduced.
- Preserved `TreatmentRecommendation` as the expert-authored source of truth.
  It maps a condition and health/severity level to versioned guidance,
  disclaimer, expert source, reference, effective window and lifecycle state.
- Added read services that resolve exactly one published recommendation for a
  condition/severity/time and continue to expose retired or superseded versions
  through history. There is no free-form AI treatment input.
- Added the versioned `HarvestReadinessCriterion` aggregate with stable code,
  version lineage, `Plant`/`Farm` granularity, effective windows and
  `Experimental`, `Validated`, `Retired` lifecycle states.
- Validation requires ground-truth protocol, dataset requirements, evaluation
  protocol and evidence reference. No criterion is seeded as validated because
  this checkpoint has no approved validation evidence.
- Kept business criteria separate from BE2-owned AI model and threshold
  configuration. Assessments store criterion ID/code/version/status snapshots
  plus optional AI model/threshold provenance.
- AI jobs store the criterion ID and version as one atomic snapshot pair.
  Composite foreign keys prevent a stored ID from resolving to a different
  version. Retiring a criterion blocks new use while historical references and
  queries remain resolvable.

## Migration

`20261009140000_AddVersionedBusinessMasterData` adds:

- PostgreSQL enums for criterion status and assessment granularity;
- `survey.harvest_readiness_criteria` with lifecycle, evidence, lineage,
  optimistic concurrency, user provenance and non-overlapping effective-window
  constraints;
- nullable expand columns on existing assessments and AI jobs so legacy rows
  remain readable;
- composite criterion-version foreign keys and model/threshold provenance
  foreign keys;
- a rollback guard that refuses destructive rollback after any new provenance
  has been used.

Run
`docs/operations/be1-step1e-business-master-data-preflight.sql` before rollout.
If rollout fails, correct the prerequisite/schema issue and roll forward by
rerunning the same migration. After new criterion/provenance data exists, do not
force a down migration; ship a corrective forward migration instead.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Idempotent SQL generation from
  `20261009120000_AllowClosingReferencedSurveyServicePrices` to
  `20261009140000_AddVersionedBusinessMasterData`: succeeded.
- Unit, migration execution, concurrency and historical-resolution tests remain
  in the unified Final Test Phase and were not run here.

Step 1A–1E is now code-complete, but Step 1 is not `Done` until the Final Test
Phase records the required evidence.
