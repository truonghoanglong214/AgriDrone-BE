# ADR-0006: BE1 business transaction boundaries

- Status: Accepted
- Date: 2026-09-26
- Decision owners: Backend 1, Architecture

## Context

Approval, mapping publication and result publication each change data owned by more than one aggregate/module. Chaining independent `SaveChanges` calls would allow partial onboarding, maps without their Plant history, or published results without a matching order/outbox state.

## Decision

BE1 uses three explicit PostgreSQL transaction boundaries. No boundary uses a distributed transaction and no handler may call another controller over HTTP.

### Survey approval

`ISurveyApprovalUnitOfWork` owns one transaction containing:

- the `SurveyRequest` transition and append-only review;
- request-kind-specific Tenant/Farm/Owner invitation provisioning;
- the active primary `FarmManagerAssignment`;
- exactly one `SurveyOrder` for the request;
- audit and outbox records.

The implementation will be a specialized cross-module `DbContext` that maps only the rows above. The unique Request-to-Order constraint and caller-scoped idempotency key are the final retry guards. Any exception rolls back every write; retry reloads the existing result rather than provisioning again.

### Farm base-map publication

`IMappingPublicationUnitOfWork` owns one transaction containing:

- Inbox deduplication;
- `FarmBaseMapVersion` and its `ZoneMapVersion` slices;
- Plant insert/update plus append-only `PlantChangeEvent` rows;
- audit and V2 outbox records.

The existing `MappingPublicationDbContext` implements this boundary. A V2 consumer gets a consumer name and Inbox identity independent from V1.

### Survey result publication

`ISurveyResultPublicationUnitOfWork` owns one transaction containing:

- Inbox/imported immutable findings or readiness assessments;
- `SurveyResult` and review history;
- derived current Plant health only when a Plant Health result is published;
- the associated `SurveyOrder` transition;
- audit and V2 outbox records.

The specialized implementation must map only the necessary Survey, Plant health, Inbox/Outbox and audit rows. Harvest Readiness never writes Harvest batch, quantity or yield data.

## Transaction rules

- The application operation is passed to `ExecuteInTransactionAsync`; commit occurs only after the operation completes successfully.
- Handlers perform business writes, audit and outbox enqueue before returning from that operation.
- Provider callbacks and message consumers use Inbox/deduplication inside the same local transaction.
- Optimistic-concurrency and unique-constraint failures return `409`; business rule failures return `422`.
- Each implementation must have a PostgreSQL rollback test before its orchestration handler is exposed. Phase 0 locks the reusable interface/test seam; Step 3, Step 6 and Step 7 activate the respective data-specific rollback scenarios.
- `AgriDroneSchemaDbContext` remains design-time/migration-only and is not injected into feature handlers.

## Consequences

The database project may depend on the required domain modules to implement a narrow transaction boundary. Modules and API code depend only on application ports/contracts, never on the cross-module context. Outbox delivery may fail after commit without rolling back business data and is recovered by the existing dispatcher/redrive path.
