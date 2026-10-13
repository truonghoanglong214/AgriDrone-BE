# BE1 Step 2A — Survey Request foundation report

- Date: 2026-10-09
- Scope: repository, read models, request number, idempotency and common response
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- Finalized `RequestIdempotency` at 100 characters for both the normalized
  caller scope and key, matching the existing PostgreSQL column limits.
- Added the request aggregate repository with a tracked mutation load, an
  untracked caller-scope/idempotency lookup and add support. Mutation loads
  include immutable review history.
- Added separate read models for the SystemAdmin inbox/detail and TenantOwner
  history. Inbox supports status, kind, service and UTC date filters with
  bounded paging and deterministic `CreatedAt`, `Id` ordering.
- Added a server-only request-number generator using a time-ordered UUID v7.
  The generated `SR-...` value is 35 characters and remains protected by the
  existing unique request-number index.
- Added a shared acknowledgement response containing only request ID, request
  number, kind, status and creation time.
- Added a canonical submission snapshot and SHA-256 fingerprint resolver.
  The resolver distinguishes a new submission, a safe replay of the existing
  acknowledgement and reuse of the same key with a different payload.
- Added stable application errors for idempotency mismatch, request-number
  conflict, non-reviewable state and scoped visibility/authorization.
- Registered the repository, queries, number generator and idempotency resolver
  in the Surveys module dependency-injection boundary.

## Persistence and race boundary

No migration was required. The existing model already enforces:

- `uq_survey_requests_caller_idempotency` on caller scope and key;
- `uq_survey_requests_number` on the server-generated request number;
- `character varying(100)` for caller scope and idempotency key.

Each submit handler in phases 2B–2D must use the resolver before insert and
classify the named unique constraint after a concurrent insert. It must then
resolve again: return the stored acknowledgement for an equivalent payload or
return `SurveyRequest.IdempotencyPayloadMismatch` for a different payload.
This preserves the database as the final authority for races.

## Deliberately deferred

- Aggregate creation factories and the three submit handlers belong to phases
  2B–2D.
- Admin start-review/reject handlers and their audit/outbox work belong to 2E.
- HTTP contracts, controllers, PII response shaping and OpenAPI belong to 2F.
- No Tenant, Farm, assignment, invitation or SurveyOrder is provisioned in 2A.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes`: no model changes since the
  latest migration, so no migration was generated. The command also reported
  that the available EF CLI is 8.0.22 while the runtime is 10.0.10; this did
  not affect the result but should be aligned before authoring a later migration.
- Tests were neither added nor run, in accordance with the unified Final Test
  Phase defined by the implementation plan.
