# BE1 Core Business — Phase 0 completion report

- Completed: 2026-09-26
- Scope: Step 0 in `Codex-Plan/BE1-Core-Business-Implementation-Plan.md`
- Database migration: none (Phase 5 schema is unchanged)
- Public Survey API opened: no

## Delivered

- Explicit transition tables and named domain methods for SurveyRequest, SurveyOrder, Appointment, Payment, PriceAdjustment and SurveyResult.
- Survey domain code is organized by business concept (`Requests`, `Orders`, `Appointments`, `Payments`, `Results`, `Catalogue`, and `ValueObjects`); every aggregate owns its transition rules and domain error codes instead of relying on a central state-machine/error file.
- Identity SystemManager application code is organized by use case, with separate command, validator, handler, response, and repository files where applicable.
- Survey EF Core mappings use one entity configuration per file.
- UTC/backdating guards and explicit optimistic-version checks with stable conflict codes.
- Stable Survey error families and `401/403/404/409/422` application-error mapping.
- A deny-by-default Survey actor policy with public, SystemAdmin, assigned/qualified SystemManager and own-TenantOwner tests.
- Accepted ADR-0006 for approval, mapping publication and result publication transaction boundaries.
- Compiling `ISurveyApprovalUnitOfWork`, `IMappingPublicationUnitOfWork` and `ISurveyResultPublicationUnitOfWork` ports; the existing mapping publication context now implements its atomic seam.
- Order-readiness and Farm/Plant-reference query contracts for BE2.
- Five immutable V2 event payloads with independent descriptors/consumer names, required order/farm/source/causation context and payload validators.
- V1 immutability checks plus V2 golden JSON/round-trip/validation tests.
- Architecture tests preventing API-to-SurveysDbContext coupling and integration-contract-to-domain/EF coupling.

## Boundary activation gates

Phase 0 intentionally does not expose Survey feature endpoints or add schema. Before later handlers are exposed:

- Step 3 must implement the approval specialized DbContext and its PostgreSQL all-or-nothing rollback scenario.
- Step 6 must move the V2 mapping handler onto `IMappingPublicationUnitOfWork` and add Farm-level publication rollback coverage.
- Step 7 must implement the result-publication specialized DbContext and its result/current-health/order/audit/outbox rollback scenario.

These are implementation gates, not deferred Phase 0 design decisions; table ownership and transaction semantics are locked by ADR-0006.

## Verification commands

```powershell
dotnet build backend/AgriDrone.sln --no-restore
dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj --no-build
dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj --no-build
```

Verification result on 2026-09-26: solution build passed with zero warnings/errors; 421 unit tests and 56 architecture tests passed with no skips. The 11 existing infrastructure integration tests could not run because PostgreSQL `127.0.0.1:55432`, RabbitMQ `127.0.0.1:55672` and Redis `127.0.0.1:56379` were not running; failures were connection-refused/timeouts rather than assertions in this phase.
