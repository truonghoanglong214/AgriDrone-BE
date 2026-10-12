# BE1 Step 4B — Baseline appointment and readiness

- Date: 2026-10-11
- Scope: Baseline Mapping appointment lifecycle and payment-free readiness
- Status: CodeComplete — AwaitingFinalTest

## HTTP and application surface

- `POST /api/system-manager/survey-orders/{orderId}/appointments` proposes or
  re-proposes a `BaselineMapping` appointment for the active assigned manager.
- `POST /api/tenant-owner/survey-orders/{orderId}/appointments/{appointmentId}/confirm`
  confirms the proposed window for an active owner membership derived through
  Order → Tenant.
- `POST /api/tenant-owner/survey-orders/{orderId}/appointments/{appointmentId}/request-reschedule`
  records the reason and reopens the Baseline appointment gate when required.
- `GET /api/system-manager/survey-orders/{orderId}/readiness/baseline-mapping?missionId={missionId}`
  returns the authoritative decision and stable failure codes for the assigned
  manager. The request does not accept boundary, appointment, payment, Drone or
  pre-flight state from the client.
- HTTP controllers only map contracts and dispatch MediatR requests; they do not
  inject feature DbContexts or repositories.

## Atomic appointment boundary

- `SurveyAppointmentSchedulingDbContext` is a serializable transaction boundary
  for SurveyOrder, appointment history, audit and Outbox.
- Manager authorization is re-checked in the transaction against the active Farm,
  primary assignment, active account/profile, `AVAILABLE` state and unexpired
  `QUALIFIED` flight qualification.
- Owner authorization is derived from the locked Order's Tenant and requires an
  active `OWNER` membership plus active user account; Tenant ID is never trusted
  from the body.
- Commands require expected Order/appointment versions. Database row locks,
  optimistic concurrency and the one-current-appointment unique index close the
  stale-write and concurrent-proposal paths.
- Re-proposal changes the `RescheduleRequested` row to `Superseded` and inserts a
  new row with increasing `RevisionNumber` and `PreviousAppointmentId`. The old
  window and reason remain queryable instead of being overwritten.
- Confirming transitions the Order to `BaselineReady`; requesting reschedule from
  that state returns it to `AwaitingBaselineAppointment`.
- Every successful mutation writes audit data and
  `survey.appointment-changed.v1` to Outbox in the same commit.

## Authoritative Baseline readiness

- The query reads the current approved boundary/scope, confirmed Baseline
  appointment, primary manager assignment/availability/qualification, linked
  Mission purpose/status/window/pilot, Drone state and completed pre-flight
  directly from PostgreSQL.
- Mission must belong to the same Order/Tenant/Farm, have purpose
  `BASELINE_MAPPING`, be `SCHEDULED`, use the assigned manager as pilot and fit
  within the confirmed appointment window.
- Readiness requires an available Drone, a completed pre-flight and no recorded
  unsuitable-condition or failsafe safety notes.
- `SurveyOrderReadinessPolicy` produces stable failure codes for missing/stale
  appointment, wrong Mission purpose/window, unavailable manager/Drone, missing
  pre-flight and failed safety checks.
- Baseline Mapping deliberately does not require or inspect payment, pole-count or
  price state. Paid-service rules remain closed for Phase 4D/4E.

## Schema and compatibility

- Migration: `20261011080125_Phase9BaselineAppointmentReadiness`.
- Added the expand-only `SUPERSEDED` appointment status plus revision chain,
  previous-appointment, proposer and superseded timestamp columns.
- Existing rows are deterministically backfilled by Order, purpose, creation time
  and ID before unique revision/chain constraints are added.
- Migration guards reject legacy reschedule reasons that are blank or exceed the
  new 1000-character limit instead of silently truncating data.
- Downgrade removes the new columns, indexes, constraints and foreign keys, but
  intentionally retains the harmless PostgreSQL enum label because enum-label
  removal is unsafe.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- EF pending-model check: no changes since the latest migration.
- Forward and downgrade SQL generation both pass. Forward SQL contains the
  guarded deterministic history backfill; downgrade does not remove the enum
  label.
- Runtime Swagger snapshot: OpenAPI 3.0.4, 78 paths and 94 schemas. All four 4B
  routes are present; `missionId` is required for readiness. Propose requires
  `purpose`, start/end and expected Order version and declares
  `201/400/401/403/404/409` responses.
- Tests were not added or run, per the consolidated Final Test Phase. Stale and
  concurrent proposal, cross-tenant owner, revision preservation, payment-free
  readiness and state-change-before-flight scenarios were added to section 27.2.
- No migration was applied to a live database in this checkpoint; PostgreSQL
  integration and rollback execution remain Final Test Phase evidence.

## Next gate — Phase 4C

Implement the authoritative confirmed pole-count handoff for new Farms and the
current-inventory path for mapped Farms, then freeze the server-resolved per-pole
price snapshot. BE2 prepare/start wiring remains in Step 5C and must transmit
MissionPurpose and re-check this decision immediately before flight.
