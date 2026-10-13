# BE1 Step 0R-4 — Disease Zone and recommendation migration report

- Date: 2026-10-07
- Migration: `20261007160000_AddDiseaseZonesAndRecommendations`
- Scope: versioned DiseaseZone proposals, membership history and expert-validated TreatmentRecommendation catalogue
- Status: Implemented and verified

## Domain and persistence behavior

- Adds the DiseaseZone lifecycle `Proposed → Reviewed → Published`, with
  `Rejected` and `Superseded` terminal/history paths.
- Keeps the original AI polygon, membership snapshot, candidate references and
  evidence immutable. Manager-reviewed geometry, membership version, reviewer,
  reason and evidence are stored separately.
- Stores exact SurveyOrder, SurveyResult, FarmBoundary and Farm base-map
  versions used by each proposal.
- Requires valid Polygon geometry using SRID 4326.
- A published zone must be covered by its referenced approved or historical
  superseded FarmBoundary and every current member must be an active Plant
  covered by the reviewed polygon.
- Adds immutable/versioned TreatmentRecommendation catalogue entries by
  condition and severity, including effective window, expert/source provenance
  and advisory disclaimer.
- Review must select one published recommendation applicable at review time or
  explicitly reject all candidates. Retired/superseded catalogue versions remain
  resolvable for historical decisions.
- Adds optimistic concurrency, repository ports, EF mappings and DI wiring for
  both aggregates.

## Migration and rollback policy

- Adds `plant.disease_zones`, `plant.disease_zone_memberships` and
  `plant.treatment_recommendations`; no legacy backfill is required because
  these concepts did not previously have authoritative tables.
- Database triggers protect proposal facts, review decisions, membership
  snapshots and catalogue content from in-place history rewrites.
- Rollback to 0R-3 succeeds while the new tables are empty.
- Rollback is rejected with SQLSTATE `P0001` after any DiseaseZone or
  TreatmentRecommendation data has been written.

## Verification evidence

- Solution/integration builds: zero warnings and zero errors.
- EF pending-model check: no model changes since the latest migration.
- Unit tests: 454 passed, including 7 DiseaseZone/recommendation domain tests.
- Architecture tests: 56 passed.
- 0R-4 PostgreSQL 17/PostGIS 3.5 migration test: passed. It covers fresh
  migration, 0R-3 upgrade, empty safe rollback, guarded rollback, lifecycle,
  spatial membership and immutable-history constraints.
- 0R-3 and 0R-2 migration regression tests: passed.
- Existing latest-schema mapping/master-data integration tests: 4 passed.
