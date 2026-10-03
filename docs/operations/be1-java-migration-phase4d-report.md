# BE1 Java Migration Phase 4D Report

Date: 2026-10-02

Phase 4D is complete. BE1 Java now owns the target SystemManager invitation,
profile lifecycle, primary Farm assignment and assigned-Farm access decision
flows. The implementation preserves the frozen C# route and wire-value
contracts while keeping removed self-registration, tenant-staff and farm-worker
surfaces absent.

## Delivered capability

- Anonymous invitation preview and atomic, single-use invitation acceptance.
  Tokens are stored as SHA-256 hashes; a new user and profile are created in the
  same transaction when registration details are required.
- SystemAdmin invitation delivery, direct profile creation, activation,
  suspension, availability and qualification changes with stable errors and
  optimistic version checks.
- Primary Farm assignment, reassignment and ending with append-only history,
  transactional audit records and a database-enforced single active assignment
  per Farm.
- `GET /api/system-manager/farms` and a fail-closed access decision that reloads
  user, profile, qualification, active Farm and assignment state from
  PostgreSQL. Tenant mismatch, inactive users, suspended/unqualified profiles
  and unassigned managers are denied.
- SMTP invitation delivery occurs only after the invitation transaction commits;
  delivery failure does not roll back an already committed invitation.

## Retained routes

- `POST /api/auth/system-manager-invitations/preview`
- `POST /api/auth/system-manager-invitations/accept`
- `POST /api/system/managers`
- `PUT /api/system/managers/{profileId}/activate`
- `PUT /api/system/managers/{profileId}/suspend`
- `PUT /api/system/managers/{profileId}/availability`
- `PUT /api/system/managers/{profileId}/qualification`
- `POST /api/system/managers/invitations`
- `PUT /api/system/farms/{farmId}/primary-manager`
- `PUT /api/system/farms/{farmId}/primary-manager/end`
- `GET /api/system-manager/farms`

## Verification

`./mvnw.cmd -q verify` completed successfully with Docker Desktop available:

- 105 unit, API contract, authorization and ArchUnit tests passed.
- 32 Testcontainers integration tests passed.
- 0 failures, 0 errors and 0 skipped tests.
- Phase 4D contributes six controller/authorization tests and three PostgreSQL
  integration tests covering the full lifecycle, access-denial matrix,
  concurrent assignment and concurrent invitation acceptance.

The complete Phase 4 exit gate is therefore closed. Phase 5 remains the next
migration slice.
