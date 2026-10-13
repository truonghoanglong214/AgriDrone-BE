# BE1 Step 1B — Public survey catalogue report

- Date: 2026-10-09
- Scope: Step 1B public catalogue only
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- `GET /api/public/survey-services` is anonymous and dispatches through
  MediatR; the controller does not access EF Core or a repository.
- The application handler obtains the evaluation instant from `TimeProvider`.
  Clients cannot supply or override the catalogue time.
- The database projection returns only `Active` or `Experimental` services
  having exactly one effective per-pole VND price at that server instant.
- Retired services, services without an effective price, legacy per-hectare
  prices and unsupported currencies are excluded.
- Results are ordered by stable service code.
- The public response contains service display metadata, service type, an
  explicit `isExperimental` signal and the indicative price window. It does
  not expose the internal lifecycle status, row version, audit data, actor
  identifiers or AI configuration.

`HARVEST_READINESS` may remain visible while experimental, but the contract
marks it explicitly and the endpoint documentation states that this does not
mean the capability has been validated.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`
- Result: succeeded with 0 warnings and 0 errors.
- The test backlog remains in the unified Final Test Phase. No test suite was
  written or executed during Step 1B.

No schema change or migration is required for this read-only phase. Steps
1C–1E remain open and are not included in this checkpoint.
