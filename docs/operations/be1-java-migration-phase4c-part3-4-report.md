# BE1 Java Migration Phase 4C — Parts 3 and 4 Report

Date: 2026-10-02

Ordered implementation Steps 3 and 4 are complete. This slice implements the
tenant-selection token primitive and changes multi-tenant login behavior. It
does not add `POST /api/auth/select-tenant`; that endpoint remains Step 5 in the
implementation guide and 4C.4 in the higher-level migration checklist.

## Part 3 — tenant-selection token

- Added the `TenantSelectionTokenService` application output port and an RS256
  infrastructure implementation using the configured Java signing key.
- Tokens use the access-token issuer, the dedicated
  `<access-audience>.TenantSelection` audience and
  `purpose=tenant_selection`.
- Tokens contain `sub`, `jti`, `iat`, `nbf` and `exp`. TTL is configured by
  `BE1_JWT_TENANT_SELECTION_TOKEN_TTL` and defaults to five minutes.
- Validation requires the configured RSA key id and RS256 algorithm, exact
  issuer, selection audience, purpose, UUID subject and all temporal/id claims.
- Clock and token-id generation are injected, allowing deterministic tests.
- The normal resource-server validator rejects selection tokens, while the
  selection-token validator rejects normal access tokens.

## Part 4 — multi-tenant login

- SystemAdmin and SystemManager accounts continue to receive tenant-free
  system sessions.
- A user with no valid active tenant receives the stable no-membership error.
- A user with one valid active tenant receives an access token immediately.
- A user with multiple valid active tenants receives no access token and gets a
  five-minute selection token plus tenant options ordered by tenant name and id.
- Inactive or deleted tenants are excluded even when an active stale membership
  is returned by persistence.
- Successful-login state is recorded only after a valid session or selection
  payload has been issued.

## Verification

- Token tests cover round-trip claims and rejection of malformed, wrongly
  signed, wrong-issuer, wrong-audience, wrong-purpose, missing/invalid-subject
  and expired tokens.
- Login service tests cover all four branches, stale-tenant filtering, ordering
  and issuer failure before successful-login recording.
- Controller contract tests prove that the multi-tenant response contains no
  access token.
- Full `mvnw verify`: 75 unit tests and 26 integration tests passed with no
  failures or errors.
