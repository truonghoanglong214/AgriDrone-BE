# BE1 Step 2D — Existing-Farm survey intake report

Date: 2026-10-10

Status: **CodeComplete — AwaitingFinalTest**

## Implemented behavior

- Added the dedicated `SubmitExistingFarmSurveyRequest` command, validator,
  handler, acknowledgement response and API input contract.
- FarmId belongs to the route/application command. The body contract contains
  only the selected service, optional estimated pole count, preferred window
  and notes; it does not accept TenantId, UserId or Farm snapshot data.
- Added a Farms-owned read port that resolves FarmId to its authoritative
  TenantId, lifecycle state, name, address, area and center point.
- Missing, inactive and cross-tenant Farms return the same non-enumerating
  not-found response. The active TenantOwner membership and owner profile are
  revalidated after the authoritative Farm-to-Tenant resolution.
- An active Farm must have a valid address, positive numeric(12,4) area and
  SRID 4326 center point before it can be snapshotted into a SurveyRequest.
- Added `SurveyRequest.CreateExistingFarmSurvey`. It requires non-empty
  TenantId, FarmId and RequestedByUserId and stores server-derived applicant and
  Farm snapshots without creating or modifying a Farm.
- Service applicability at intake requires an Active or Experimental service
  with an effective per-pole price.
- Idempotency continues to use the server-derived tenant/actor caller scope.
  Existing-Farm fingerprints include FarmId and client-controlled request data,
  but exclude mutable server-derived owner and Farm snapshots.
- Authorization and active Farm state are checked before idempotent replay.
- SurveyRequest, PII-redacted farm-scoped user audit and tenant-scoped
  acknowledgement email outbox are persisted in one Surveys transaction.
- Intake does not select a previous compatible order; that remains part of the
  Step 3 approval transaction.

## Deferred boundary

- The HTTP controller, authorization policy attachment, route-to-command mapping
  and idempotency-header mapping remain in Phase 2F.
- Admin inbox/detail/start-review/rejection remain in Phase 2E.
- ID-enumeration, cross-tenant, inactive-Farm and idempotency concurrency
  scenarios remain in the unified Final Test Phase.

## Persistence impact

Step 2D introduces no schema changes and no new migration.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes --context
  AgriDroneSchemaDbContext ...`: no pending model changes.
- `git diff --check`: succeeded.
- The installed EF CLI is 8.0.22 while the runtime is 10.0.10; version alignment
  remains recommended before later migration authoring.
- Tests were neither added nor run, in accordance with the unified Final Test
  Phase in the implementation plan.
