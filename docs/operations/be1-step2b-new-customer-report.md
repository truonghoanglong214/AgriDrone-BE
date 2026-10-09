# BE1 Step 2B — NewCustomer Survey Request report

- Date: 2026-10-09
- Scope: public NewCustomer application intake
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented behavior

- Added `SurveyRequest.CreateNewCustomer`. It fixes the kind to `NewCustomer`,
  keeps Tenant/Farm/requesting-user IDs null and starts in `Submitted`.
- Domain invariants require an accepting Active or Experimental service,
  positive `numeric(12,4)` area, a valid SRID 4326 point, optional positive
  estimated pole count, paired UTC preferred times and bounded required text.
- Added a dedicated command, FluentValidation validator, handler and response.
  Email is canonicalized to lowercase, phone separators are removed and text is
  trimmed before persistence and idempotency comparison.
- The API input contract contains only applicant, proposed Farm, service,
  coordinates and scheduling data. It cannot supply request number, kind,
  TenantId, FarmId, RequestedByUserId or caller scope.
- The handler resolves idempotency before revalidating current service state so
  a valid retry can still return its original acknowledgement after a service
  lifecycle change.
- New requests require a currently effective per-pole price and an Active or
  Experimental service, matching the public catalogue boundary.
- The handler catches the named caller-scope/idempotency unique constraint and
  resolves the committed row again. Equivalent payloads replay the original
  response; different payloads return the stable mismatch conflict.
- Request number collisions return a stable conflict and remain protected by
  the database unique constraint.

## Atomic acknowledgement

The first successful submission stores exactly these side effects in one
Surveys transaction:

1. one `SurveyRequest`;
2. one PII-redacted system audit record;
3. one email acknowledgement Outbox message.

No Tenant, User, Farm, assignment, invitation or SurveyOrder is created.
Idempotent replay does not enqueue a second acknowledgement.

The acknowledgement uses the existing generic email event and a dedicated,
HTML-encoded template. Generic email messages may now be platform-scoped with
a null TenantId. Every other integration-event descriptor continues to require
a tenant. Database checks restrict tenantless Inbox/Outbox rows to
`notification.email-requested.v1` only.

## Migration

`20261009151552_AllowPlatformScopedMessaging`:

- makes `system.inbox_messages.tenant_id` and
  `system.outbox_messages.tenant_id` nullable;
- adds checks restricting null tenant scope to the generic email event;
- changes the Survey Request estimated-pole-count check from non-negative to
  strictly positive when supplied;
- refuses rollback while tenantless Inbox/Outbox rows exist instead of writing
  a fake empty tenant ID.

Run `docs/operations/be1-step2b-new-customer-preflight.sql` before applying the
migration. Zero-valued estimated pole counts must be corrected deliberately;
the migration fails closed if any remain.

## Deferred boundary

- The public controller, idempotency header and server-derived public
  session/fingerprint policy remain in Phase 2F.
- Existing-Tenant/New-Farm and Existing-Farm factories and handlers remain in
  phases 2C and 2D.
- Admin review/rejection remains in Phase 2E.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Idempotent SQL generation from
  `20261009140000_AddVersionedBusinessMasterData` to
  `20261009151552_AllowPlatformScopedMessaging`: succeeded.
- The installed EF CLI is 8.0.22 while the runtime is 10.0.10; migration
  scaffolding succeeded, but the tool should be aligned before later migration
  authoring.
- Tests were neither added nor run, in accordance with the unified Final Test
  Phase in the implementation plan.
