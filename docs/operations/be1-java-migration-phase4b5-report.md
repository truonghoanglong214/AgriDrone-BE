# BE1 Java Migration Phase 4B.5 Report

Date: 2026-10-02

Phase 4B.5 is complete. BE1 Java owns the RSA/RS256 access-token issuer and
publishes its public key as JWKS. BE2 now supports either its legacy HMAC secret
or a remote JWKS signing source, with exactly one source required at startup.
JWKS mode restricts accepted tokens to RS256 and keeps strict issuer, audience,
lifetime and signing-key validation.

## Contract fixture

`contracts/examples/auth/java-issued-access-token.v1.json` is the canonical
cross-runtime fixture. A Java test creates the token with the production
`JwtTokenIssuer` using a fixed clock, key id and token id, then asserts exact
equality with the fixture. The fixture contains only a test public key/JWKS and
a token signed by the repository's test-only private key.

The BE2 compatibility tests load that fixture and verify:

- JWKS parsing and `kid` key selection;
- RS256 signature, issuer, audience and lifetime;
- `sub`, `tenant_id`, `tenant_membership_id` and `tenant_role` claims;
- the `SYSTEM_ADMIN` role mapping; and
- rejection when the configured audience is wrong.

## Runtime configuration

BE2 retains the shared-secret mode for rollback compatibility. To cut over BE2
verification to BE1 Java, set the same issuer/audience on both services, clear
`BE2_JWT_SECRET`, set `BE2_JWT_JWKS_URI` to the Java JWKS endpoint and keep HTTPS
metadata required outside the local Compose network. The repository
`.env.example` contains both the rollback defaults and the commented cutover
values.

## Verification

- BE1 Java focused issuer/golden-contract tests pass.
- BE1 Java full `mvnw verify`: 59 unit tests and 22 integration tests pass.
- BE2 focused Java JWT compatibility tests: 3 passed.
- BE2 full unit suite in the .NET SDK container: 439 passed, 0 failed, 0 skipped.
- BE2 Docker image builds and publishes successfully.

The Java-issued JWT -> BE2 verification exit gate is therefore closed.
