# BE1 Java Migration Phase 4A Report

Date: 2026-09-28

## Result

Phase 4A implementation is complete. It establishes the Identity domain and
persistence boundary needed by later Identity use cases without exposing an HTTP
API or issuing JWTs prematurely. The overall Phase 4 exit gate remains open.

## Delivered

- Plain Java domain models and lifecycle rules for users, tenants, owner-only
  memberships, SystemManager profiles, invitations, password-reset tokens and
  primary Farm assignments.
- Application-owned repository ports under `identity.application.port.out.persistence` for
  all Phase 4A Identity aggregates.
- Spring Data JPA repositories and Hibernate entities under
  `identity.infrastructure.persistence.jpa`, separated into `entity`,
  `repository` and `adapter`; Identity CRUD contains no handwritten SQL.
- Java naming is explicit by responsibility: `UserRepository` is the output
  port, `UserJpaRepository` is the Spring Data interface and
  `UserPersistenceAdapter` implements the port. Java interfaces do not use an
  `I` prefix.
- Application repository ports remain independent from Spring Data and JPA;
  adapters map persistence entities to plain domain models.
- Optimistic version checks for SystemManager profiles and assignment endings.
- PostgreSQL constraints remain the final concurrency guard for one active
  TenantOwner and one active primary SystemManager per Farm.
- Pessimistic JPA locks protect invitation and reset-token single-use transitions;
  Hibernate version columns protect profile and assignment updates.

## Verification

| Check | Result | Evidence |
|---|---|---|
| Java compilation | PASS | `./mvnw.cmd -q -DskipTests compile` |
| Unit and architecture tests | PASS | 27 tests, 0 failures, 0 errors, 0 skipped |
| Identity domain tests | PASS | 7 tests cover defaults, qualification, suspension and optimistic assignment transitions |
| PostgreSQL/JPA repository tests | PASS | 3 tests cover aggregate round trips, uniqueness constraints and single-use token transitions |
| Full Testcontainers integration suite | PASS | 14 tests, 0 failures, 0 errors, 0 skipped across PostgreSQL/PostGIS, Redis and RabbitMQ |

The full `./mvnw.cmd verify` gate ran successfully with Docker. The Identity
repository suite is `IdentityRepositoryIT`; all three cases executed against a
real PostGIS/PostgreSQL Testcontainer.

## Deferred to later Phase 4 slices

- SystemAdmin bootstrap and role seeding.
- Login, password hashing orchestration, JWT/JWKS and BE2 token verification.
- Forgot/reset-password and invitation application services.
- Tenant selection/read APIs and SystemManager/Farm access decisions.
- Retained-route API contract tests and the complete Phase 4 exit gate.
