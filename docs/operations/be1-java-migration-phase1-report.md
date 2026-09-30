# BE1 Java Migration — Phase 1 completion report

Date: 2026-09-28  
Plan: `Codex-Plan/BE1-Java-Spring-Boot-Migration-Plan.md`, Migration Phase 1  
Result: **complete; Migration Phase 2 may start from this foundation**

## 1. Delivered foundation

- Added `backend-be1-java` as a Java 21 / Spring Boot 3.5.16 modular-monolith skeleton.
- Added Maven Wrapper 3.9.12 and pinned build dependencies through the Spring Boot parent plus an explicit ArchUnit version.
- Added `test` and `compose` profiles and a multi-stage, non-root JRE container image.
- Added package boundaries for `identity`, `farm`, `plant`, and `survey`, each split into `domain`, `application`, `infrastructure`, and `api`.
- Added PostgreSQL/PostGIS, Hibernate Spatial, Flyway, RabbitMQ, Redis/Lettuce, and Testcontainers dependencies. Flyway is intentionally disabled until Phase 2 owns a fresh BE1 schema.
- Added a UTC `Clock`, UTC Jackson/JPA configuration, graceful shutdown, structured JSON logging, validated correlation IDs, validation mapping, and a stable error envelope.
- Added closed-by-default Spring Security. JWT/JWKS verification is activated only when `BE1_JWT_ENABLED=true`; health probes remain public.
- Added architecture rules that keep domain code framework-free, prevent API/application access to infrastructure, isolate shared code and module adapters, reject cycles, and constrain controllers to API packages.
- Extended Compose with independently built `be1-java` and `be2-dotnet` services. Java is exposed on port `8081` and receives no production-like traffic switch in this phase.

No Java business controller, message producer/consumer, Flyway migration, or legacy `410` compatibility handler was added.

## 2. Health policy

| Probe | Policy |
|---|---|
| `/actuator/health/liveness` | JVM/application availability only; external dependency failures must not restart a live process. |
| `/actuator/health/readiness` | Application readiness plus PostgreSQL, RabbitMQ, and Redis. |
| BE2 `/health/ready` | Existing .NET readiness policy, used by its Compose healthcheck. |

Failure drill evidence:

| State | BE1 liveness | BE1 readiness |
|---|---:|---:|
| All dependencies healthy | `200` | `200` |
| Redis stopped | `200` | `503` |
| Redis restored | `200` | `200` |

## 3. Security and configuration gates

- Runtime passwords and the transitional BE2 JWT secret are required through environment variables; Compose no longer supplies known development-password fallbacks.
- `.env.example` contains placeholders only and keeps BE1/BE2 ports separate.
- Java exception logging records only a server-normalized UUID correlation ID and the exception type. It does not log request bodies, rejected field values, authorization headers, tokens, or credentials.
- Invalid caller-supplied correlation IDs are replaced instead of being copied into logs.
- Validation responses omit rejected values to avoid reflecting secrets or PII.
- The local test used an ephemeral BE2 JWT value supplied only to the process environment; it was not written to the repository.

## 4. Verification evidence

| Gate | Result |
|---|---|
| `mvnw.cmd -B -ntp test` | PASS — 13 tests: 6 architecture, 3 platform, 4 auth/error/correlation |
| `mvnw.cmd -B -ntp verify` | PASS — the 13 tests above plus 1 Testcontainers integration test |
| Testcontainers PostGIS | PASS — connected and executed `PostGIS_Version()` |
| Testcontainers RabbitMQ | PASS — opened a real AMQP connection |
| Testcontainers Redis | PASS — real server returned `PONG` |
| `docker compose build be1-java be2-dotnet` | PASS — both multi-stage images built from clean container stages |
| `docker compose config --quiet` | PASS |
| `docker compose up --detach --no-build --wait` | PASS — PostgreSQL, RabbitMQ, Redis, BE1 Java, and BE2 .NET all healthy |
| BE2 `/health/ready` | PASS — `200` |
| Dependency failure/recovery drill | PASS — `200/503/200` behavior shown above |
| Graceful shutdown | PASS — Tomcat completed graceful shutdown before JPA/Hikari closed |

## 5. Phase 1 exit gate

- [x] Maven Wrapper build/test succeeds locally and in the clean container build.
- [x] Liveness/readiness reflect the dependency policy.
- [x] New tracked runtime configuration contains no hard-coded secret and Java logs do not emit PII/token values.
- [x] Java starts alongside BE2 .NET in Compose.

## 6. Deliberately deferred

Migration Phase 2 owns `V1__be1_baseline.sql`, the fresh logical BE1 database/schema, database roles/grants, and Flyway activation. Until that gate is implemented, Java connects read-only in intent to the transitional PostgreSQL instance only for readiness and owns no table. Business runtime, messaging semantics, audit, Inbox/Outbox, and JWT issuance remain later migration phases.
