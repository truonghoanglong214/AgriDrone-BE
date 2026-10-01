# AgriDrone BE1 Java

Spring Boot modular-monolith target for BE1. Migration Phase 3 provides the
shared execution, audit, idempotency, Inbox/Outbox, RabbitMQ and Redis runtime
on top of the Flyway-owned `agridrone_be1` database. Phase 4A adds the Identity
domain, repository output ports under `application.port.out`, and JPA
persistence split into `entity`, `repository` and `adapter`. Phase 4B.6 now
exposes the Identity login and password-reset routes; tenant and manager
workflows remain deferred to later Phase 4 slices.

## Local verification

Prerequisites: Java 21 and Docker.

```powershell
./mvnw.cmd verify
```

The default profile expects PostgreSQL, RabbitMQ and Redis. Tests use the `test` profile; integration tests create isolated PostGIS, RabbitMQ and Redis containers.

## Docker Desktop project

The root `compose.yaml` uses the Compose project name `agridrone`. PostgreSQL,
RabbitMQ, Redis, BE1 Java and BE2 .NET therefore appear under one `agridrone`
group in Docker Desktop. Start only shared infrastructure with:

```powershell
docker compose up -d postgres rabbitmq redis
```

Start the complete stack with `docker compose up -d --build`. Always run these
commands from the repository root and use Compose rather than starting the
services with separate `docker run` commands. Maven integration tests use
short-lived Testcontainers containers; those are independent test resources
and are removed automatically after the test JVM exits.

## Runtime configuration

All credentials come from environment variables. Required Compose variables are documented in the repository `.env.example`. JWT verification is disabled until an explicit `BE1_JWT_ENABLED=true`, `BE1_JWT_JWK_SET_URI`, `BE1_JWT_ISSUER`, and `BE1_JWT_AUDIENCE` are supplied. Tokens must use RS256 and include a `kid` header. The Java issuer/JWKS endpoint is separately enabled with `BE1_JWT_ISSUER_ENABLED=true` and requires PEM RSA key locations, a key id, and a positive access-token TTL. The public JWKS endpoint is `/.well-known/jwks.json`.

Issuer key locations should use Spring resource locations such as
`file:C:/Users/ASUS/.agridrone/jwt-private.pem` and
`file:C:/Users/ASUS/.agridrone/jwt-public.pem`. The private key must be
PKCS#8 PEM (`BEGIN PRIVATE KEY`) and the public key must be X.509 PEM
(`BEGIN PUBLIC KEY`). Local PEM files are ignored by Git.

Login is exposed at `POST /api/auth/login` when the Java issuer is enabled.
Password reset uses `POST /api/auth/forgot-password` and
`POST /api/auth/reset-password`; forgot-password requires SMTP to be enabled
and sends a link using the configured reset URL. All authentication and reset
routes are anonymous endpoints; protected business routes still require a
Bearer token.

Initial System Admin creation is disabled by default. To run it during startup,
set `BE1_SYSTEM_ADMIN_BOOTSTRAP_ENABLED=true`,
`BE1_SYSTEM_ADMIN_EMAIL`, and optionally `BE1_SYSTEM_ADMIN_FULL_NAME`. The
configuration fails startup when enabled with a blank/invalid email or name.
Bootstrap is transactionally idempotent across concurrent BE1 instances.

Flyway is enabled by default and validates/applies migrations from
`src/main/resources/db/migration`. The default local database is
`agridrone_be1`; `ddl-auto` remains `none`, and Flyway clean is disabled.

RabbitMQ publishing uses persistent JSON messages, mandatory routing and
correlated publisher confirms. Consumers use manual acknowledgement and only
ACK after the Inbox/business transaction commits. Messaging remains disabled
until `BE1_MESSAGING_ENABLED=true` and a capability declares its consumer.

Redis values are JSON only. Java uses the versioned
`agridrone:be1:v1:...` keyspace, while Compose configures .NET BE2 under
`agridrone:be2`; cache failures fall back to the authoritative store.

Compose provisions separate `agridrone_be1` and `agridrone_be2` databases with
different owner roles. PostgreSQL initialization scripts only run for a fresh
volume. Because the migration plan explicitly discards existing development
data, reset an old pre-Phase-2 local volume before first startup:

```powershell
docker compose down --volumes
docker compose up --build --wait
```

The first command permanently removes that local Compose database volume. Do
not run it against an environment whose data must be retained.

Health probes:

- `/actuator/health/liveness` checks only the application state.
- `/actuator/health/readiness` requires the application, PostgreSQL, RabbitMQ and Redis to be ready.

Run the full stack from the repository root with `docker compose up --build` after providing the required secrets.

During migration, BE1 Java listens on `http://localhost:8081` and the existing
.NET runtime listens on `http://localhost:8080`. This is deployment coexistence;
only the explicitly enabled Java Identity routes are available and no business
traffic or queue ownership has moved.

Implementation and verification evidence is recorded in the repository-level
`docs/operations` directory, including
`be1-java-migration-phase4b6-report.md` for the login/password-reset slice.
