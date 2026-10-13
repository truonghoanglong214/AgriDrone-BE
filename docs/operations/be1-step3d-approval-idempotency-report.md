# BE1 Step 3D — Approval idempotency and concurrency report

- Date: 2026-10-11
- Scope: approval claim/replay, concurrent winner recovery and persistence-error translation
- Status: CodeComplete — AwaitingFinalTest

## Durable idempotency claim

- Added `SurveyApprovalIdempotencyRecord` and migration
  `AddSurveyApprovalIdempotency`.
- A claim is uniquely scoped by `(ActorId, IdempotencyKey)`, while a second unique
  index guarantees only one approval operation for each SurveyRequest.
- The claim stores a SHA-256 fingerprint over the canonical command payload:
  request, selected manager, normalized reason/checklist notes, checklist values
  and expected request version. The key itself is not part of the fingerprint.
- The repository flushes the claim at the new `IdempotencyClaim` failure seam
  before prerequisite resolution or provisioning. It is not independently
  committed: every failure still rolls it back with the complete approval graph.

## Replay behavior

- A retry with the same actor, normalized key, request and command fingerprint
  returns the immutable response snapshot created by the winning transaction.
  Replay is resolved before request-version and request-status validation, so the
  original expected version remains valid for an exact retry.
- Reusing the same actor/key for another request or different payload returns
  `SurveyApproval.IdempotencyConflict`.
- Approving an already claimed request with another actor/key returns
  `SurveyApproval.ConcurrentApproval`.
- The response snapshot preserves the original review, order, Tenant, Farm,
  manager, assignment and invitation identifiers even if mutable operational
  state changes later.

## Concurrent failure recovery

- `SurveyApprovalDbContext` clears tracked state after every commit or rollback,
  allowing the same scoped UoW to open a clean recovery transaction.
- The handler recovers after:
  - EF optimistic-concurrency failures;
  - PostgreSQL serialization failure (`40001`) and deadlock (`40P01`), including
    provider errors wrapped by `DbUpdateException`;
  - unique collisions for approval claim, Request→Order, order number, Tenant
    code, Farm code, active primary assignment and pending Owner invitation.
- Recovery first checks for a committed matching winner and replays it. If none
  exists, it returns the stable error mapped to the violated constraint; unknown
  database failures continue to propagate instead of being mislabeled.

## Schema

The new `survey.survey_approval_idempotency` table contains:

- request and actor identifiers;
- normalized idempotency key and fixed-length SHA-256 command fingerprint;
- JSONB result snapshot;
- claim/completion timestamps with a completion-pair check constraint;
- unique indexes on SurveyRequest and `(ActorId, IdempotencyKey)`;
- restrictive foreign key to `survey.survey_requests`.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet ef migrations has-pending-model-changes ...`: no pending model changes.
- Tests were not added or run, in accordance with the consolidated Final Test
  Phase. The PostgreSQL concurrent/rollback matrix remains a release gate there.

## Next gate — Step 3E

Expose the SystemAdmin-only approval endpoint, map its header/body contract and
response, update OpenAPI and collect compile/contract evidence without bypassing
the application handler.
