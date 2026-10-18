# BE1 Step 2 — R2 Survey Request rebaseline report

- Date: 2026-10-18
- Scope: intake, idempotency, review checklist and contract/privacy boundary
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- All three intake flows accept only the MVP Plant Health service and preserve
  the no-provisioning-before-approval boundary.
- NewCustomer may submit a normalized candidate Tenant code. The request-level
  unique reservation does not create a Tenant; actual provisioning remains an
  atomic Step 3 approval side effect.
- Public idempotency uses a protected server-issued anonymous session; tenant
  flows derive caller scope from authenticated tenant and actor IDs. Canonical
  payload fingerprints distinguish replay from key reuse with changed data.
- Existing-Farm intake resolves Farm ownership and active state on the server;
  TenantOwner request bodies cannot choose Tenant, user or Farm identity.
- SystemAdmin review uses expected-version concurrency. Rejection appends an
  immutable checklist snapshot and commits transition, redacted audit and
  notification outbox atomically.

## Checklist R2

The current version is `survey-request-review.v5` and contains exactly:

1. contact validity;
2. dragon-fruit Farm confirmation;
3. supported service area;
4. sufficient candidate location, explicitly not an approved Farm boundary;
5. written data-use consent feasibility;
6. flight-permission feasibility;
7. active, available and flight-qualified manager availability.

Consent and flight-permission answers are preliminary feasibility checks only.
They cannot create or verify an authorization requirement, and verbal/message
evidence is not mapped to verified authorization.

## Privacy and API contract

- Acknowledgement and inbox responses do not expose applicant name, email,
  phone, Farm address or notes.
- Full applicant/contact data remains available only from the SystemAdmin detail
  route.
- Public, TenantOwner and SystemAdmin controllers retain their explicit
  anonymous/TenantOwner/SystemAdmin authorization boundaries.
- Runtime OpenAPI was regenerated with the v5 checklist fields. The aggregated
  snapshot also contains the Step 3 approve route because later phases are
  already present in the current codebase.

## Verification

- Solution build: succeeded with 0 warnings and 0 errors.
- Unit tests: 576 passed, 0 failed.
- Architecture tests: 89 passed, 0 failed.
- No schema change was required; checklist history remains versioned JSON and
  old snapshots remain readable.

Concurrent PostgreSQL idempotency, transaction/outbox integration and E2E tests
remain required in the Final Test Phase before Step 2 can be marked `Done`.
