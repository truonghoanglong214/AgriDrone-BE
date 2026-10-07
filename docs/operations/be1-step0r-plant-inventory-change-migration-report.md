# BE1 Step 0R-5 — Plant inventory change migration report

- Date: 2026-10-07
- Migration: `20261007180000_AddPlantInventoryChangeReports`
- Scope: owner-reported removal, replacement and new-plant verification lifecycle
- Status: Implemented — awaiting Final Test Phase

## Domain and persistence behavior

- Adds `PlantInventoryChangeReport` with `Removed`, `Replaced` and `NewPlant`
  kinds and the lifecycle `Submitted → UnderReview → Verified → Applied`, with
  `Rejected` and `Withdrawn` terminal paths.
- Requires a new-plant report to pass through `AwaitingSurveyEvidence` and retain
  the later SurveyOrder, Mission, candidate, FarmBoundary and optional base-map
  provenance before it can become `Verified`.
- Keeps owner report evidence, manager decisions, later-survey evidence and the
  application decision as separate immutable snapshots on the aggregate.
- Does not create, retire or replace a `Plant` directly. `MarkApplied` only records
  the identifiers committed by the mapping/base-map amendment transaction.
- Enforces caller-scoped idempotency and at most one open report for the same
  existing Plant or normalized pole location.
- Enforces replacement identity: the resulting Plant must differ from the old
  Plant; removal cannot have a resulting Plant; replacement/new-plant must have one.
- Adds stable `PlantInventoryChange.*` domain error codes and optimistic version
  checks.

## Migration and rollback policy

- Adds PostgreSQL enums `plant_inventory_change_kind` and
  `plant_inventory_change_status`.
- Adds `plant.plant_inventory_change_reports` with WGS84 geometry, JSON evidence,
  lifecycle checks, cross-module provenance foreign keys and partial unique indexes
  for open reports.
- Rollback succeeds while the table is empty and is rejected with SQLSTATE `P0001`
  after any report data has been written.

## Verification evidence

- Full solution build: passed with zero warnings and zero errors before migration
  generation.
- The EF migration, designer and model snapshot were generated from
  `AgriDroneSchemaDbContext`.
- Tests and PostgreSQL migration execution remain intentionally deferred to the
  Final Test Phase defined by the unified implementation plan.
