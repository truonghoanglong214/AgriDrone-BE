# Be-Plan Phase 4 Report

- Status: **Passed with archival data gate**
- Verified: 2026-09-25 (Asia/Bangkok)
- Scope: remove out-of-scope API/application/runtime surfaces without inventing retention approval or deleting unresolved legacy data

## Verification results

| Gate | Result | Evidence |
|---|---:|---|
| Solution build | PASS | 0 warnings, 0 errors |
| Unit tests | PASS | 365 passed, 0 failed, 0 skipped |
| Architecture tests | PASS | 33 passed, 0 failed, 0 skipped |
| Integration tests | PASS | 10 passed, 0 failed, 0 skipped against PostgreSQL/RabbitMQ/Redis containers |
| Fresh-database migration | PASS | Integration databases applied all migrations through `20260925053119_Phase4ArchiveHarvestRuntimeModel` |
| Upgrade migration | PASS | Dedicated database upgraded from `20260925050810_AddSystemManagerInvitations` through all three Phase 4 migrations |
| OpenAPI scope check | PASS | 57 paths, 63 operations, 56 schemas; no worker, farm-membership, tenant-staff, self-registration, field-task, harvest/yield/batch/quality path |
| New Survey API check | PASS | No Survey API was added |
| Legacy project check | PASS | Solution and project references contain no `AgriDrone.Modules.FieldTasks` or `AgriDrone.Modules.Harvests` |

## Removed production surface

- Removed `POST /api/auth/register`, its API contract, handler, validator, options and registration-success email template.
- Removed TenantAdmin/Member invitation routes and handlers. Preview, accept and email delivery now reject/skip historical staff invitations and accept only `OwnerProvisioning` invitations for `TenantMemberRole.Owner`.
- Removed tenant role/status mutation controllers and handlers, tenant-user listing, farm member assign/revoke/list/detail, and `users/me/farm-assignments`.
- Removed Harvest catalogue and System harvest-quality mutations, their contracts, handlers and tests.
- Removed FieldTasks and Harvests module projects, DI registrations, EF runtime configurations, enum mappings and all project references.
- Replaced the Farm archive adapter's FieldTask/Plant compatibility dependencies with the retained Mission dependency query. Reusable Farm/Mission/Plant history remains intact.

## Database contract and retention decision

Three independent migrations were added:

1. `20260925052900_Phase4DetachPlantFromFieldTasks` removes the FieldTask FK/index and `plant.plant_scans.source_task_id`.
2. `20260925053000_Phase4ArchiveFieldTaskRuntimeModel` records the `field_task` schema as archival-only while removing it from the EF runtime model.
3. `20260925053119_Phase4ArchiveHarvestRuntimeModel` records the `harvest` schema as archival-only while removing it from the EF runtime model.

Upgrade verification proved that `source_task_id` changed from present to absent, while `field_task.field_tasks` and `harvest.harvest_batches` remained present. Both retained schemas have the comment:

```text
ARCHIVAL ONLY: detached from the AgriDrone runtime in Be-Plan Phase 4
```

No historical migration was edited. No AuditLog, Inbox, Outbox, Plant scan/media/history or Mission data was dropped.

The physical legacy schemas and membership tables are intentionally not dropped. Phase 0 found five unresolved TenantAdmin/Member memberships and three Farm Manager memberships, and its zero-row FieldTask/batch/record inventory is not production retention approval. The Phase 4 read-only inventory script was smoke-tested successfully and found no task/batch/record rows but did find four historical seeded quality-grade rows in the fresh/upgrade test database; their checksum was emitted and the rows were preserved. `be-plan-phase4-legacy-inventory.sql` must be rerun on the approved production-like snapshot; non-zero data must be exported/checksummed and dispositioned before a later contract migration may drop tables or enum types.

## OpenAPI snapshot

- Artifact: `docs/openapi/be-plan-phase4.openapi.json`
- SHA-256: `50C7DFBB7DC52A2CFD04546299A7ECB0A63C77264FFF99F456F02828E2334023`
- OpenAPI: 3.0.4
- Capture source: `GET /swagger/v1/swagger.json` after Phase 4 migrations were applied.

## Commands used

```powershell
dotnet build AgriDrone.sln --no-restore
dotnet test tests\AgriDrone.UnitTests\AgriDrone.UnitTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.ArchitectureTests\AgriDrone.ArchitectureTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.IntegrationTests\AgriDrone.IntegrationTests.csproj --no-build --no-restore
docker compose -f compose.step1-test.yaml up -d --wait
dotnet ef database update 20260925050810_AddSystemManagerInvitations
dotnet ef database update
```

## Exit-gate conclusion

Phase 4 production code has no FieldTasks/Harvests project or API/write path, no staff/farm-worker API, no public self-registration and no new Survey API. Schema drops remain correctly gated by production-like inventory, export/checksum and retention approval; until then, legacy tables are preserved as archival data outside the EF runtime model.
