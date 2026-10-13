# BE1 Step 2C — Existing-tenant/new-farm intake report

Date: 2026-10-09

Status: **CodeComplete — AwaitingFinalTest**

## Implemented behavior

- Added the dedicated `SubmitNewFarmSurveyRequest` command, validator, handler,
  acknowledgement response and API input contract.
- The input contract contains only the selected service and proposed Farm data.
  It does not accept TenantId, UserId, applicant contact or caller scope.
- Added an Identity-owned read port that returns the applicant snapshot only
  when the tenant, user and Owner membership are all active and not deleted.
- The handler derives TenantId and UserId from the execution context and checks
  the active-owner reference before idempotency resolution. A deactivated owner
  therefore cannot submit or replay an existing acknowledgement.
- Applicant name, email and phone are copied from the server-side Identity
  profile. An incomplete or invalid profile is rejected without trusting client
  contact data.
- Added `SurveyRequest.CreateExistingTenantNewFarm`. It stores TenantId and
  RequestedByUserId, always leaves FarmId null, validates the proposed Farm
  fields and does not provision a Farm.
- Caller scope is derived as `tenant:{tenantId}:actor:{actorId}`. The payload
  fingerprint includes the caller context, service and proposed Farm data while
  excluding mutable server-derived contact fields.
- SurveyRequest, PII-redacted user audit and tenant-scoped acknowledgement email
  outbox are persisted in one Surveys transaction.
- Existing unique constraints continue to handle idempotency races and request
  number collisions.

## Deferred boundary

- The HTTP controller, authorization policy attachment and idempotency-header
  mapping remain in Phase 2F.
- Existing-Farm intake remains in Phase 2D; admin review/rejection remains in
  Phase 2E.
- The two required authorization scenarios—tenant A attempting tenant B and a
  just-deactivated membership—remain in the unified Final Test Phase.

## Persistence impact

Step 2C introduces no schema changes and no new migration. It reuses the
nullable TenantId/FarmId model and messaging changes introduced in Step 2B.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes --context
  AgriDroneSchemaDbContext ...`: no pending model changes.
- The installed EF CLI is 8.0.22 while the runtime is 10.0.10; the version
  alignment remains recommended before later migration authoring.
- Tests were neither added nor run, in accordance with the unified Final Test
  Phase in the implementation plan.
