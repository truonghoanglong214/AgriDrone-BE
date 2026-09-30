# BE1 Java Migration Phase 4B.4 Report

Date: 2026-09-30

## Result

Phase 4B.4 is complete. BE1 Java can create the initial System Admin exactly
once at application startup when bootstrap is explicitly enabled. Configuration
is validated before startup completes, and PostgreSQL-backed tests cover retry
and concurrent-instance behavior. The overall Phase 4 exit gate remains open.

## Delivered

- Transactional System Admin bootstrap orchestration that normalizes the
  configured identity, resolves the seeded `SystemAdmin` role, hashes a random
  bootstrap credential with BCrypt and persists the user-role assignment.
- A database pessimistic lock on `system-admin-bootstrap`, shared by all BE1
  instances, serializes the existence check and creation transaction.
- Idempotent retry behavior: once an active System Admin exists, later startup
  attempts return a no-op result without creating another account.
- Stable failures for a missing seeded role and for a configured email already
  owned by another user. Bootstrap never promotes an existing account.
- Startup configuration under `agridrone.identity.system-admin-bootstrap` with
  conditional email/name validation and an `ApplicationRunner` that invokes
  the use case only when explicitly enabled.
- Compose and `.env.example` variables for enabling bootstrap and supplying the
  initial administrator identity. The feature remains disabled by default.
- Unit tests for orchestration, validation and startup invocation, plus real
  PostgreSQL integration tests for repeated and simultaneous bootstrap calls.

The generated bootstrap credential is not logged or exposed. Credential
delivery and the user-facing forgot/reset-password flow remain separate Phase
4 work; an operator must not enable bootstrap in an environment without the
approved credential-establishment procedure for that environment.

## Verification

| Check | Result | Evidence |
|---|---|---|
| Java compilation | PASS | `./mvnw.cmd -q -DskipTests compile` |
| Bootstrap unit/configuration tests | PASS | Creation, no-op retry, missing role, occupied email and fail-fast property validation |
| PostgreSQL bootstrap integration | PASS | Repeated calls create one user; two concurrent transactions report exactly one creation |
| Full Maven verification gate | PASS | `./mvnw.cmd -q verify`, exit code 0 |
| Full Java test suite | PASS | 42 unit tests and 20 integration tests; 0 failures, 0 errors, 0 skipped |

The integration cases run against a real PostGIS/PostgreSQL Testcontainer and
therefore exercise the same row-lock semantics used by deployed instances.

## Deferred to later Phase 4 slices

- RSA JWT issuing, JWKS publication and BE2 verification (4B.5).
- Forgot/reset-password application flows and the complete Phase 4B exit gate
  (4B.6).
- Identity HTTP APIs, authorization decisions and retained-route parity in
  subsequent Phase 4 slices.
