# BE1 Step 2F — Contract and security hardening report

Date: 2026-10-10

Status: **CodeComplete — AwaitingFinalTest**

## HTTP surface

The runtime OpenAPI document exposes exactly these Survey Request routes:

```text
POST /api/public/survey-requests
POST /api/tenant-owner/survey-requests/new-farm
POST /api/tenant-owner/farms/{farmId}/survey-requests
GET  /api/system-admin/survey-requests
GET  /api/system-admin/survey-requests/{requestId}
POST /api/system-admin/survey-requests/{requestId}/start-review
POST /api/system-admin/survey-requests/{requestId}/reject
```

No `approve` endpoint is registered. Approval and all provisioning side effects
remain in the atomic Step 3 orchestration boundary.

## Authorization and caller identity

- Public NewCustomer submission is explicitly `AllowAnonymous` and its OpenAPI
  operation overrides the global Bearer requirement.
- Both TenantOwner submission routes require the existing `TenantOwner` policy.
- Inbox, detail, start-review and reject require the existing `SystemAdmin`
  policy.
- TenantId, UserId and Farm ownership remain server-derived. The new-Farm body
  contains none of those identifiers; the existing-Farm route accepts FarmId
  only in the route and resolves its Tenant on the server.
- Exact-origin development CORS now permits credentials so the public anonymous
  session cookie can cross the configured frontend/API origins safely; wildcard
  origins are not enabled.

## HTTP idempotency

- All three submit routes require the `Idempotency-Key` header.
- The HTTP boundary trims the key, rejects blank values and enforces the same
  100-character maximum as `RequestIdempotency` and the database schema.
- Authenticated flows derive caller scope from the execution context in their
  handlers.
- The anonymous flow derives caller scope from an opaque Guid v7 session created
  by the server and stored in a time-limited, protected, HttpOnly, SameSite=Lax,
  essential cookie. HTTPS requests also set Secure.
- The anonymous scope is not supplied by the request body/header and does not
  use IP address or User-Agent, avoiding spoofable identity and shared-NAT
  collisions.
- Invalid, expired or tampered public cookies are rotated rather than trusted.

## Contract and PII boundary

- Public response enums are serialized as stable uppercase wire strings.
- Admin query filters accept the documented strings, including
  `UNDER_REVIEW`, `NEW_CUSTOMER`, `EXISTING_TENANT_NEW_FARM` and
  `EXISTING_FARM_SURVEY`.
- Public/TenantOwner acknowledgement responses contain request identity, kind,
  status and creation time without echoing applicant PII or free-form notes.
- Admin inbox keeps contact PII out of the list response. Full review detail is
  available only under the SystemAdmin policy.
- Candidate coordinates are explicitly exposed with
  `IsApprovedFarmBoundary = false`; intake data is never presented as an
  approved Farm boundary.
- HTTP validation responses do not echo idempotency keys.

## OpenAPI evidence

`docs/openapi/be1-surveys.openapi.json` was generated from the running
`GET /swagger/v1/swagger.json` endpoint, not assembled manually. Inspection
confirmed:

- OpenAPI 3.0.4;
- exactly seven Survey Request paths;
- global Bearer security with an empty security requirement only on the public
  operation;
- required `Idempotency-Key` parameters on all three submit operations;
- string schemas with the stable status/kind enum values;
- zero `/approve` paths.

To permit contract generation without a running database, core-master-data
startup validation now has a configuration switch. Its application default is
still `true`; only the snapshot-generation process used an environment override
alongside the existing migration startup override.

## Persistence impact

Step 2F introduces no schema changes and no migration.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`: succeeded with 0 warnings
  and 0 errors.
- `dotnet ef migrations has-pending-model-changes --context
  AgriDroneSchemaDbContext ...`: no pending model changes.
- Runtime Swagger startup and snapshot retrieval succeeded with database,
  RabbitMQ, Redis, outbox, retention, SMTP and bootstrap workers disabled only
  for contract generation.
- The installed EF CLI is 8.0.22 while the runtime is 10.0.10; version alignment
  remains recommended before later migration authoring.
- Tests were neither added nor run, in accordance with the unified Final Test
  Phase in the implementation plan.
