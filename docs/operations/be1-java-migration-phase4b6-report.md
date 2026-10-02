# BE1 Java Migration Phase 4B.6 Report

Date: 2026-10-01

Phase 4B.6 is implemented on BE1 Java. The Identity module now exposes login,
forgot-password and reset-password use cases under separate `api`, `application`
and `infrastructure` packages. Authentication errors use stable codes, reset
tokens are stored as SHA-256 hashes, and password reset delivery uses the SMTP
adapter when `BE1_SMTP_ENABLED=true`.

The login response issues an RSA-signed access token through the Phase 4B.5
issuer adapter and includes tenant membership claims when the user has exactly
one active membership. Users with multiple active memberships remain blocked by
the stable tenant-selection error until the 4C selection flow is implemented.

## Verification

- `mvn -q test` passes, including service, security/execution-context and API
  contract tests.
- Swagger UI is available at `/swagger-ui/index.html`; the raw OpenAPI document
  is available at `/v3/api-docs`. Both routes are public documentation routes,
  including when JWT resource-server security is enabled.
- `mvn -q verify` completes without test failures. Testcontainers integration
  tests are skipped when Docker Desktop is unavailable.

## Remaining Phase 4 work

- 4C TenantOwner invitation, tenant selection and tenant lifecycle/read models.
- 4D SystemManager invitation/profile/qualification/availability and primary Farm
  assignment/access flow.

The cross-runtime JWT gate was completed on 2026-10-02 and is recorded in
`be1-java-migration-phase4b5-report.md`.
