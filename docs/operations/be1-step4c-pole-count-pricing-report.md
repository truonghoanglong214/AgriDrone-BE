# BE1 Step 4C — Confirmed pole count and per-pole pricing snapshot

- Date: 2026-10-11
- Scope: authoritative active-pole confirmation and immutable Order pricing
- Status: CodeComplete — AwaitingFinalTest

## HTTP and ownership boundary

- `POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm` is limited
  to the currently assigned SystemManager.
- The request contains only `expectedFarmBaseMapVersionId`, evidence and expected
  Order version. It cannot provide pole count, unit price, currency or final price.
- Authorization is re-checked inside the transaction against the active Farm,
  current assignment, active account/profile and unexpired `QUALIFIED` status.
- The controller maps the HTTP contract and dispatches MediatR; it does not inject
  a DbContext or repository.

## Authoritative count and price resolution

- `SurveyPricingConfirmationDbContext` owns a serializable transaction for the
  locked SurveyOrder, audit and Outbox records.
- The repository locks the Order, approved FarmBoundary and current published
  Farm base map, then rejects a stale expected base-map version.
- Authoritative inventory is counted from `plant.plants` where lifecycle is
  `ACTIVE` and `current_map_version_id` matches the current published map.
- Every counted Plant must be covered by the approved FarmBoundary and its active,
  non-archived Zone boundary. Empty inventory or any active current-map Plant
  outside that approved scope fails closed.
- Count source is derived from base-map provenance: the current Order's own map is
  `BASELINE_PUBLICATION`; a reused map is `CURRENT_PUBLISHED_INVENTORY`. This must
  agree with the Order's baseline requirement.
- The effective price is resolved at server time from the selected Survey Service.
  Exactly one non-legacy VND `PricePerPole` version must be effective; missing or
  ambiguous price windows fail without changing the Order.
- `SurveyOrder.ConfirmPoleCountAndPricing` uses the single `Money` policy:
  `FinalPrice = Round(ConfirmedSurveyPoleCount × PricePerPole, 2,
  AwayFromZero)`.

## Snapshot and atomic effects

- The Order stores FarmBoundary/base-map version, count source, evidence,
  confirmed count, price-version ID, `PricePerPole`, currency, final price,
  confirmer and UTC confirmation time.
- Confirmation transitions only `AwaitingPricing → AwaitingPaidAppointment`.
  Later catalogue or inventory changes cannot call the transition again and do
  not silently recalculate the stored snapshot.
- Audit and `survey.order-pricing-confirmed.v1` Outbox records are persisted in
  the same transaction. Serialization, deadlock, uniqueness and optimistic-version
  conflicts return a stable conflict result.
- The initial-map publication producer remains a Step 6C responsibility and must
  call the existing `SurveyOrder.RecordBaselinePublication` handoff atomically.
  Phase 4C supplies the receiving domain and confirmation path for both initial
  and already-mapped Farms.

## Schema and migration

- Migration: `20261011154933_Phase9PoleCountPricingSnapshot`.
- Added `system.survey_pole_count_source` with
  `BASELINE_PUBLICATION` and `CURRENT_PUBLISHED_INVENTORY`.
- Added nullable `pole_count_source` and
  `pole_count_evidence_reference varchar(1000)` columns to SurveyOrder.
- Existing count snapshots are backfilled from the referenced base-map source
  Order. A count row without a matching same-Tenant/Farm base map blocks migration
  instead of receiving guessed provenance.
- Updated constraints require every non-null confirmed count to include boundary,
  base-map, source and confirmation metadata. Blank evidence is rejected when
  evidence is present; historical rows may remain null.
- Downgrade removes the columns and enum type only after restoring the previous
  snapshot constraint.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- EF pending-model check: no changes since the latest migration.
- Forward and downgrade SQL generation both pass. Forward SQL contains the
  provenance backfill and fail-closed guard; downgrade restores the former check.
- Runtime Swagger snapshot: OpenAPI 3.0.4, 79 paths and 96 schemas. The new route
  declares `200/400/401/403/404/409`; request requires evidence, expected map ID
  and expected Order version and exposes no monetary/count input.
- Runtime snapshot generation logged sandbox-specific Windows DataProtection/DPAPI
  warnings, but the API started and served Swagger successfully.
- Tests were not added or run, per the consolidated Final Test Phase. Stale map,
  concurrent confirmation, out-of-scope inventory, zero count, missing/ambiguous
  price, rounding and immutable-snapshot scenarios are recorded in section 27.2.
- The migration was not applied to a live database in this checkpoint; PostgreSQL
  upgrade/rollback and PostGIS scope execution remain Final Test Phase evidence.

## Next gate — Phase 4D

Implement the PaidService appointment and provider-backed payment lifecycle using
only the immutable amount/currency snapshot created by Phase 4C. No owner or client
path may mark payment confirmed directly.
