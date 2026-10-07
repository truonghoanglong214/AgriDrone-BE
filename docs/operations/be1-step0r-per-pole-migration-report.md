# BE1 Step 0R-2 — Per-pole order migration report

- Date: 2026-10-07
- Migration: `20261007120000_RebaselinePerPoleOrder`
- Scope: per-pole catalogue/order snapshots, two-stage order statuses and
  purpose-specific appointments
- Status: Implemented and verified

## Migration behavior

- Keeps Phase 5 migration immutable.
- Adds nullable `PricePerPole` catalogue and order snapshot columns.
- Keeps `PricePerHa` and area snapshots as legacy read-only data; no automatic
  conversion from hectares to poles is performed.
- Renames equivalent legacy statuses:
  - `PENDING_SCOPE_CONFIRMATION` to `PENDING_BOUNDARY_VERIFICATION`;
  - `AWAITING_APPOINTMENT` to `AWAITING_PAID_APPOINTMENT`;
  - `READY_FOR_OPERATIONS` to `READY_FOR_PAID_SERVICE`.
- Adds the five Baseline Mapping and pricing states without rewriting legacy
  order history.
- Backfills existing appointments to `PaidService` and changes active
  appointment uniqueness to `(SurveyOrderId, Purpose)`.
- Adds database checks for positive pole count, exactly one catalogue pricing
  mode, complete immutable pricing snapshots and the per-pole final-price
  formula.
- Adds same-Farm base-map and confirmer foreign keys.

## Rollback policy

Rollback to Phase 5 succeeds while the database contains only compatible
legacy data. It is rejected with SQLSTATE `P0001` after any per-pole snapshot,
Baseline appointment, base-map/count snapshot or new-only intermediate state
has been written. This prevents silent loss or semantic corruption.

## Verification evidence

- Solution build: passed with zero warnings and zero errors.
- EF pending-model check: no model changes since the latest migration.
- Unit tests: 441 passed.
- Architecture tests: 56 passed.
- Step 0R migration integration test: passed on PostgreSQL 17/PostGIS 3.5.
  It covers fresh migration, Phase 5 upgrade with legacy rows, legacy value
  preservation, valid per-pole writes, pricing/appointment constraints, safe
  rollback and guarded rollback.
- Historical Phase 5 fresh/upgrade/rollback test: passed against the explicit
  Phase 5 migration target.
- Existing latest-schema mapping/master-data integration tests: 4 passed.

The temporary PostgreSQL test container was removed after verification.
