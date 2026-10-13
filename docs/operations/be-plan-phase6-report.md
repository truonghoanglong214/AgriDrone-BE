# BE1 Be-Plan Phase 6 — Reusable Core and Boundary Report

Date: 2026-09-25

Status: COMPLETE

## Scope delivered

Phase 6 refactors retained Tenant, Farm, Mission, Mapping, Plant, and Survey foundations into explicit internal seams without publishing a new Survey workflow.

- Added `ITenantProvisioningPort`, `ITenantOwnerInvitationPort`, and `IFarmProvisioningPort`. Existing command handlers now reuse these ports rather than duplicating provisioning logic, and future orchestration does not need controller-to-controller HTTP calls.
- Added one authoritative Farm geometry policy for create/update paths: WGS84 SRID 4326, non-empty valid polygons, valid/covered center point, Zone containment, active-Zone non-overlap, and Farm/Zone area rules.
- Changed active Farm/Zone mutation handlers and route policies to assigned `SystemManager`; the decision is resolved by `ISystemManagerAccessService` from active profile, qualification, availability, and primary Farm assignment, independently of TenantMembership/FarmMembership.
- Added a server-owned `SurveyOrderReadinessPolicy` covering scope, confirmed appointment, confirmed payment, qualified primary manager, price adjustment, and terminal-order gates.
- Added deterministic Survey execution primitives: ISO 4217 currency normalization, decimal money rounding per ADR-0003, caller-scoped request idempotency, and provider/reference/event payment deduplication. Time continues to use the repository-wide injectable `.NET TimeProvider` abstraction.
- Kept Plant scans free of SourceTask/FieldTask and added immutable attachment to SurveyOrder/SurveyResult. The human-review lifecycle is owned by `SurveyResult` and manager approval.
- Added `DroneMission.CreateForSurvey` with SurveyOrder/Farm/Tenant/purpose context. It creates Draft only and does not expose prepare/start routes.
- Added `IFarmBaseMapPublicationService` and order/farm/version context for preparing a Draft base-map header. Existing V1 mapping publication remains unchanged.

## Boundary and route controls

- No Survey controller or business route was added.
- Existing legacy endpoint gates remain in place; direct Farm restore is also closed as outside the MVP contract.
- Provisioning seams operate in process and expose no HTTP dependency.
- Core Farms, Missions, Plants, and Surveys assemblies do not reference the removed FieldTasks or Harvests modules.
- No entity or repository was copied into Surveys; cross-module preparation is represented by explicit ports/context objects.

## Verification evidence

- `dotnet build backend/AgriDrone.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test backend/AgriDrone.sln --no-restore --verbosity minimal`: passed.
  - Unit: 377 passed.
  - Integration: 11 passed.
  - Architecture: 42 passed.
- `dotnet ef migrations has-pending-model-changes --context AgriDroneSchemaDbContext ...`: no pending model changes.
- `Phase6BoundaryTests` verifies explicit provisioning ports, order-aware Mission/mapping seams, SystemManager route policy, removed-module dependencies, and absence of Survey controllers.
- `Phase6DomainSeamsTests` and `SurveyPoliciesTests` cover Mission/BaseMap preparation, Plant/SurveyResult review ownership, readiness, money rounding, currency, and idempotency.

## Deferred to owning phases

- Survey Service/Request repositories, handlers, and public submission/review APIs: Phase 7.
- Atomic approval/onboarding transaction and order state machine: Phase 8.
- Appointment/payment handlers and readiness transitions: Phase 9.
- Mission prepare/start APIs and mandatory SurveyOrder cutover: Phase 10.
- V2 Farm base-map producer/publication transaction: Phase 11.
- Full result review/publication workflows: Phase 12.
