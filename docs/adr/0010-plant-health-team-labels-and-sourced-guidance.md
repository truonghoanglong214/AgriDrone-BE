# ADR-0010: Team-labelled Plant Health classes and sourced guidance

- Status: Accepted (Amended by ADR-0012)
- Date: 2026-10-17
- Decision owners: Product, Backend 1, AI/GIS
- Supersedes: treatment-recommendation portion of ADR-0009
- Amended by: ADR-0012 (Extensible Plant Health Conditions Catalog and Metadata Management, 2026-11-03)

## Context

The pilot dataset does not support clinically meaningful fixed severity levels or
an expert-diagnosis claim. The MVP still needs a stable vocabulary for review,
historical provenance and cautious operational guidance.

## Decision

- The baseline label set initially defined two suspected-disease classes,
  `BROWN_SPOT` and `ANTHRACNOSE`, plus the confusing/non-disease classes
  `SUNBURN` and `MECHANICAL_SCAR`.
  *(Note: Amended by ADR-0012 to allow extensible disease/condition registration beyond the initial four pilot classes).*
- Every target label carries dataset version, annotation-rule version
  and explicit limitation that it is team-labelled and not expert-confirmed.
- Target API, read model and lifecycle logic do not contain severity or fixed
  health-level fields.
- `SourcedGuidance` references one active suspected class revision and stores a
  stable code, immutable version number, source title/reference, guidance text,
  effective window and `Draft → Active → Retired` lifecycle.
- Only one active guidance window may apply to a suspected class at a time.
  Retired versions remain readable for historical resolution.
- Guidance is preliminary operational information, not diagnosis or treatment
  prescription. The API returns that claim boundary explicitly.
- Legacy `HealthLevel`, `TreatmentRecommendation` and Harvest Readiness artifacts
  remain in persistence only where required for existing data; they are not
  target runtime/API capabilities.

## Consequences

The database migration backfills the four pilot labels and enforces their kind,
type and provenance shape. With ADR-0012, database constraint `ck_plant_conditions_health_label_code`
was dropped and startup validation updated so new conditions can be dynamically added
and managed by System Administrators. Results must snapshot the class,
annotation/model and guidance versions they used so later retirement does not
rewrite history.
