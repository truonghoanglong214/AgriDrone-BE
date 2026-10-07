# BE1 Step 0R-6 — V3 contracts and publication boundaries

Date: 2026-10-07

## Delivered

- Extended `MappingPublicationDbContext` so a mapping publication can atomically
  update Farm boundary/base-map state, Zone maps, Plants, plant-change reports,
  confirmed order pole count, Inbox, Outbox and audit records.
- Added `SurveyResultPublicationDbContext` for atomic SurveyResult,
  DiseaseZone/membership, recommendation provenance, Plant, Inbox, Outbox and
  audit changes.
- Registered both specialized contexts against the shared enum-aware Npgsql data
  source. The canonical schema/migration owner remains `AgriDroneSchemaDbContext`.
- Added `BaselineMappingCandidatesReadyV3`, `FarmBaseMapPublishedV3` and
  `PlantHealthAnalysisReadyV3` with RFC 7946 GeoJSON DTOs and stable string codes.
- Added V3 validators, schema/event descriptors, event types and consumer queue
  names. Existing V1/V2 DTOs, validators and routing values were not changed.

## Migration decision

No migration is required for 0R-6. The specialized contexts compose tables and
constraints already introduced by the Step 0R schema migrations; the V3 work is
transport/application-boundary code only. Creating an empty migration would blur
schema ownership and provide no deployable database change.

## Verification

- Full solution build: succeeded with zero warnings and zero errors.
- EF model construction smoke check: Mapping publication model = 15 entities;
  Survey result publication model = 11 entities.
- Contract limits: 10,000 mapping/result items and the existing 4 MiB envelope
  body limit.

Per the implementation plan, validator/golden JSON, routing compatibility and
PostgreSQL transaction tests are deferred to the Final Test Phase.
