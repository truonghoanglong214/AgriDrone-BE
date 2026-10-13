# BE1 Step 3A — Approval prerequisites report

- Date: 2026-10-10
- Scope: Step 3A contracts and application-level prerequisite resolution
- Status: CodeComplete — AwaitingFinalTest

## Implemented

- Added the approval-only, row-locking `ISurveyApprovalRequestRepository`
  contract. Its implementation must use the Step 3B specialized approval
  DbContext rather than `SurveysDbContext`.
- Added `ISurveyApprovalReferenceQueries` and immutable reference snapshots for:
  service lifecycle, active TenantOwner, Farm state, SystemManager eligibility,
  active primary assignment, current published base map/confirmed inventory and
  previous compatible order.
- Locked reference-query semantics:
  - mutable authorization and eligibility rows remain protected until the
    approval transaction ends;
  - previous compatible order means the same Tenant, Farm and SurveyService,
    an order in `Completed`, and a result in `Published`;
  - selection orders by `PublishedAt DESC, OrderId DESC`;
  - baseline is required when either the current published Farm base map or a
    positive authoritative confirmed active-pole count is absent.
- Added the approval-scoped provisioning port. Its implementation may only stage
  Tenant, Farm, primary assignment and Owner invitation data in the specialized
  DbContext. It must not open or commit an Identity/Farms transaction.
- Added the approval Outbox port and SurveyOrder repository contract for the same
  transaction boundary.
- Added stable prerequisite/provisioning/concurrency errors, canonical database
  constraint names for Step 3B/3D exception mapping, and a kind-aware prerequisite
  resolver:
  - `NewCustomer` requires a selected assignable manager and always requires
    Baseline Mapping;
  - `ExistingTenantNewFarm` revalidates the active TenantOwner, requires a
    selected assignable manager and always requires Baseline Mapping;
  - `ExistingFarmSurvey` revalidates owner/Farm/current primary assignment,
    derives baseline state and resolves the previous compatible order;
  - approval cannot silently reassign an existing Farm. A supplied manager ID is
    treated as an expected-current-manager guard and must match the current
    primary assignment.
- Added server-side generators whose output respects current database lengths:
  Tenant code <= 50, Farm code <= 30 and SurveyOrder number <= 40.

## Transaction and DI boundary

`SurveyApprovalPrerequisiteResolver` is intentionally not registered yet.
Step 3B must first implement and register `ISurveyApprovalReferenceQueries`,
`ISurveyApprovalRequestRepository`, `ISurveyOrderRepository`,
`ISurveyApprovalProvisioningPort`, `ISurveyApprovalOutbox` and
`ISurveyApprovalUnitOfWork` against the same `SurveyApprovalDbContext` instance.
Registering the resolver before those implementations would make runtime DI
validation fail or tempt the approval flow to read through independent module
DbContexts.

Existing `TenantProvisioningPort`, `FarmProvisioningPort` and
`TenantInvitationService` are not valid Step 3 implementations because they own
independent SaveChanges/transaction boundaries. Step 3B must not delegate to
them from inside approval.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Tests were not added or run, in accordance with the consolidated Final Test
  Phase. ADR-0006 still requires the Step 3 PostgreSQL rollback scenario before
  the approve handler is exposed; Step 3B/3E must preserve that gate.

## Next gate — Step 3B

Step 3B must map only the required approval tables, implement the contracts
above on one Npgsql connection/transaction, add controlled failure injection
after actual database flush points, and register all approval services as one
scoped graph. It must not register or expose the approve HTTP action yet.
