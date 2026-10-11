# BE1 Step 3C — Request-kind approval orchestration report

- Date: 2026-10-10
- Scope: application orchestration for all three survey request kinds
- Status: CodeComplete — AwaitingFinalTest

## Common approval flow

- Added `ApproveSurveyRequestCommand`, validator, response and handler. The
  command carries request ID, optional selected SystemManager profile, complete
  checklist/reason, expected request version and an idempotency key reserved for
  Phase 3D replay handling.
- The handler executes the complete workflow inside the specialized
  `ISurveyApprovalUnitOfWork` transaction. It reads and row-locks the request,
  checks the expected version, validates the approval checklist, revalidates all
  authoritative references, dispatches by request kind, records the immutable
  approved review, transitions the request, stages a redacted SystemAdmin audit
  and writes the approval event to the same Outbox.
- Added `survey.request-approved.v1`. Its payload contains only durable business
  identifiers and approval state; it does not expose applicant PII, the review
  reason, checklist notes or an invitation token.
- Changed `SurveyRequest.Approve` to return the created review, matching
  `Reject`, so the handler can correlate audit/outbox data with the immutable
  approval decision without querying it back.

## Request-kind side effects

### NewCustomer

1. Provision a Tenant named from the applicant snapshot.
2. Provision a Farm in that Tenant from the request snapshot; the point remains
   a center/candidate location and no approved FarmBoundary is created.
3. Create a SurveyOrder and a primary SystemManager assignment.
4. Create an Owner-provisioning invitation through the approval port. No User,
   password or active Owner membership is created.

### ExistingTenantNewFarm

1. Revalidate that the requester remains the active Owner of the request Tenant.
2. Provision only a Farm in that Tenant.
3. Create a SurveyOrder and primary SystemManager assignment.
4. Do not create a Tenant, User, membership or invitation.

### ExistingFarmSurvey

1. Revalidate the active Farm, Owner, current primary assignment and assigned
   SystemManager. An optional selected manager is only an expected-current guard;
   approval cannot silently reassign the Farm.
2. Create no Tenant, Farm, assignment or invitation.
3. Create the order with `RequiresBaselineMapping` derived from the current
   published base map and confirmed inventory, plus the latest compatible
   completed/published order for the same Tenant, Farm and service when present.

## Order and transaction invariants

- Every order starts in `PendingBoundaryVerification`.
- Approval does not set a confirmed pole count, service-price ID, price snapshot,
  currency or final price.
- New Farms always require Baseline Mapping. Existing Farms use the authoritative
  baseline resolver from Step 3A.
- A failed strategy returns `Result.Failure`; the Step 3B UoW rolls back all
  earlier flush checkpoints. Audit, invitation email Outbox and approval Outbox
  are committed only with the request review/status and order.
- The response exposes resource IDs and state but no invitation token or secret.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Tests were not added or run, in accordance with the consolidated Final Test
  Phase in the implementation plan.

## Deferred gates

- Phase 3D: claim/replay semantics for the approval idempotency key, concurrent
  winner recovery and PostgreSQL unique/concurrency exception translation.
- Phase 3E: SystemAdmin HTTP authorization, API contract/action, OpenAPI and
  endpoint-level evidence.
