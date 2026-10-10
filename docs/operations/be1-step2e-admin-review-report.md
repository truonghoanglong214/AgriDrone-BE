# BE1 Step 2E — SystemAdmin request review report

Date: 2026-10-10

Status: **CodeComplete — AwaitingFinalTest**

## Implemented read use cases

- Added `GetSurveyRequestInbox` with optional status, kind, service and UTC
  created-time filters, page-size protection and deterministic
  `CreatedAt DESC, Id DESC` ordering.
- Inbox responses expose only the applicant name required for triage; email and
  phone remain in detail to reduce unnecessary PII exposure.
- Added `GetSurveyRequestDetail` with the complete request/service snapshot,
  applicant contact, Farm inputs, estimated pole count, preferred window and
  append-only review history.
- The submitted point is returned as `CandidateLocation` with
  `IsApprovedFarmBoundary = false`; it is never represented as an approved Farm
  boundary.
- Detail publishes checklist definition `survey-request-review.v1` and its six
  review items so clients do not invent checklist versions.

## Implemented mutation use cases

- Added `StartSurveyRequestReview` with expected-version validation and the sole
  `Submitted -> UnderReview` transition.
- Added `RejectSurveyRequest` with required reason, expected version and a
  strongly structured checklist snapshot.
- The checklist captures contact validity, dragon-fruit Farm confirmation,
  service-area support, location sufficiency, preliminary legal/flight
  feasibility and eligible SystemManager availability.
- Reject is allowed only from `UnderReview`, appends a new immutable
  `SurveyRequestReview`, and transitions the request to `Rejected`.
- Review collection mutation is now private to the aggregate; no update/delete
  path exists for historical decisions.
- Domain-version conflicts and database concurrency conflicts return the stable
  SurveyRequest version-conflict response.

## Transaction, audit and notification

- Start-review persists its transition and PII-redacted SystemAdmin audit in one
  Surveys transaction.
- Reject persists the transition, review snapshot, PII-redacted SystemAdmin
  audit and rejection-email outbox in one Surveys transaction.
- Added the `survey-request-rejected` email template. It supports both
  tenant-scoped requests and platform-scoped NewCustomer requests.
- Audit snapshots contain only operational IDs, status, kind, service, version
  and checklist version; applicant contact, free-form reason and checklist notes
  are excluded.

## Deferred boundary

- HTTP controllers, SystemAdmin role-policy attachment, route mapping and
  OpenAPI exposure remain in Phase 2F. Application handlers require an
  authenticated execution context; the HTTP role guarantee is attached at that
  boundary following the existing catalogue pattern.
- Approve is not exposed and no approval orchestration is implemented in Step 2.
  The shared checklist definition is ready for the Step 3 approval command.
- Test scenarios remain in the unified Final Test Phase.

## Persistence impact

Step 2E introduces no schema changes and no new migration. Checklist version is
stored inside the existing JSONB review snapshot.

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
