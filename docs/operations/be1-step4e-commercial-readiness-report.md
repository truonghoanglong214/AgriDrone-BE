# BE1 Step 4E — Commercial adjustment and authoritative readiness

- Date: 2026-10-12
- Scope: append-only price adjustment, SystemAdmin payment reconciliation,
  refund/chargeback and purpose-aware readiness
- Status: CodeComplete — AwaitingFinalTest

## Price-adjustment lifecycle

- Assigned and currently qualified SystemManager uses
  `POST /api/system-manager/survey-orders/{orderId}/price-adjustments` to propose
  a new confirmed pole count with reason/evidence. The effective VND per-pole
  price is resolved at server time; the caller cannot provide unit price,
  currency or final price.
- `PriceAdjustment` snapshots old/new pole count, price-version ID,
  price-per-pole, final price and currency. It retains request, review and apply
  actors/timestamps/evidence plus an `xmin` optimistic version.
- SystemAdmin approve, reject and apply are separate commands and routes. Only
  apply mutates the Order snapshot. The transaction locks Order, Adjustment and
  latest Payment, verifies all expected versions, writes audit and emits
  `survey.price-adjustment-changed.v1` through the same Outbox boundary.
- A confirmed Payment that no longer matches the applied snapshot transitions to
  `AdjustmentRequired`; the Order moves from `ReadyForPaidService` back to
  `AwaitingPayment`. Pending or approved-not-applied adjustment independently
  closes readiness.

## Reconciliation and provider revocation

- `POST /api/system-admin/survey-payments/{paymentId}/reconcile` requires exact
  provider reference, settled amount/currency, reason, evidence and expected
  Payment/Order versions.
- Reconciliation cannot force a mismatch to Confirmed. Only amount/currency equal
  to the current Order snapshot can transition Pending, Processing or
  AdjustmentRequired Payment to Confirmed and reopen the commercial gate.
- Provider callback accepts `payment.refunded` and `payment.chargeback` in
  addition to the Phase 4D event set. Both retain the immutable PaymentEvent,
  transition the Payment to Refunded and move a not-yet-started ready Order back
  to AwaitingPayment. In-progress missions are not retroactively rewritten.

## Authoritative readiness

- `ISurveyOrderReadinessService` is the single application port intended for BE2
  prepare/start checks. It accepts Order, Mission and
  `BaselineMapping`/`PlantHealth`/`HarvestReadiness` purpose.
- The database projection reads current approved boundary/scope, current
  appointment, active qualified/available primary manager, linked Mission,
  schedule, Drone, completed safe pre-flight, current published base map,
  count/price snapshot, latest Payment and open PriceAdjustment on every call.
- Baseline Mapping does not require map/count/price/payment. Paid purposes require
  the Survey Service type to match purpose, a published current map, count/price,
  exact Confirmed payment and no open adjustment.
- Stable failure codes now include map/count/price, payment mismatch/revocation,
  adjustment, mission/purpose, assignment/availability and safety conditions.
  The manager HTTP endpoints use the same service and additionally verify that
  the current actor is the assigned manager.

## Schema and migration

- Migration: `20261012024731_Phase9CommercialAdjustmentReadiness`.
- The migration expands the legacy area-based PriceAdjustment record to the
  per-pole snapshot model, preserves legacy columns as nullable history and
  performs a guarded upgrade backfill before installing non-null/check/FK/index
  constraints.
- One open Pending/Approved adjustment per Order is enforced by a partial unique
  index. Review/application consistency and final-price arithmetic are protected
  by database checks.
- Forward and downgrade SQL generation both pass. Downgrade reconstructs the
  legacy area/price projection before removing 4E columns.

## HTTP/OpenAPI contract

- The runtime Swagger snapshot exposes 87 paths and includes six new 4E paths:
  manager request, paid-service readiness, admin approve/reject/apply and payment
  reconciliation.
- Refund/chargeback reuse the signed provider callback schema; no owner payment
  confirmation endpoint was added.
- `docs/openapi/be1-surveys.openapi.json` was generated from the running
  Development API with startup migration/master-data/admin-bootstrap gates
  disabled for contract capture.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- EF pending-model check: no changes since the 4E migration.
- Forward and downgrade SQL generation: PASS.
- Runtime Swagger capture: PASS, 87 paths and 109 component schemas.
- Tests were not added or run, per the consolidated Final Test Phase. Section
  27.2 already contains refund/chargeback, pending-adjustment, direct-readiness,
  failure-code and state-change re-check scenarios.
- The migration was not applied to a live database at this checkpoint. Fresh and
  upgrade PostgreSQL evidence remains part of the Final Test Phase.

## Next gate

Connect atomic initial-map publication to
`SurveyOrder.RecordBaselinePublication`, then make BE2 prepare/start call the
authoritative readiness port and re-check immediately before flight.
