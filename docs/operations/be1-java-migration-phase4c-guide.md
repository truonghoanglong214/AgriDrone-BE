# BE1 Java Migration Phase 4C Implementation Guide

Date: 2026-10-02

Phase 4C ports TenantOwner onboarding, tenant selection and the SystemAdmin
tenant lifecycle/read APIs to BE1 Java. Password reset is already complete in
4B.6 and must not be reimplemented in this slice.

Implementation status:

- [x] Step 1 — API DTO, pagination shape and stable error-code contracts.
- [x] Step 2 — repository ports, JPA projections/adapters and Postgres tests.
- [x] Step 3 — short-lived RSA tenant-selection token.
- [x] Step 4 — multi-tenant login selection response.
- [x] Step 5 — select-tenant endpoint.
- [x] Step 6 — tenant lifecycle, audit and effective-access invalidation.
- [x] Step 7 — Owner provisioning, secure invitation token and outbox delivery.
- [x] Step 8 — invitation preview.
- [x] Step 9 — atomic invitation acceptance.
- [x] Step 10 — SystemAdmin tenant and user-membership read models.
- [x] Step 11 — vertical, authorization, concurrency and legacy-negative tests.

## Current starting point

The Java baseline already has `Tenant`, `TenantMembership` and
`TenantInvitation` domain/persistence types. Its V1 Flyway schema already
enforces one active Owner per tenant, one membership per tenant/user, unique
invitation token hashes and one pending Owner provisioning per tenant.

Phase 4C now provides:

- a short-lived RSA tenant-selection token and multi-tenant login response;
- `POST /api/auth/select-tenant` with authoritative database rechecks;
- tenant lifecycle, Owner provisioning, invitation preview/accept and both
  SystemAdmin read models; and
- contract, authorization, end-to-end and real-Postgres concurrency coverage.

Do not edit the applied V1 migration. Add a V3 migration only if implementation
reveals a genuine schema gap.

## Contract that 4C must preserve

| Method | Route | Actor | Result |
|---|---|---|---|
| `POST` | `/api/auth/select-tenant` | anonymous + selection token | normal login response with tenant session |
| `POST` | `/api/system/tenants` | SystemAdmin | create tenant, `201` |
| `PUT` | `/api/system/tenants/{tenantId}/activate` | SystemAdmin | idempotent activation, `204` |
| `PUT` | `/api/system/tenants/{tenantId}/deactivate` | SystemAdmin | idempotent deactivation, `204` |
| `POST` | `/api/system/tenants/{tenantId}/owner-provisionings` | SystemAdmin | create Owner invitation, `201` |
| `GET` | `/api/system/tenants/all` | SystemAdmin | paged tenant read model |
| `GET` | `/api/system/users/{userId}/tenants` | SystemAdmin | paged membership read model |
| `POST` | `/api/auth/invitations/preview` | anonymous | masked invitation preview |
| `POST` | `/invitations/accept` | anonymous | accept invitation; keep this absolute legacy path |

`POST /api/tenants/current/transfer-ownership` stays `LEGACY_GONE`; do not port
its handler. Public registration and TenantAdmin/Member mutation also stay out.

## Ordered implementation

### 1. Freeze API DTOs and stable errors — complete

Copy field names, nullability, HTTP status and error codes from the Phase 0
OpenAPI snapshot and the current .NET handlers before writing services. At a
minimum preserve these response shapes:

- login: `email`, `fullName`, `phone`, then exactly one of `session` or
  `tenantSelection`;
- tenant selection: `selectionToken`, `expiresAt`, and tenant options containing
  `id`, `code`, `name`, `role`;
- preview: `maskedEmail`, `tenantName`, `role`, `expiresAt`,
  `requiresAccountCreation`;
- accept: `userId`, `tenantId`, `role`, `accountCreated`;
- owner provisioning: `invitationId`, `email`, `expiresAt`.

Create stable Java error constants before handler logic. Preserve at least
`Authentication.InvalidTenantSelectionToken`, `Tenant.AccessDenied`,
`Tenant.NotFound`, `Tenant.Inactive`, `TenantInvitation.InvalidOrExpired`,
`TenantInvitation.OwnerAlreadyAssigned`,
`TenantInvitation.OwnerProvisioningAlreadyPending`,
`TenantInvitation.UserAlreadyMember`,
`TenantInvitation.RegistrationDetailsRequired`, and
`TenantInvitation.UserInactive`.

Gate: controller contract tests assert JSON and status codes for success and
each public error path.

### 2. Extend repository ports and adapters first — complete

Add only the queries required by use cases:

- tenant: page all including inactive, find by id including inactive, and a
  normalized-code existence check;
- membership: find any membership by user/tenant, page memberships by user, and
  keep `hasActiveOwner` as an authoritative database query;
- invitation: find pending Owner provisioning, save/update expiry, and keep
  `findByTokenHashForUpdate` for acceptance;
- user: existing email/id/system-role queries plus `save` for account creation.

Use projections for both paged read models instead of loading aggregates and
mapping in memory. Keep page defaults `1/20`, validate positive page numbers,
and cap page size consistently with the existing API policy.

Gate: repository integration tests run against Postgres and cover inactive
rows, pagination ordering, normalized lookup, the row lock and both unique
partial indexes.

### 3. Implement the short-lived tenant-selection token — complete

Introduce an application port such as `TenantSelectionTokenService` with
`issue(userId)` and `validate(token)`. Its infrastructure implementation should:

- use the configured Java RSA signing key and `RS256`;
- use the same issuer as access tokens, audience
  `<access-audience>.TenantSelection`, and claim `purpose=tenant_selection`;
- contain `sub`, `jti`, `iat`, `nbf`, `exp`, with a five-minute configurable TTL;
- reject wrong signature, issuer, audience, purpose, missing subject and expiry;
- never be accepted by the normal resource-server access-token decoder.

Inject `Clock` and token-id generation so tests are deterministic.

Gate: unit tests cover round-trip validation and every rejection case, including
proving that an access token cannot be used as a selection token and vice versa.

### 4. Change multi-tenant login without regressing other login modes — complete

Update `LoginUserResult` and `LoginResponse` so `session` is nullable and
`tenantSelection` is nullable. Then change `LoginUserService`:

1. SystemAdmin/SystemManager still receives a system session without tenant.
2. Zero active memberships still returns the stable no-membership error.
3. One active membership still receives an access token immediately.
4. More than one active membership receives no access token; return the
   selection token and sorted active tenant options.

Filter out inactive/deleted tenants even if a stale active membership exists.
Only record a successful login after returning a valid session or selection
payload.

Gate: service and controller tests cover all four branches and verify that the
multi-tenant response does not expose an access token.

### 5. Add `POST /api/auth/select-tenant` — complete

Implement `SelectTenantUseCase` in this order:

1. Validate the selection token and obtain its user id.
2. Reload the user and require `ACTIVE`; otherwise return
   `Authentication.InvalidTenantSelectionToken`.
3. Load the requested active membership and its active tenant.
4. Return `Tenant.AccessDenied` when the user cannot select that tenant.
5. Reload system role codes and issue the normal access token with `tenant_id`,
   `tenant_membership_id` and `tenant_role`.
6. Return the same login/session DTO used by `POST /api/auth/login`.

The endpoint is anonymous because the selection token is its credential. Do not
trust tenant ids or roles embedded by the client; reload all authorization data
from PostgreSQL.

Gate: API tests cover valid selection, expired/tampered token, inactive user,
inactive tenant, inactive membership and a tenant belonging to another user.

### 6. Implement tenant create/activate/deactivate — complete

Add SystemAdmin-only use cases and controller routes. Normalize tenant code
before checking uniqueness. Create tenants as active, matching the baseline.
Activation and deactivation are idempotent, but a missing tenant returns the
stable not-found error.

Write an audit record in the same transaction for CREATE, ACTIVATE and
DEACTIVATE, including actor id and correlation id. Deactivation must immediately
make tenant selection, invitation preview/accept and tenant-scoped access fail
closed; it does not delete memberships or history.

Gate: integration tests assert authorization, idempotency, audit rollback and
the access effect of deactivation/reactivation.

### 7. Implement Owner provisioning and invitation delivery — complete

Add a configurable invitation TTL and a cryptographically random token service.
Store only `SHA-256(plainToken)`; the plain token may appear only in the newly
created outbox event used by the notification flow.

Inside one transaction:

1. Require an active tenant and current SystemAdmin actor.
2. Normalize the invited email.
3. Reject self-invite, existing membership and an existing active Owner.
4. Check pending Owner provisioning. Expire an obsolete row; reject one that is
   still usable.
5. Create the `OWNER`/`OWNER_PROVISIONING` invitation.
6. Add the invitation-email integration event to the outbox.
7. Commit invitation and outbox together.

Translate unique-index races to
`TenantInvitation.OwnerProvisioningAlreadyPending`; do not expose a raw SQL
constraint error.

Gate: unit plus concurrent Postgres tests prove that two requests cannot create
two active pending Owner invitations and that rollback leaves no outbox row.

### 8. Implement invitation preview — complete

For `POST /api/auth/invitations/preview`, hash the supplied token, load the
invitation, and require pending + unexpired + Owner provisioning purpose + Owner
role. Require the tenant to exist and be active. If an existing user is
inactive, return `TenantInvitation.UserInactive`.

Return only the masked email (`a***@domain`), tenant name, role, expiry and
whether account creation is required. Use the same public invalid/expired error
for unknown, used, revoked and expired tokens to avoid token-state disclosure.

Gate: tests cover new/existing user, malformed/unknown/expired/accepted token,
inactive user and inactive tenant.

### 9. Implement atomic invitation acceptance — complete

For the absolute route `POST /invitations/accept`, execute all work in one
transaction and lock the invitation row by token hash:

1. Recheck pending/unexpired/purpose/role after acquiring the lock.
2. Require no active Owner and require an active tenant.
3. Find the normalized invitation email; never accept a client-supplied email.
4. If no account exists, require trimmed full name and a password of at least
   eight characters, hash the password and create an active user. Phone is
   optional and trimmed.
5. If an account exists, require it to be active and ignore registration fields.
6. Reject any existing membership for that tenant.
7. Insert the active Owner membership, mark the invitation accepted once, and
   enqueue the welcome-email event in the same transaction.
8. Translate the active-Owner unique-index conflict to
   `TenantInvitation.OwnerAlreadyAssigned`.

The database constraints are the final concurrency guard. Never implement this
as separate check/commit transactions.

Gate: concurrent acceptance yields exactly one Owner membership, one accepted
invitation and one welcome outbox message. Reuse, rollback and losing-race tests
must return stable errors without partial user/membership data.

### 10. Implement the two SystemAdmin read models — complete

Add `GET /api/system/tenants/all` and
`GET /api/system/users/{userId}/tenants`. Preserve the .NET field names and
paged envelope exactly. Sort deterministically before applying offset/limit.
Both routes require SystemAdmin; tenant context is not required.

Gate: contract tests cover default/custom pagination, empty pages, inactive
tenants/memberships and non-SystemAdmin rejection.

### 11. Close 4C with vertical and negative tests — complete

Run one end-to-end path:

1. SystemAdmin creates tenant.
2. SystemAdmin provisions Owner.
3. Anonymous user previews and accepts the invitation.
4. Owner login receives a tenant session.
5. Add a second active membership test fixture; login returns tenant selection.
6. Select one tenant and verify the issued access-token claims.
7. Deactivate that tenant and prove selection plus tenant-scoped access are
   denied; reactivate and prove access can be re-established with a new token.

Also assert route absence/`410` behavior for ownership transfer, public
registration and other forbidden legacy mutations. Run `./mvnw.cmd verify`, then
compare Java responses against the frozen OpenAPI/golden fixtures. Mark 4C done
only when every route above has a passing contract test and concurrency tests
pass against real Postgres.

Completion verification (2026-10-02): `./mvnw.cmd verify` passes all 128 tests
with zero failures, errors or skips. This includes the frozen controller/JWT
contracts, SystemAdmin authorization, legacy `410` behavior, the complete Owner
onboarding-to-tenant-selection vertical path, and concurrent provisioning and
acceptance against PostgreSQL 17 via Testcontainers.

## Recommended commit slices

Keep the work reviewable in this order:

1. DTO/error contracts and repository extensions.
2. Selection-token primitive and multi-tenant login.
3. Select-tenant endpoint.
4. Tenant lifecycle plus audit.
5. Owner provisioning plus outbox.
6. Invitation preview/accept plus concurrency tests.
7. Read models, end-to-end tests and migration documentation.
