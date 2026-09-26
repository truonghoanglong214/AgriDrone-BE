# BE1 Be-Plan Phase 5 — Database Foundation Report

Date: 2026-09-25

Status: COMPLETE

## Scope delivered

Phase 5 adds the target persistence model without exposing or registering any new Survey controller or customer workflow.

- New `AgriDrone.Modules.Surveys` persistence/domain project.
- `survey` schema with service catalogue and effective-dated prices, requests and append-only reviews, orders, appointments, payments/events, audited price adjustments, result headers, and Harvest Readiness assessments.
- Farm-level `farm.farm_base_map_versions` header and nullable linkage from legacy `zone_map_versions`.
- Nullable expand columns on missions for `survey_order_id` and the new `mission_purpose`; legacy `mission_type` remains intact.
- Versioned pre-flight checklist definitions plus per-mission offline/audit snapshots.
- Nullable Survey Order/Result linkage for existing Plant Health scans.
- Farm base-map linkage for mission observations/re-identification.
- Seed data contains only `PLANT_HEALTH` (Active) and `HARVEST_READINESS` (Experimental). Baseline Mapping remains a MissionPurpose and Follow-up remains an order relationship.

## Integrity controls

- Caller-scoped request idempotency and one order per request.
- PostgreSQL exclusion constraint prevents overlapping effective price windows per service.
- Referenced price versions are immutable through a database trigger.
- Composite tenant/farm and farm/service foreign keys prevent cross-boundary references.
- Previous compatible orders are constrained to the same Farm and Service and guarded by a deferred trigger requiring a completed order with a published result.
- Partial unique indexes enforce one active appointment/payment, one published Farm base map, one active checklist definition, and one completed pre-flight snapshot under their respective policies.
- Money columns use `numeric(18,2)`; area uses `numeric(12,4)`; order final price has a database rounding check.
- Mutable workflow headers use PostgreSQL `xmin` optimistic concurrency.

## Migration and recovery

- Migration: `20260925100711_Phase5SurveyDatabaseFoundation`.
- Preflight: `docs/operations/be-plan-phase5-preflight.sql` (read-only).
- Strategy is expand-only for existing operational tables: all new Mission, Plant, Observation, and Zone-map links are nullable.
- Mapping/event V1 and legacy MissionType remain available during the drain window.
- The generated `Down` migration removes Phase 5 foreign keys, columns, tables, enums, and custom guard functions. Production rollback should first stop Phase 5 writers and export any newly created Survey records.

## Verification evidence

- `dotnet build AgriDrone.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet ef migrations has-pending-model-changes ...`: no pending model changes.
- PostgreSQL 17/PostGIS fresh migration: passed.
- Upgrade from `20260925053119_Phase4ArchiveHarvestRuntimeModel`: passed.
- Rollback to the Phase 4 migration followed by re-upgrade to Phase 5: passed.
- `Phase5DatabaseFoundationIntegrationTests`: passed against PostgreSQL and exercised effective-price overlap, referenced-price immutability, cross-tenant FK rejection, active-appointment uniqueness, money precision, and stale-`xmin` rejection.
- No Survey controller/route was added to `AgriDrone.Api`.

## Deferred to owning phases

- Survey catalogue/request application commands and public APIs: Phase 7.
- Approval/onboarding orchestration and order state machine: Phase 8.
- Appointment/payment lifecycle and readiness policy: Phase 9.
- Mission cutover making SurveyOrder/MissionPurpose mandatory and executing pre-flight policy: Phase 10.
- Base-map publication behavior: Phase 11.
- Service-result review/publication behavior: Phase 12.
