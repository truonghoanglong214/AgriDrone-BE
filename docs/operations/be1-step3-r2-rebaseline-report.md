# BE1 Step 3 — R2 approval rebaseline report

- Date: 2026-10-18
- Scope: conditional Order initialization, previous-order compatibility and
  capture-authorization requirements
- Status: **CodeComplete — AwaitingFinalTest**

## Conditional Order initialization

`SurveyOrder.Create` now derives the initial state only from the authoritative
baseline requirement resolved inside the approval transaction:

- no current published base map or no confirmed active-pole inventory:
  `RequiresBaselineMapping = true` and
  `AwaitingBaselineAppointment`;
- current published base map plus confirmed active-pole inventory:
  `RequiresBaselineMapping = false` and
  `PendingBoundaryVerification`.

NewCustomer and ExistingTenantNewFarm are new Farms, so they always use the
first branch. ExistingFarmSurvey uses the locked current Farm baseline query.
The legacy `PendingBoundaryVerification → AwaitingBaselineAppointment`
transition remains readable for existing Orders, while a newly created unmapped
Order no longer needs that artificial transition.

## Versioned previous-order compatibility

The reference query now returns the latest same-Tenant/Farm/service
completed-and-published candidate, not a value falsely named "compatible".
Policy `plant-health-order-compatibility.v1` then fails closed unless all of the
following are true:

1. current and previous Orders reference the same published Farm base-map ID,
   which is the Plant identity boundary;
2. result provenance declares label dataset
   `AGRIDRONE_PILOT_DATASET_V1`;
3. result provenance declares annotation rules
   `AGRIDRONE_ANNOTATION_RULES_V1`.

Missing current inventory/provenance, changed map identity or changed semantics
leaves `PreviousCompatibleOrderId` null. Every new Order persists the policy version
and a stable decision reason such as `COMPATIBLE`,
`NO_PREVIOUS_PUBLISHED_ORDER`, `PLANT_IDENTITY_MISMATCH` or
`CLASS_SEMANTICS_UNAVAILABLE`. The same decision is included in approval audit
and the additive approval API response fields.

Migration `20261018014010_Step3OrderCompatibilityPolicy` adds the nullable
policy-version/reason columns for backward compatibility with historical rows,
plus a check constraint requiring the pair to be both absent or both nonblank.
All new domain writes require both values.

## Pending capture authorization

Approval stages exactly two `SurveyCaptureAuthorization` rows for every new
Order in the specialized `SurveyApprovalDbContext` transaction:

- `WrittenDataUseConsent`;
- `FlightPermission`.

Both start at `Pending` with no evidence, verifier or verification timestamp.
The existing unique `(SurveyOrderId, Type)` index and idempotent approval replay
prevent duplicate requirements. Step 2 feasibility answers are not reused as
verification evidence and do not open the flight gate.

## API and verification

- Runtime OpenAPI was regenerated from
  `GET /swagger/v1/swagger.json`; approval response contains
  `previousOrderCompatibilityPolicyVersion` and
  `previousOrderCompatibilityReason`.
- Solution build: 0 warnings, 0 errors.
- Unit tests: 588 passed, 0 failed.
- Architecture tests: 90 passed, 0 failed.
- EF model check: no pending model changes after the Step 3 migration.

The local PostgreSQL endpoint `localhost:5433` was unavailable, so migration
execution, atomic rollback/failure injection, concurrent approval and HTTP E2E
remain mandatory Final Test Phase gates before Step 3 can be marked `Done`.
