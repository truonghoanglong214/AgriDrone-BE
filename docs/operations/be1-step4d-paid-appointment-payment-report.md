# BE1 Step 4D — Paid-service appointment and payment

- Date: 2026-10-12
- Scope: PaidService appointment lifecycle, owner payment initiation and trusted provider callbacks
- Status: CodeComplete — AwaitingFinalTest

## Paid-service appointment boundary

- The existing appointment commands are now purpose-aware for both
  `BaselineMapping` and `PaidService` instead of duplicating lifecycle code.
- A SystemManager may propose a PaidService appointment only after the Order has
  the authoritative pole-count and immutable price snapshot from Step 4C and is
  in `AwaitingPaidAppointment`.
- TenantOwner authorization is derived from Order Tenant ownership in the locked
  persistence query. Confirm and request-reschedule require both expected Order
  and appointment versions.
- Confirm transitions the Order to `AwaitingPayment`. A reschedule request made
  before payment reopens `AwaitingPaidAppointment`; a current active payment
  blocks schedule mutation.
- Re-proposal supersedes the prior revision and creates a new appointment linked
  through `PreviousAppointmentId`. The existing partial unique index still
  guarantees one current appointment per Order and purpose.
- Appointment changes retain the existing atomic audit and
  `survey.appointment-changed.v1` Outbox behavior. Baseline Mapping remains free
  of payment requirements.

## Owner payment initiation

- `POST /api/tenant-owner/survey-orders/{orderId}/payments` accepts only
  `expectedOrderVersion`; amount and currency are never accepted from the owner.
- The serializable payment transaction locks the Order, re-checks active OWNER
  membership and requires a current, confirmed, non-expired PaidService
  appointment.
- The payment amount and currency are copied only from `FinalPrice` and
  `Currency` on the immutable Order snapshot. The existing partial unique index
  enforces one active payment per Order under concurrent requests.
- `ISurveyPaymentGateway` keeps provider mechanics outside the application
  feature. The `agridrone-hmac` adapter issues a provider reference, short-lived
  redirect URL and signed client token. It is disabled by default and validates
  its checkout URL and signing secret when enabled; no secret is committed.
- Payment creation, system/user audit, `survey.payment-changed.v1` Outbox and
  response state are persisted in the same transaction. A failed payment may be
  retried; Processing, Confirmed or AdjustmentRequired remains active.

## Trusted callback and state transitions

- `POST /api/integrations/payments/{provider}/callbacks` is anonymous at the JWT
  layer because provider authentication uses
  `X-AgriDrone-Payment-Timestamp` and
  `X-AgriDrone-Payment-Signature`.
- The endpoint reads the bounded raw request body and verifies configured provider,
  timestamp tolerance and constant-time HMAC-SHA256 over
  `{unixTimestamp}.{rawJson}` before dispatching MediatR. Invalid verification
  does not create a PaymentEvent.
- Supported Step 4D events are `payment.processing`, `payment.succeeded` and
  `payment.failed`. Refund/chargeback handling belongs to Step 4E.
- The repository locks Payment and Order together. Every accepted callback is
  stored as an immutable PaymentEvent and deduplicated by
  `(Provider, ProviderReference, ProviderEventId)` at query and database levels.
- Events older than the current payment update and events received after a final
  state are recorded but cannot regress state. A success with any amount/currency
  mismatch transitions Payment to `AdjustmentRequired` and never marks the Order
  ready.
- Only a matching trusted success can transition Payment to `Confirmed` and the
  Order to `ReadyForPaidService`. There is no TenantOwner command or endpoint to
  mark a payment confirmed. Callback acknowledgement is returned only after the
  transaction commits; a concurrent duplicate is acknowledged idempotently.

## Schema and migration

- Migration: `20261011162047_Phase9PaidAppointmentPayment`.
- `payment_events` gains non-null `provider_reference` and `provider_event_id`.
  Legacy rows are backfilled from event payload/payment reference with explicit
  deterministic legacy fallbacks and a fail-closed completeness guard.
- A unique index on provider/reference/event ID protects callback deduplication;
  the existing provider/deduplication hash index remains defense in depth.
- `ck_survey_payments_provider_session` requires a provider reference after a
  Payment leaves Pending. Existing non-Pending legacy payments receive a stable
  legacy reference before the constraint is installed.
- Downgrade removes only the new constraint, index and event identity columns.

## HTTP/OpenAPI contract

- Initiation declares `201/400/401/403/404/409/422`, and its body exposes only
  `expectedOrderVersion`.
- Callback declares `200/400/401/404/409/413/422`, has no Bearer security
  requirement, documents the two signature headers and publishes a concrete JSON
  schema for provider reference, event identity/type, amount, currency and UTC
  occurrence time.
- `docs/openapi/be1-surveys.openapi.json` is generated from the running
  Development API rather than edited manually.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- EF pending-model check: no changes since the Step 4D migration.
- Forward and downgrade SQL generation pass; the forward SQL contains the legacy
  reference backfill, completeness guard, provider-session check and tuple unique
  index.
- Runtime Swagger snapshot exposes 81 paths, including the owner initiation and
  anonymous callback routes described above.
- Tests were not added or run, per the consolidated Final Test Phase. Paid
  appointment races, cross-Tenant access, server-owned amount, session concurrency,
  invalid signature/timestamp/provider, concurrent deduplication, out-of-order
  callbacks and mismatch handling are recorded in section 27.2.
- The migration was not applied to a live database at this checkpoint. Fresh and
  upgrade PostgreSQL evidence remains part of the Final Test Phase.

## Next gate — Phase 4E

Implement append-only price adjustment, SystemAdmin reconciliation,
refund/chargeback transitions and the single authoritative purpose-aware readiness
service consumed by BE2 prepare/start.
