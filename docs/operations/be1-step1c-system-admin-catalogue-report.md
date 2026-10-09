# BE1 Step 1C — SystemAdmin survey catalogue report

- Date: 2026-10-09
- Scope: Step 1C SystemAdmin catalogue lifecycle
- Status: **CodeComplete — AwaitingFinalTest**

## Implemented boundary

- `GET /api/system-admin/survey-services` returns all service states and price
  history, including experimental and retired services.
- Separate use cases implement metadata update, activation, marking a service
  experimental and retirement.
- Every mutation requires an authenticated SystemAdmin actor, an expected
  PostgreSQL `xmin` version and an audit record with the request correlation ID.
- Activation requires an effective VND per-pole price at server time.
- Retired services reject metadata and price changes while remaining queryable
  for historical order/result resolution.
- Domain transitions and application failures use stable lifecycle,
  concurrency, not-found and active-price-required codes.
- Only the existing `PLANT_HEALTH` and `HARVEST_READINESS` service seeds remain;
  Baseline Mapping and follow-up behavior are not modelled as saleable services.

## Verification

- `dotnet build backend/AgriDrone.sln --no-restore`
- Result: succeeded with 0 warnings and 0 errors.
- Tests remain in the unified Final Test Phase and were not run here.

Step 1D versioned price creation is intentionally excluded from this
checkpoint.
