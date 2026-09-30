# BE1 Java Migration — Phase 3 completion report

Date: 2026-09-28  
Plan: `Codex-Plan/BE1-Java-Spring-Boot-Migration-Plan.md`, Migration Phase 3  
Result: **complete; all Phase 3 exit gates are covered by executable tests**

## Delivered runtime

- Immutable HTTP/RabbitMQ/system execution context with tenant, actor,
  correlation, message identity and roles; stable API error and bounded paging
  primitives.
- Transaction-required append-only audit writer carrying before/after/reason
  and execution correlation.
- Transaction-required Outbox enqueue, leased `SKIP LOCKED` dispatcher,
  persistent JSON messages, mandatory routing, correlated publisher confirm,
  exponential bounded retry, dead state and Micrometer counters.
- Inbox transaction coordinator with `(consumer_name, message_id)` dedup,
  manual ACK after commit, retry queue tiers, DLQ and confirmed redrive.
- Strict 4 MiB JSON envelope policy and registry for all 11 canonical BE1/BE2
  event contracts. Values retain nulls, tolerate additive fields and contain no
  CLR/Java type metadata.
- Database idempotency records for HTTP commands, callbacks and events,
  including request-hash conflict detection and response replay.
- Spring Data Redis/Lettuce JSON cache abstraction with service/version prefix,
  TTL, bounded connection/command timeout, invalidation and database fallback.
  Compose isolates Java BE1 and .NET BE2 under `agridrone:be1` and
  `agridrone:be2` keyspaces.
- Flyway `V2__shared_runtime.sql` adds the durable idempotency store.
- Persistence is separated behind `AuditRepository`, `InboxRepository`,
  `OutboxRepository` and `IdempotencyRepository`; PostgreSQL-specific SQL lives
  only in their `Jdbc...Repository` implementations.

## Exit-gate evidence

| Gate | Executable evidence | Result |
|---|---|---|
| Business rollback rolls back audit/outbox | `SharedRuntimeIT.businessRollbackAlsoRollsBackAuditAndOutbox` on PostGIS PostgreSQL | Pass |
| Crash before/after commit does not duplicate work | `SharedRuntimeIT.crashBeforeCommitRetriesAndCrashAfterCommitIsDeduplicated`; `RabbitDeliveryProcessorTest` verifies ACK occurs only after Inbox returns committed | Pass |
| Broker outage cannot lose committed event | `SharedRuntimeIT.brokerOutageKeepsCommittedEventAndSchedulesBoundedRetry` | Pass |
| Publisher confirm and mandatory routing | `RabbitPublisherConfirmIT` on RabbitMQ | Pass |
| C# ↔ Java contracts | Java round-trips all 11 fixtures; .NET 10 `CrossServiceGoldenEventFixtureTests` round-trips the same set | Pass (1 Java test + 12 C# cases) |
| Redis outage preserves correctness | `VersionedJsonCacheTest` forces connection failure and verifies authoritative loader fallback | Pass |
| No .NET binary/type serialization dependency | Golden JSON assertion rejects `$type`, `System.` and `PublicKeyToken`; Redis uses `StringRedisTemplate` JSON | Pass |

## Verification commands

```powershell
cd backend-be1-java
./mvnw.cmd verify

docker run --rm -v "<repository>:/workspace" -w /workspace/backend `
  mcr.microsoft.com/dotnet/sdk:10.0-alpine `
  dotnet test tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj `
  --filter "FullyQualifiedName~CrossServiceGoldenEventFixtureTests"
```

Final Java suite: 18 unit tests and 11 integration tests, zero failures.
The .NET 10 cross-service contract selection ran 12 cases, zero failures.

Messaging is deliberately off by default. Later capability phases enable it
only after declaring queue ownership, preventing Java and C# consumers from
competing for the same queue during migration.
