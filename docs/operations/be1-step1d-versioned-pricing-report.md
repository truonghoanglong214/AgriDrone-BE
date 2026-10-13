# BE1 Step 1D — Versioned per-pole pricing report

- Date: 2026-10-09
- Scope: Step 1D versioned pricing
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- `POST /api/system-admin/survey-services/{serviceId}/prices` creates a new
  positive VND `PricePerPole` version with a UTC effective window.
- `GET /api/system-admin/survey-services/{serviceId}/prices` returns immutable
  per-pole and legacy per-hectare price history for administration and support.
- The command requires an authenticated SystemAdmin actor and the expected
  survey-service `xmin` version.
- A replacement closes the one open price window at the new version's
  `EffectiveFrom`, flushes that closure, then inserts the new version in the
  same database transaction and commit. Historical amounts are never edited;
  any failure rolls the closure back with the insertion and audit record.
- Application overlap detection provides an early stable conflict response;
  PostgreSQL exclusion constraint `ex_survey_service_prices_no_overlap` remains
  the race-condition authority and maps to `SurveyService.PriceWindowOverlap`.
- Audit data captures the previous current window and the newly created version,
  including the closed price identifier.
- Retired services reject new price versions. Retroactive effective starts are
  rejected against server time.

## Migration

`20261009120000_AllowClosingReferencedSurveyServicePrices` changes the existing
immutability trigger narrowly: a referenced, open price may have only
`EffectiveTo` set once. Its identifier, service, amount, pricing mode, currency,
start time and creation provenance remain immutable, and deletion remains
blocked.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`
- Result: succeeded with 0 warnings and 0 errors.
- `dotnet ef migrations has-pending-model-changes` reports no model changes
  missing from the migration baseline.
- Concurrency, migration upgrade/fresh-database and transaction rollback tests
  remain in the unified Final Test Phase and were not run here.

Step 1E business master data remains open and is not included in this
checkpoint.
