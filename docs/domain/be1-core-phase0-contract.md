# BE1 Core Phase 0 — locked business conventions

This document is the reviewed contract for Step 0 of `BE1-Core-Business-Implementation-Plan.md`. Domain code is authoritative for allowed transitions; the tables below make the policy reviewable.

## Time and concurrency

- All persisted business timestamps are UTC `DateTimeOffset` values. Application handlers obtain them from injected `TimeProvider.GetUtcNow()` and pass them into domain methods.
- Domain methods reject a non-UTC or backdated transition time.
- Mutable aggregate roots and independently addressed Appointment/Payment rows expose an explicit `Version` concurrency token. Commands carry `ExpectedVersion`; a mismatch or database concurrency failure maps to `409` with the relevant `*.VersionConflict` code. PriceAdjustment mutations are serialized through the owning SurveyOrder version plus the one-pending-adjustment unique constraint.
- A controller or client never sends a derived target state. It invokes a named command; the domain method owns the resulting state.

## SurveyRequest

| From | Command/source of truth | To |
|---|---|---|
| `Submitted` | SystemAdmin `StartReview` | `UnderReview` |
| `UnderReview` | approval orchestration after the complete checklist passes | `Approved` |
| `UnderReview` | SystemAdmin `Reject` with an append-only review snapshot | `Rejected` |
| `Submitted`, `UnderReview` | authorized applicant/owner `Withdraw` | `Withdrawn` |

Approval/rejection cannot bypass `UnderReview`. Review snapshots are appended and never updated/deleted.

## SurveyOrder

| From | Command/source of truth | To |
|---|---|---|
| `PendingScopeConfirmation` | assigned SystemManager confirms scope and immutable price snapshot | `AwaitingAppointment` |
| `AwaitingAppointment` | active appointment is confirmed by TenantOwner | `AwaitingPayment` |
| `AwaitingPayment` | server readiness policy observes confirmed payment, appointment, scope, qualified primary manager and no pending adjustment | `ReadyForOperations` |
| `ReadyForOperations` | BE2 starts the first order-bound operation | `InProgress` |
| `InProgress` | required mission/AI handoff is complete | `PendingReview` |
| `PendingReview` | official result/base-map publication gate succeeds | `Completed` |
| pre-operation states through `ReadyForOperations` | authorized cancellation command | `Cancelled` |

Cancellation after work starts requires an explicit later compensation policy and is therefore not part of the Phase 0 state machine. History is retained.

## Appointment

| From | Command | To |
|---|---|---|
| `Proposed` | TenantOwner confirms | `Confirmed` |
| `Proposed`, `Confirmed` | TenantOwner requests reschedule | `RescheduleRequested` |
| `RescheduleRequested` | assigned SystemManager reproposes a valid UTC window | `Proposed` |
| any non-terminal appointment state | authorized cancellation | `Cancelled` |

Reschedule clears the prior confirmation. The database partial unique index remains the one-active-appointment guard.

## Payment

| From | Command/source | To |
|---|---|---|
| `Pending` | provider initiation | `Processing` |
| `Pending`, `Processing` | verified provider callback or evidenced admin reconciliation | `Confirmed` |
| `Pending`, `Processing` | verified failure | `Failed` |
| `Confirmed` | approved commercial change requires settlement | `AdjustmentRequired` |
| `AdjustmentRequired` | verified settlement | `Confirmed` |
| `Confirmed`, `AdjustmentRequired` | evidenced refund | `Refunded` |

Only server-side verification can confirm payment. `ProviderReference` and the deduplicated `PaymentEvent` are required evidence.

## PriceAdjustment

`Pending → Approved | Rejected`; only `Approved → Applied`. Approval identity/time are mandatory. Applying is performed in the same transaction as the updated order/payment snapshot.

## SurveyResult

`PendingReview → Approved → Published`. Approval is a human SystemManager action. Publication is a separate transaction gate and is the only action that makes a result visible to TenantOwner or allows order completion. Rejected/corrected imported items remain in immutable review history; they cannot become published by setting an enum from the client.

## HTTP and stable error convention

| Situation | HTTP | Stable code family |
|---|---:|---|
| missing/invalid authentication | `401` | authentication infrastructure |
| resource absent or outside tenant/assignment visibility | `404` | `SurveyRequest.NotFound`, `SurveyOrder.NotFound`, `Appointment.NotFound`, `Payment.NotFound`, `SurveyResult.NotFound` |
| visible resource but action not permitted | `403` | `SurveyOrder.Forbidden`, `SurveyResult.Forbidden` |
| stale version, duplicate conflict or illegal transition | `409` | `*.VersionConflict`, `*.InvalidTransition`, `SurveyRequest.Duplicate`, `Payment.DuplicateEvent` |
| syntactically valid request violating a business rule | `422` | `SurveyRequest.InvalidInput`, `SurveyOrder.NotReady`, `Appointment.InvalidWindow`, `Payment.InvalidEvidence` |

Fluent/request-shape validation remains `400`; application `ErrorType.Validation` is business validation and maps to `422`.

## Authorization matrix enforcement

`SurveyAuthorizationPolicy` is deny-by-default and is covered for public, SystemAdmin, assigned/unassigned or qualified/unqualified SystemManager, and correct/wrong TenantOwner. Resource lookup must first apply tenant/assignment visibility so an out-of-scope identifier produces `404`, not an existence-leaking `403`.

## BE1 ↔ BE2 V2 contracts

V1 records, names and schema version stay immutable. V2 has independent event types, descriptors, consumer names and Inbox identities:

- `SurveyOrderOperationalContextV2` via `ISurveyOrderOperationalContextQuery`;
- `FarmReferenceSnapshotV2` via `IFarmReferenceSnapshotQuery`;
- `BaselineMappingCandidatesApprovedV2`;
- `FarmBaseMapPublishedV2`;
- `HealthObservationsReadyV2`;
- `HarvestReadinessAssessmentsReadyV2`;
- `SurveyResultReviewStateChangedV2` for the official review/publication outcome consumed by BE2.

The existing envelope supplies `MessageId`, `CorrelationId`, `OccurredAt` and schema version. Every V2 business payload adds `CausationId`, `SurveyOrderId`, `FarmId`, the source Mission/Job identifiers where applicable, and service-specific context. Golden JSON and validators run alongside—not instead of—the V1 contract tests.
