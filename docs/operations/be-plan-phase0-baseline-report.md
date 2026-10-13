# Be-Plan Phase 0 Baseline Report

- Status: **Passed**
- Captured: 2026-09-22 (Asia/Bangkok)
- Source commit before Phase 0 changes: `8a878706dedec1b617696af0f1f21707fb7d359a`
- Runtime: Windows, .NET SDK 10.0.401, .NET runtime 10.0.12, Docker Engine 28.5.2

## Verification results

| Gate | Result | Evidence |
|---|---:|---|
| Solution build | PASS | 0 warnings, 0 errors |
| Unit tests | PASS | 384 passed, 0 failed, 0 skipped; baseline trước thay đổi là 381 |
| Architecture tests | PASS | 2 passed, 0 failed, 0 skipped |
| Phase 0 characterization filter | PASS | 6 passed for `Category=BePlanPhase0Characterization` |
| Integration tests | PASS | 10 passed, 0 failed, 0 skipped against real PostgreSQL/RabbitMQ/Redis containers |
| Test stack | PASS | PostgreSQL, RabbitMQ and Redis all reported `healthy` |
| Clean-database migration | PASS | 27 migrations applied; latest `20260921160954_ProtectCoreMasterDataSelections` |
| Inventory SQL smoke test | PASS | Completed a `REPEATABLE READ, READ ONLY` transaction and `COMMIT` on the migrated test database |
| Current local-data inventory | PASS | Same read-only script completed against the existing local development database; aggregate result is recorded in the data-disposition report |
| OpenAPI snapshot | PASS | OpenAPI 3.0.4; 54 paths; 48 schemas; HTTP 200 |

Commands used:

```powershell
dotnet restore AgriDrone.sln -p:NuGetAudit=false
dotnet build AgriDrone.sln --no-restore
dotnet test tests\AgriDrone.UnitTests\AgriDrone.UnitTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.ArchitectureTests\AgriDrone.ArchitectureTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.IntegrationTests\AgriDrone.IntegrationTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.UnitTests\AgriDrone.UnitTests.csproj --no-build --no-restore --filter "Category=BePlanPhase0Characterization"
docker compose -f compose.step1-test.yaml up -d --wait
```

## OpenAPI compatibility snapshot

- Artifact: `docs/openapi/be-plan-phase0.openapi.json`
- SHA-256: `95931D264C545C3D5A7A9B6CA6398BBD96950456EFCC1CA3615CA3F13ED37745`
- Capture source: `GET /swagger/v1/swagger.json` from the API after all migrations were applied to the Phase 0 test database.
- This snapshot intentionally contains legacy endpoints. Phase 1 must review its diff when deprecating or returning `410 Gone`.

## Characterization coverage

The six tagged tests lock these current behaviors before remediation:

1. Auth register atomically creates User, Tenant, Owner membership and email outbox data.
2. Owner invitation/provisioning is created for a Tenant without an Owner.
3. Farm can currently be created directly with Tenant ownership.
4. Drone registration currently requires and retains Tenant ownership.
5. Mission currently starts without SurveyOrder/payment readiness gates.
6. Mapping publication contract V1 round-trips with its current wire semantics.

## Notes

- No historical migration was edited or deleted.
- The API migration smoke test emitted the existing EF warning that `CompleteMissionLifecycleUc02AndMigrateDroneAudit` contains PostgreSQL enum operations which cannot run transactionally. The migration nevertheless completed on a clean database; the warning remains a known rollback/recovery risk for future migration work.
- Integration containers remain available after this report so subsequent phases can reuse the verified baseline.
