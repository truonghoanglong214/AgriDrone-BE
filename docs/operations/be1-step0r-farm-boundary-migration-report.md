# BE1 Step 0R-3 — FarmBoundary migration report

- Date: 2026-10-07
- Migration: `20261007140000_AddVersionedFarmBoundaries`
- Scope: versioned FarmBoundary, BoundaryException review and Zone spatial invariants
- Status: Implemented and verified

## Domain and persistence behavior

- Adds immutable-version `FarmBoundary` lifecycle: `Draft → Approved | Rejected`
  and `Approved → Superseded`.
- Enforces at most one current approved boundary per Farm.
- Adds reviewer, reason, UTC timestamps, provenance and optimistic concurrency.
- Adds `BoundaryException` with `OutOfBoundary`, `NeedsReview` and `Resolved`
  states; decisions are `AcceptedInside`, `RejectedOutside` or
  `LocationCorrected`.
- Preserves the original WGS84 position. A corrected position is stored
  separately and is required only for `LocationCorrected`.
- Stores measured distance, threshold and policy version rather than embedding
  a numeric near-boundary threshold in handlers.
- Requires valid non-empty Polygon/Point geometries using SRID 4326.
- Requires every active Zone to have a polygon covered by the approved
  FarmBoundary and prevents overlapping active Zones; touching edges remain
  valid.
- Adds same-tenant/Farm foreign keys from orders and exceptions to the exact
  FarmBoundary version.

## Legacy transition and rollback

- Keeps `farm.farms.boundary` unchanged as compatibility data.
- Backfills each valid legacy Farm polygon as version 1, source `LegacyImport`,
  status `Draft`; it is deliberately not auto-approved.
- Upgrade fails explicitly if an old order already contains an unresolvable
  boundary-version identifier or a legacy geometry is invalid.
- Rollback to 0R-2 is allowed while only untouched legacy-import drafts exist.
- Rollback is rejected with SQLSTATE `P0001` after an approved/rejected/new
  boundary, an order reference or any BoundaryException exists.

## Verification evidence

- Solution and integration-test project builds: zero warnings and zero errors.
- EF pending-model check: no model changes since the latest migration.
- Unit tests: 447 passed, including 6 FarmBoundary/BoundaryException tests.
- Architecture tests: 56 passed.
- 0R-3 PostgreSQL 17/PostGIS 3.5 migration test: passed. It covers fresh
  migration, 0R-2 upgrade, legacy Draft backfill, PostGIS/uniqueness/history
  guards, safe rollback and guarded rollback.
- 0R-2 migration regression test: passed against its explicit migration target.
- Existing latest-schema mapping/master-data integration tests: 4 passed.

The temporary PostgreSQL test container was removed after verification.
