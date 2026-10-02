# BE1 Java Migration Phase 4C — Parts 1 and 2 Report

Date: 2026-10-02

The first two ordered Phase 4C slices are complete. This change intentionally
does not add tenant-selection token behavior, new controllers or lifecycle use
cases; those start at Step 3.

## Part 1 — contract foundation

- Added request/response records for tenant selection, tenant lifecycle/read
  models, Owner provisioning and invitation preview/accept.
- Extended the login contract so `session` and `tenantSelection` can represent
  the two mutually exclusive successful login outcomes.
- Aligned the paged envelope with .NET fields: `items`, `pageNumber`, `pageSize`,
  `totalCount`, `totalPages`, `hasPreviousPage` and `hasNextPage`.
- Pagination is one-based, defaults to page 1/size 20 and caps size at 100.
- Added the stable tenant, invitation and tenant-selection error-code constants.
- Added serialization and validation contract tests for field names, numeric
  role/status wire values, pagination metadata and error codes.

## Part 2 — persistence foundation

- Tenant repository now supports explicit inactive lookup, normalized-code
  existence checks and deterministic paged projection reads.
- Membership repository now supports any-status lookup and deterministic active
  membership projection pages by user.
- Invitation persistence now maps Owner role/purpose, supports unlocked preview
  lookup, pessimistic locked acceptance lookup, pending Owner provisioning
  lookup and save/update for expiry transitions.
- Read models use JPA constructor projections rather than loading aggregate
  collections and paging in memory.
- The existing V1 schema already contains the required unique partial indexes;
  no Flyway migration was added or modified.

## Verification

- `Phase4cContractTest`: 4 passed.
- `IdentityRepositoryIT`: 8 passed against Postgres/PostGIS Testcontainers.
- Repository tests cover inactive tenant reads, one-based paging/order,
  normalized code lookup, inactive membership lookup/filtering, pending Owner
  uniqueness, invitation expiry and a real blocking pessimistic row lock.
- Full `mvnw verify`: 65 unit tests and 26 integration tests passed with no
  failures or errors.
