# BE1 Java Migration — Phase 0 completion report

- Completed: 2026-09-28
- Plan: `Codex-Plan/BE1-Java-Spring-Boot-Migration-Plan.md`, Migration Phase 0
- Git baseline commit: `22a06f9eb83f58a58a7891efab4dbceeddf9d30d`
- Scope: freeze active BE1 C#, capture HTTP/database/event/test contracts, identify target ownership and lock Java/deployment names
- Result: **complete; Migration Phase 1 may start from the artifacts listed below**

The baseline includes the Core Step 1A Survey runtime work present when the snapshot was taken. Phase 0 does not claim that Survey public catalogue API is complete: the OpenAPI snapshot contains no Survey business endpoint.

## 1. Frozen names and ownership

| Item | Locked value |
|---|---|
| Java repository directory | `backend-be1-java` |
| Maven group/artifact | `com.agridrone` / `agridrone-be1` |
| Java base package | `com.agridrone.be1` |
| Docker Compose service | `be1-java` |
| Container image | `agridrone/be1-java` |
| BE1 logical database | `agridrone_be1` |
| Java-owned schemas | `identity`, `farm`, `plant`, `survey`, `notification`, `audit`, `messaging` |
| .NET deployment after split | `be2-dotnet` |
| BE2 logical database | `agridrone_be2` |

The Java target is one Spring Boot modular monolith. Package boundaries are `shared`, `identity`, `farm`, `plant`, `survey` and `integration`; no BE1 microservice split is introduced during migration.

The C# freeze and exception process is recorded in `be1-csharp-migration-freeze.md`. There is no active migration exception at Phase 0 close.

## 2. HTTP baseline and endpoint ownership

`docs/openapi/be1-java-migration-phase0.openapi.json` was fetched from the running .NET API against a freshly migrated PostgreSQL/PostGIS database.

| Evidence | Value |
|---|---|
| HTTP response | `200 OK` from `/swagger/v1/swagger.json` |
| Operations | 63 |
| Bytes | 98,013 |
| SHA-256 | `d334e10a3360f82328826afe074071771d12c5cf707fa6ce40a16b1f59eefdc5` |

`be1-java-migration-phase0-endpoints.md` assigns every operation to `BE1_PORT`, `BE1_REAUTHORIZE`, `BE2_KEEP`, `SPLIT_OPS`, `LEGACY_GONE` or `BE2_REDESIGN`, and records its current actor/policy. It also locks the negative inventory so legacy TenantAdmin/Member, Farm Manager/Worker, direct Farm create/restore, direct Mission mutations, ownership transfer, FieldTask and Harvest Management are not accidentally copied into Java.

## 3. Database inventory and disposition

`be1-java-migration-phase0-inventory.sql` is a read-only inventory query over a fully migrated fresh database. It emits every application table, constraint and index with a disposition and rationale. The captured run returned:

| Object type | Count |
|---|---:|
| Tables | 68 |
| Indexes | 397 |
| Primary/unique/foreign/check/exclusion/constraint-trigger constraints | 409 |
| Total | 874 |

The sorted CSV result of the query has SHA-256 `a64b6925eccb9a55ef65ac2bbe7c5482489ab786ac715348a0d477a7d2939db5`. It identified 35 cross-service foreign keys that must not exist after the database split.

Table decisions:

| Disposition | Count | Tables |
|---|---:|---|
| `BE1_KEEP` | 33 | `farm.farms`, `farm.farm_zones`, both Farm/Zone map-version tables; Identity users/roles/user_roles/tenants/invitations/password reset/SystemManager profile+invitation/primary assignment/initialization lock; official Plant, Plant change events, conditions, health levels; all 11 Survey tables; notification intent; audit/inbox/outbox |
| `BE1_REDESIGN_OWNER_ONLY` | 1 | `identity.tenant_memberships` — retain only TenantOwner selection/ownership semantics; remove TenantAdmin/Member behavior |
| `BE2_KEEP` | 15 | current `mission` and `media` operational tables excluding the legacy ownership bridge |
| `MOVE_RAW_FLOW_TO_BE2` | 6 | Plant scan/media/verification and raw condition detection/lesion/review tables; BE1 replaces these with official Survey result association |
| `DROP_LEGACY` | 12 | four FieldTask tables, five legacy Harvest Management tables, Farm memberships, Zone assignments and `mission.drone_legacy_tenant_ownership` |
| `REPLACE_BY_FLYWAY` | 1 | `system.__ef_migrations_history` |

PostGIS extension-owned `spatial_ref_sys`, `tiger` and `topology` objects are intentionally excluded from application ownership inventory. Flyway Phase 2 must recreate target invariants from the keep/redesign decisions, not replay the mixed EF chain.

## 4. RabbitMQ golden contracts

Eleven canonical complete-envelope fixtures now live in `contracts/examples/events`. They use deterministic UUIDs and timestamps and are read from disk by `CrossServiceGoldenEventFixtureTests`; expectations are not regenerated from DTOs.

| Event | Version | Direction / owner | Runtime state at baseline |
|---|---:|---|---|
| `mapping.candidates-approved.v1` | 1 | BE2 → BE1 | compatibility runtime |
| `mapping.zone-map-published.v1` | 1 | BE1 → BE2 | compatibility runtime |
| `identity.tenant-invitation-email-requested.v1` | 1 | BE1 Identity → email worker | runtime |
| `notification.email-requested.v1` | 1 | BE1 producer → notification delivery | runtime |
| `health.observations-ready.v1` | 1 | BE2 → BE1 | compatibility runtime |
| `health.review-state-changed.v1` | 1 | BE1 → BE2 | compatibility runtime |
| `mapping.baseline-candidates-approved.v2` | 2 | BE2 → BE1 | target contract locked; not yet cut over |
| `mapping.farm-base-map-published.v2` | 2 | BE1 → BE2 | target contract locked; not yet cut over |
| `health.observations-ready.v2` | 2 | BE2 → BE1 | target contract locked; not yet cut over |
| `harvest-readiness.assessments-ready.v2` | 2 | BE2 → BE1 | target contract locked; not yet cut over |
| `survey.result-review-state-changed.v2` | 2 | BE1 → BE2 | target contract locked; not yet cut over |

The test suite has explicit BE1-consumer, BE2-consumer and BE1-notification cases and also asserts that fixture event types exactly equal the registered descriptor set. V1 fixtures are immutable; V2 evolution requires a new event type/version.

## 5. Test parity baseline

`BE1-Current-Flow-Parity-Matrix.md` maps every accepted capability to current C# boundary, Java target, contract fixture, authorization cases, database/retry/concurrency invariant, C# evidence, replacement test and cutover state. Its second table accounts for every baseline test class and distinguishes Java ports, Java absence tests and BE2-only tests.

Verification used .NET SDK 10.0.401 and real Compose services `postgis/postgis:17-3.5`, `rabbitmq:4.2.7-management-alpine` and `redis:8.8.1-alpine`:

| Suite | Baseline result | Phase 0 guard result |
|---|---:|---:|
| Unit | 424 passed | 436 passed after adding 12 golden-fixture cases |
| Architecture | 59 passed | 59 passed |
| Integration | 12 passed | 12 passed |

The PostgreSQL host port was changed from `55432` to `56432` in `compose.step1-test.yaml` and all integration connection strings because Windows reserves `55345–55444` on this machine. This is test-infrastructure correction only; container port `5432` and database semantics are unchanged.

## 6. Phase 0 exit gate

- [x] Endpoint inventory exists and every endpoint has an owner/disposition.
- [x] Table/constraint/index inventory exists and every returned object inherits an explicit keep/move/drop/replace decision; cross-service FKs are called out separately.
- [x] Golden contract fixtures cover every registered event and are exercised as BE1 and BE2 consumer inputs.
- [x] BE1 C# is frozen; the exception log has no active new-feature exception.
- [x] Java repository/module, package, image, Compose service and logical database names are locked.
- [x] 424 unit, 59 architecture and 12 infrastructure integration baseline cases pass; new golden guards also pass.

Migration Phase 1 may scaffold `backend-be1-java`. Phase 1 must not add business scope, expose Survey endpoints, copy a `LEGACY_GONE` handler, or start from the mixed EF schema.
