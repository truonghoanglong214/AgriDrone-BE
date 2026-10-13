# BE1 Step 0R — Code completion report

- Completed: 2026-10-08
- Scope: Step 0R / Phase 6R in `BE1-Core-Business-Implementation-Plan.md`
- Status: **CodeComplete — AwaitingFinalTest**
- Final status: **not Done**

## Delivered slices

1. Per-pole pricing and purpose-aware readiness
   - Added `PricePerPole`, `ConfirmedSurveyPoleCount`, immutable order pricing
     snapshots and the authoritative final-price formula.
   - Split readiness by Baseline Mapping versus paid Plant Health/Harvest
     Readiness purposes. Baseline Mapping does not require payment.
2. Versioned Farm boundaries
   - Added FarmBoundary versions, approval/supersession, BoundaryException review,
     WGS84/PostGIS invariants and guarded migration behavior.
3. Disease Zones and recommendations
   - Added versioned DiseaseZone geometry/membership review and the
     expert-validated TreatmentRecommendation catalogue with provenance.
4. Owner-reported inventory changes
   - Added `PlantInventoryChangeReport`, stable state/error codes, idempotency and
     the later-survey evidence gate for genuinely new plants.
5. Publication boundaries and V3 contracts
   - Extended the mapping transaction context and added the survey-result
     publication context.
   - Added independent V3 mapping/base-map/health DTOs, validators, descriptors,
     event types and consumer names while preserving V1/V2.
6. Documentation lock
   - Accepted ADR-0007 through ADR-0011.
   - Updated the canonical glossary and recorded OpenAPI intent without exposing
     unfinished runtime feature routes.

## Persistence artifacts

- `20261007120000_RebaselinePerPoleOrder`
- `20261007140000_AddVersionedFarmBoundaries`
- `20261007160000_AddDiseaseZonesAndRecommendations`
- `20261007180000_AddPlantInventoryChangeReports`

No migration belongs to 0R-6 because its specialized DbContexts compose tables
already owned by `AgriDroneSchemaDbContext`; its V3 additions are transport and
transaction-boundary code.

## Stable contracts and errors

- HTTP intent retains RFC 7807 Problem Details and the established
  `401/403/404/409/422` mapping.
- Stable business code families include `SurveyOrder.*`, `FarmBoundary.*`,
  `BoundaryException.*`, `DiseaseZone.*`, `TreatmentRecommendation.*` and
  `PlantInventoryChange.*`.
- Mapping publication retains stable `MAPPING_*` permanent/conflict codes.
- V3 routing identities are:
  `mapping.baseline-candidates-ready.v3`,
  `mapping.farm-base-map-published.v3` and `health.analysis-ready.v3`.
- V1/V2 payloads, validators and routing names remain unchanged.

## Verification evidence available now

- 0R-1 through 0R-4 reports retain their recorded unit, architecture,
  PostgreSQL/PostGIS migration and regression evidence from 2026-10-07.
- 0R-5 migration/model code and 0R-6 contracts/contexts compile in the full
  solution.
- Latest 0R-6 full-solution build completed with zero warnings and zero errors.
- Mapping and result publication EF models were constructed successfully with 15
  and 11 entities respectively.

This report does not reinterpret build success as final test evidence.

## Deferred Final Test gates

The following remain unchecked in section 27 of the unified plan:

- PlantInventoryChangeReport state-machine tests;
- mapping/result atomic publication and rollback tests;
- V3 golden JSON, round-trip, routing and V2 immutability tests;
- stable error/OpenAPI assumption tests;
- pending/raw artifact visibility architecture tests;
- fresh/upgrade/rollback migration regression and final release-suite rerun.

Step 0R may therefore hand implementation control to the next code checkpoint,
but it must stay `CodeComplete — AwaitingFinalTest`. Only the Final Test Phase may
promote it to `Done` after the complete release evidence is green.
