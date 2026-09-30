# BE1 Core Business — Phase 1A completion report

Date: 2026-09-26

Status: COMPLETE

## Scope delivered

- Added the single `AddSurveysModule` runtime registration entry point.
- Registered `SurveysDbContext` with all Survey PostgreSQL enums and PostGIS.
- Added the module-local unit of work, Survey Service repository, catalogue query
  port and EF implementations.
- Made the runtime context an atomic persistence boundary for Survey data, audit
  records and outbox messages.
- Registered MediatR, FluentValidation, `TimeProvider` and scoped persistence
  dependencies from the Surveys assembly.
- Registered a readiness health check that verifies Survey storage and the two
  required service identities without constraining their mutable lifecycle state.
- Referenced and registered the Surveys module from `AgriDrone.Api`.
- Added no Survey HTTP endpoint; public catalogue behavior remains Phase 1B.

## Verification evidence

- `dotnet build AgriDrone.sln --no-restore`: passed with 0 warnings and 0 errors.
- `AgriDrone.UnitTests`: 424 passed, 0 failed.
- `AgriDrone.ArchitectureTests`: 59 passed, 0 failed.
- `Be1CorePhase1ARuntimeIntegrationTests`: passed against PostgreSQL 17/PostGIS.
  The test applies all migrations to a fresh database, resolves the runtime
  module, checks connectivity, reads both required service seeds and executes the
  Survey readiness health check.
- Production-profile API startup against the migrated PostgreSQL test database:
  passed; `GET /health/ready` returned HTTP 200 and `Healthy`.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.

## Runtime boundary

- API controllers may depend on application request handlers only; architecture
  tests reject direct dependencies on Surveys infrastructure or repositories.
- `AgriDrone.Modules.Surveys` does not reference `AgriDrone.Api` or the
  cross-module migration project.
- The runtime `SurveysDbContext` is not the migration owner;
  `AgriDroneSchemaDbContext` remains the design-time migration context.

## Deferred to Phase 1B+

- Public service availability and current-price resolution policy.
- Public and SystemAdmin controllers/contracts.
- Survey Service lifecycle command handlers and authorization.
- Price-version mutations and Harvest Readiness criteria catalogue.
