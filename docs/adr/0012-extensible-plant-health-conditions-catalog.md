# ADR-0012: Extensible Plant Health Conditions Catalog and Metadata Management

- Status: Accepted
- Date: 2026-11-03
- Decision owners: Product, Backend 1, AI/GIS
- Amends: ADR-0010 (plant-health-team-labels-and-sourced-guidance)

## Context

In ADR-0010, the Plant Health label set was locked to exactly two suspected disease classes (`BROWN_SPOT`, `ANTHRACNOSE`) and two confusing non-disease classes (`SUNBURN`, `MECHANICAL_SCAR`) under the pilot dataset `AGRIDRONE_PILOT_DATASET_V1`. This was enforced via database check constraints (`ck_plant_conditions_health_label_code`), strict entity normalization, and startup validation that rejected any additional active condition codes.

While this restriction prevented arbitrary label proliferation during the initial pilot phase, it blocked administrators from:
1. Registering new plant diseases (e.g., `STEM_CANKER`, `MOSAIC_VIRUS`, `RUST`) as orchard management requirements evolved.
2. Editing existing disease descriptions and metadata directly without schema modifications.
3. Specifying updated dataset or annotation rule versions per condition.

## Decision

1. **Remove Code Lock:**
   - Drop the PostgreSQL check constraint `ck_plant_conditions_health_label_code` on table `plant.plant_conditions`.
   - Any valid uppercase alphanumeric condition code is permitted as either a `SuspectedDisease` or `ConfusingNonDisease`.
   - The original four pilot labels remain seeded as the baseline supported label set.

2. **Permissive Metadata and Flexible Provenance:**
   - Remove hardcoded string equality checks against `AGRIDRONE_PILOT_DATASET_V1` and `AGRIDRONE_ANNOTATION_RULES_V1`.
   - Allow custom dataset versions, annotation rule versions, and team limitations. If omitted during creation, sensible system defaults apply.
   - Retain integrity constraints ensuring that `SuspectedDisease` labels use condition type `DISEASE`, while `ConfusingNonDisease` labels use non-disease condition types (`ABIOTIC_DAMAGE`, `MECHANICAL_DAMAGE`, etc.).

3. **In-place and Versioned Condition Updates:**
   - Support revision creation via `POST /api/system/plant-conditions/{conditionId}/versions` for historical audit tracking.
   - Provide in-place metadata updates via `PUT /api/system/plant-conditions/{conditionId}` allowing System Admins to update `Name`, `ScientificName`, and `Description` with optimistic concurrency protection (`ExpectedVersion`).

4. **Startup Validation Evolution:**
   - Update `ValidateCoreMasterDataAsync` so it validates that all baseline labels exist and are active, while allowing an arbitrary number of additional active conditions without raising unexpected-label startup errors.

## Consequences

- Administrators can create, update, and retire plant disease conditions dynamically via the API without requiring database migrations or code changes.
- Existing historical scans and sourced guidance remain safely linked to specific condition IDs and revision numbers.
- Automated tests and health checks verify both baseline seed completeness and extensible condition creation.
