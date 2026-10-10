# BE1 Step 3B — Atomic persistence boundary report

- Date: 2026-10-10
- Scope: specialized persistence and locking boundary for survey approval
- Status: CodeComplete — AwaitingFinalTest

## Implemented

- Added `SurveyApprovalDbContext`, mapping only the cross-module graph required
  by approval: request/review, service, Tenant/Owner/invitation, Farm/base map,
  SystemManager assignment, SurveyOrder/result references, audit and Outbox.
- Added `SurveyApprovalUnitOfWork` as the application-facing facade. It opens one
  Npgsql `RepeatableRead` transaction and performs one commit. Direct
  `SaveChangesAsync` through the UoW is rejected so application orchestration
  cannot create an independent save boundary. A returned application
  `Result.Failure` also rolls back before it is returned, including data already
  flushed at an earlier checkpoint.
- Added transaction-bound implementations for the Step 3A request repository,
  reference queries, SurveyOrder repository, provisioning port and Outbox port.
  No existing Identity, Farms or Surveys DbContext/UoW is called from this graph.
- Added authoritative row locking:
  - the SurveyRequest aggregate is locked with `FOR UPDATE`;
  - service, TenantOwner membership/Tenant/User, Farm, SystemManager/User,
    current primary assignment, current published base map/source order and the
    selected previous compatible order/result are locked with `FOR SHARE`;
  - every locking query refuses to run outside the active approval transaction.
- New-customer provisioning reuses the existing Tenant, Farm, primary assignment
  and Tenant invitation domain factories. The Owner invitation token remains only
  in the email Outbox payload and is never returned by the port.
- Registered the entire approval persistence graph as scoped services and enabled
  the Step 3A prerequisite resolver only after all same-context implementations
  became available.

## Rollback seams

`ISurveyApprovalFailureInjector` is invoked after real database flushes and before
commit at these checkpoints:

1. Tenant
2. Farm
3. Primary manager assignment
4. Owner invitation
5. Outbox

The production implementation is a no-op and can be replaced by the PostgreSQL
integration fixture in the Final Test Phase. An exception from any checkpoint
escapes the operation, and the UoW rolls the entire transaction back explicitly.
This includes entities flushed by earlier checkpoints.

## Constraint review

No migration was required. The current model already contains the retry/concurrency
constraints needed by the boundary:

- `uq_survey_orders_request`
- `uq_survey_orders_number`
- `uq_farm_manager_assignments_active_farm`
- `ux_tenants_code_active`
- `ux_farms_tenant_code_active`
- `uq_tenant_invitations_pending_owner_provisioning`

Database exception classification and replay behavior remain Phase 3D work.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Tests were not added or run, in accordance with the consolidated Final Test
  Phase. ADR-0006 still requires the PostgreSQL rollback matrix for all five
  checkpoints before the approve endpoint can be exposed.

## Next gate — Step 3C

Implement the three request-kind orchestration paths inside this transaction.
They must complete all prerequisite validation before staging side effects, use
the specialized repositories/ports only, and leave API exposure for Step 3E.
