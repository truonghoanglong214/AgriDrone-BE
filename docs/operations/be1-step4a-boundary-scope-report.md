# BE1 Step 4A — FarmBoundary and SurveyOrder scope verification

- Date: 2026-10-11
- Scope: assigned-manager boundary work context and atomic verification
- Status: CodeComplete — AwaitingFinalTest

## HTTP and application surface

- `GET /api/system-manager/survey-orders/{orderId}/boundary-scope` returns the
  SurveyOrder version, current approved FarmBoundary and active Zone scope only
  after the SystemManager access service confirms the active primary assignment.
- `POST /api/system-manager/survey-orders/{orderId}/boundary-scope/verify`
  accepts a WGS84 GeoJSON Polygon, reason, optional evidence reference, expected
  Order version and expected current boundary version number.
- The controller only maps HTTP contracts and dispatches MediatR requests. It
  does not inject a DbContext or repository.

## Atomic business boundary

- `SurveyBoundaryVerificationDbContext` is the single commit boundary for the
  new approved FarmBoundary version, optional supersession of the current
  boundary, SurveyOrder transition, audit and Outbox message.
- The transaction locks the SurveyOrder and re-checks the Farm, active primary
  manager assignment, active/qualified manager profile and actor account.
- `FarmBoundaryApprovalService` enforces SRID/valid polygon, active Zone boundary
  presence, containment and non-overlap before approval.
- FarmBoundary now records `SYSTEM_MANAGER` as a truthful source and persists an
  optional `evidence_reference`; applicant geometry is not silently promoted.
- `RequiresBaselineMapping` is re-derived from the current published Farm base
  map plus its source Order's confirmed pole count. A published map without an
  authoritative count fails with a stable conflict instead of opening a gate.
- Unmapped Farms transition to `AWAITING_BASELINE_APPOINTMENT`; mapped Farms with
  authoritative inventory transition to `AWAITING_PRICING` and snapshot the
  current map/count source on the Order.
- The same commit emits `survey.order-boundary-verified.v1` with identifiers and
  stable wire tokens only.

## Schema and compatibility

- Migration: `20261011041401_Phase9BoundaryVerification`.
- Added enum label: `SYSTEM_MANAGER` to `system.farm_boundary_source`.
- Added nullable `farm.farm_boundaries.evidence_reference varchar(1000)`.
- The migration is expand-only for the PostgreSQL enum. Downgrade drops the
  evidence column but intentionally retains the harmless enum label because
  PostgreSQL cannot remove enum labels safely.
- Existing FarmBoundary rows and V1/V2/V3 integration contracts remain valid.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- EF pending-model check: no changes since the latest migration.
- Forward and downgrade SQL generation both pass; downgrade no longer attempts
  the unsupported removal of `SYSTEM_MANAGER` from the PostgreSQL enum.
- Runtime Swagger: OpenAPI 3.0.4, 74 paths, 88 schemas; both boundary-scope
  routes present. Verify request requires `boundary`, `reason` and
  `expectedOrderVersion`; responses are `200/400/401/403/404/409/422`.
- Tests were not added or run, per the consolidated Final Test Phase. PostgreSQL
  locking/rollback, PostGIS Zone geometry, assignment revocation and concurrent
  boundary replacement scenarios remain in section 27.

## Next gate — Phase 4B

Implement purpose-specific Baseline Mapping appointment lifecycle and the
payment-free Baseline readiness path without weakening manager, schedule,
pre-flight or safety checks.
