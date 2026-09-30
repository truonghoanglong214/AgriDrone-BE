# BE1 Java Migration — Phase 2 completion report

Date: 2026-09-28  
Plan: `Codex-Plan/BE1-Java-Spring-Boot-Migration-Plan.md`, Migration Phase 2  
Result: **complete; Migration Phase 3 may start from this database foundation**

## 1. Fresh Flyway baseline

`backend-be1-java/src/main/resources/db/migration/V1__be1_baseline.sql` is the
sole BE1 schema baseline. It creates the PostGIS/citext/pgcrypto/btree_gist
extensions and the seven owned schemas `identity`, `farm`, `plant`, `survey`,
`notification`, `audit`, and `messaging`.

Final SHA-256: `ab07489b7655c77a7186281ba44dba52437a16a78e06e55dec08ad7d2f4bc0bb`.

The baseline creates 34 application tables matching the Phase 0 ownership
inventory:

| Area | Tables | Notes |
|---|---:|---|
| Identity | 11 | Tenant membership is owner-only; SystemManager assignment history is retained. |
| Farm | 4 | Farm/Zone and Farm/Zone map versions; BE2 mission identifiers are UUID references without cross-database FKs. |
| Plant | 4 | Official Plant identity/change history and business catalogue only. |
| Survey | 11 | Service/price, request/review, order, appointment/payment, result and readiness foundation. |
| Notification | 1 | BE1 notification intent/read state. |
| Audit | 1 | Append-only audit records. |
| Messaging | 2 | Inbox and Outbox persistence foundation. |

The fresh model deliberately contains no FieldTask, legacy Harvest Management,
FarmMembership, ZoneAssignment, Mission/media/telemetry/AI, or raw Plant scan
tables. It also contains no cross-service foreign key.

Flyway is enabled with `validate-on-migrate=true`, `clean-disabled=true`, and
Hibernate schema generation remains disabled.

## 2. Invariants and seed data

Database enforcement includes:

- SRID-typed PostGIS geometry, valid Farm/Zone polygons, zone-within-farm and
  active-zone non-overlap enforcement, plus GiST indexes.
- `numeric(18,2)` money and `numeric(12,4)` area columns, ISO currency checks,
  positive amounts, and a rounded order price snapshot check.
- A GiST exclusion constraint preventing overlapping effective price windows
  for one Survey Service.
- Partial unique indexes for one active TenantOwner, one active primary
  SystemManager per Farm, one published Farm base map, and one confirmed Zone
  map.
- Explicit positive `bigint version` columns on mutable aggregates so later
  repositories can use application-managed optimistic compare-and-swap.
- Append-only audit enforcement and Inbox/Outbox state/lease/retry constraints.

Deterministic seeds are present for `SYSTEM_ADMIN`, `SYSTEM_MANAGER`,
`PLANT_HEALTH`, `HARVEST_READINESS`, and the five required health levels
`UNKNOWN`, `HEALTHY`, `MILD`, `MODERATE`, and `SEVERE`.
The approved revision-1 condition catalogue is seeded with `BROWN_SPOT`,
`ANTHRACNOSE`, `SUNBURN`, and `MECHANICAL_SCAR`.

## 3. Database ownership

Compose now bootstraps two logical databases and distinct runtime owners:

| Service | Database | Owner role |
|---|---|---|
| BE1 Java | `agridrone_be1` | `agridrone_be1` |
| BE2 .NET | `agridrone_be2` | `agridrone_be2` |

Passwords are required environment variables and are not committed. The
bootstrap script revokes database `CONNECT` from `PUBLIC` before granting it to
the matching owner. BE1 schemas/tables/functions/sequences also revoke all
access from `PUBLIC`.

The initialization script runs only when PostgreSQL creates a fresh volume.
The local reset warning and procedure are documented in
`backend-be1-java/README.md`.

## 4. Verification evidence

| Gate | Result |
|---|---|
| `mvnw.cmd -B -ntp test` | PASS — 13 unit/architecture/platform tests. |
| `mvnw.cmd -B -ntp verify` | PASS — 13 tests above plus 6 Testcontainers integration tests. |
| Fresh Flyway migrate | PASS — `V1__be1_baseline.sql` applied to `postgis/postgis:17-3.5` / PostgreSQL 17.5. |
| Flyway validate | PASS — one migration validated after apply. |
| Runtime DB owner | PASS — the baseline was also migrated and validated by a non-superuser owner after bootstrap installed the required extensions. |
| Target/negative inventory | PASS — exactly 34 owned tables; legacy, raw-processing and BE2 schemas/tables absent. |
| Geometry | PASS — outside-Farm and overlapping Zone writes rejected. |
| Money/effective window | PASS — scale metadata is 2; overlapping service-price windows rejected. |
| Assignment/concurrency | PASS — second active primary assignment rejected; stale version update affects zero rows. |
| Schema grants | PASS — unprivileged/cross-service roles cannot write the other owner schema. |
| Compose config | PASS — required variables supplied only to the validation process. |
| Database provisioning smoke test | PASS — fresh container created both databases/owners; own connections succeeded and cross-database connections were denied both ways. |

The provisioning smoke test used a disposable container and removed it after
verification. No repository database volume was reset.

## 5. Phase 2 exit gate

- [x] Fresh Flyway migration passes on real PostgreSQL/PostGIS.
- [x] Flyway validation passes and Hibernate cannot create or mutate schema.
- [x] Geometry, money, effective-window, unique active assignment, and
  optimistic concurrency integration tests pass.
- [x] BE1 and BE2 use separate database owners; cross-database connection is
  denied in both directions.
- [x] Legacy/raw/BE2 runtime tables and cross-service FKs are absent from BE1.

Migration Phase 3 may now implement shared execution context, audit runtime,
Inbox/Outbox dispatch/consumption, stable errors, idempotency, and Redis
abstractions against this schema.
