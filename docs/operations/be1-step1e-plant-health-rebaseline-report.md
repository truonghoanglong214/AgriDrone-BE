# BE1 Step 1E — Plant Health R2 rebaseline report

- Date: 2026-10-17 (Updated: 2026-11-03)
- Scope: no-severity Plant Health labels and sourced guidance
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- Seeded baseline target set: `BROWN_SPOT`, `ANTHRACNOSE`, `SUNBURN` and
  `MECHANICAL_SCAR`, with dataset, annotation-rule and team-label limitation
  provenance.
- Added a public authenticated Plant Health master-data query that returns only
  active target labels and currently effective sourced guidance. Its result is
  explicitly `PRELIMINARY` and contains no severity field.
- Added SystemAdmin create/history/activate/retire use cases for cited,
  versioned guidance, including effective-window overlap prevention, audit and
  optimistic concurrency.
- Removed the legacy health-level HTTP/query path and stopped registering legacy
  treatment and Harvest Readiness repositories/queries in target runtime DI.
- Added startup/readiness validation for the baseline pilot label set and its
  provenance.

## Migration and compatibility

`20261017154310_Step1PlantHealthMasterData` adds label provenance, guidance to
condition-revision linkage, lifecycle timestamps, indexes, foreign key and check
constraints. It backfills the four existing active condition rows and preserves
legacy sourced-guidance rows with nullable compatibility linkage. Legacy tables
are not destructively removed in this expand phase.

`20261103033709_UnlockPlantHealthConditionCodes` drops the restrictive check
constraint `ck_plant_conditions_health_label_code` to allow dynamic catalog expansion
as defined in ADR-0012.

No fabricated guidance content is seeded. A SystemAdmin must enter and activate
content only after Product has approved its real source/reference.

## Post-Step 1E Evolution: Extensible Conditions (2026-11-03)
- Under ADR-0012, the catalog lock was relaxed to allow adding new diseases (`POST /api/system/plant-conditions`), editing metadata in-place (`PUT /api/system/plant-conditions/{id}`), and versioning (`POST /api/system/plant-conditions/{id}/versions`).
- Startup validation now verifies baseline labels exist without blocking additional active labels.

## Verification

- Solution build: succeeded with 0 warnings and 0 errors.
- Unit tests: 719 passed, 0 failed.
- Architecture tests: 80 passed, 0 failed.
- EF Core pending-model check: up-to-date with `20261103033709_UnlockPlantHealthConditionCodes`.
