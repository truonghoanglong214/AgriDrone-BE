# BE1 Step 1E — Plant Health R2 rebaseline report

- Date: 2026-10-17
- Scope: no-severity Plant Health labels and sourced guidance
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- Locked the target set to `BROWN_SPOT`, `ANTHRACNOSE`, `SUNBURN` and
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
- Added startup/readiness validation for the exact active pilot label set and its
  provenance.

## Migration and compatibility

`20261017154310_Step1PlantHealthMasterData` adds label provenance, guidance to
condition-revision linkage, lifecycle timestamps, indexes, foreign key and check
constraints. It backfills the four existing active condition rows and preserves
legacy sourced-guidance rows with nullable compatibility linkage. Legacy tables
are not destructively removed in this expand phase.

No fabricated guidance content is seeded. A SystemAdmin must enter and activate
content only after Product has approved its real source/reference.

## Verification

- Solution build: succeeded with 0 warnings and 0 errors.
- Unit tests: 572 passed, 0 failed.
- Architecture tests: 80 passed, 0 failed.
- EF Core pending-model check: no changes since the latest migration.
- Runtime OpenAPI snapshot regenerated; new Plant Health routes are present and
  `/api/catalog/health-levels` is absent.

PostgreSQL migration execution and integration/E2E tests remain required in the
Final Test Phase before Step 1 can be marked `Done`.
