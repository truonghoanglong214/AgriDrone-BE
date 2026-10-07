# BE1 Step 0R — Locked business and V3 contract

- Status: Approved design baseline
- Date: 2026-10-07
- Implements: ADR-0007 through ADR-0011
- Implementation status: InProgress; PR 0R-1 implements the per-pole value objects,
  two-stage SurveyOrder model, appointment purpose and purpose-aware readiness.
  PR 0R-2 adds the fresh/upgrade/guarded-rollback persistence migration. PR 0R-3
  implements versioned FarmBoundary, BoundaryException review, PostGIS Zone
  invariants and its guarded migration. PR 0R-4 implements versioned DiseaseZone
  proposals/membership review and the expert-validated TreatmentRecommendation
  catalogue with guarded persistence. PR 0R-5 implements the owner-reported
  `PlantInventoryChangeReport` lifecycle, idempotency/open-report constraints,
  later-survey evidence gate and guarded persistence. PR 0R-6 adds the independent
  V3 mapping/health
  contracts, validators, descriptors/routing identities, and the mapping/result
  publication transaction contexts. Feature consumers/processors remain owned by
  Steps 6 and 7; golden JSON remains owned by the Final Test Phase.

## 1. Canonical business primitives

| Primitive | Contract |
|---|---|
| `PricePerPole` | Positive `decimal(18,2)`, currency `VND` in MVP |
| `ConfirmedSurveyPoleCount` | Positive integer; count of active, in-scope, boundary-resolved pole positions |
| `FinalPrice` | `Round(ConfirmedSurveyPoleCount × PricePerPole, 2, AwayFromZero)` |
| `AppointmentPurpose` | `BaselineMapping`, `PaidService` |
| `MissionPurpose` | `BaselineMapping`, `PlantHealth`, `HarvestReadiness` |
| `FarmBoundaryStatus` | `Draft`, `Approved`, `Rejected`, `Superseded` |
| `BoundaryExceptionState` | `OutOfBoundary`, `NeedsReview`, `Resolved` |
| `BoundaryExceptionDecision` | `AcceptedInside`, `RejectedOutside`, `LocationCorrected` |
| `DiseaseZoneStatus` | `Proposed`, `Reviewed`, `Published`, `Rejected`, `Superseded` |
| `PlantInventoryChangeKind` | `Removed`, `Replaced`, `NewPlant` |
| `PlantInventoryChangeStatus` | `Submitted`, `UnderReview`, `AwaitingSurveyEvidence`, `Verified`, `Applied`, `Rejected`, `Withdrawn` |

All timestamps are UTC `DateTimeOffset`. All mutable business resources carry an explicit version/expected-version guard. Clients invoke named commands and never send derived readiness/publication state.

## 2. SurveyOrder transition table

| From | Source of truth | To |
|---|---|---|
| creation | approved SurveyRequest orchestration | `PendingBoundaryVerification` |
| `PendingBoundaryVerification` | assigned manager verifies boundary/scope; Farm has no published map | `AwaitingBaselineAppointment` |
| `PendingBoundaryVerification` | assigned manager verifies boundary/scope and current published map/count | `AwaitingPricing` |
| `AwaitingBaselineAppointment` | Baseline appointment confirmed | `BaselineReady` |
| `BaselineReady` | BE2 starts Baseline Mapping Mission after readiness re-check | `BaselineInProgress` |
| `BaselineInProgress` | candidates imported successfully | `AwaitingBaselineReview` |
| `AwaitingBaselineReview` | atomic map publication confirms active-pole count | `AwaitingPricing` |
| `AwaitingPricing` | manager confirms price version, pole count and immutable snapshot | `AwaitingPaidAppointment` |
| `AwaitingPaidAppointment` | PaidService appointment confirmed | `AwaitingPayment` |
| `AwaitingPayment` | verified payment and full paid readiness pass | `ReadyForPaidService` |
| `ReadyForPaidService` | BE2 starts selected paid Mission after readiness re-check | `InProgress` |
| `InProgress` | analysis handoff imported and operational execution ends | `PendingReview` |
| `PendingReview` | official result publication commits | `Completed` |
| any eligible pre-start state | authorized cancellation | `Cancelled` |

No `Cancelled` transition is allowed after the relevant Mission has started. Failure/abort/compensation is modeled separately and preserves work history.

## 3. Readiness decisions

### Baseline Mapping

Required:

- order expects Baseline Mapping and is `BaselineReady`;
- approved FarmBoundary and verified scope;
- confirmed active `BaselineMapping` appointment;
- assigned active, available and flight-qualified primary SystemManager;
- mission/drone/pre-flight safety gates.

Not required: price snapshot or payment.

### Paid service

Required:

- order is `ReadyForPaidService`;
- approved FarmBoundary and published Farm base map;
- confirmed active-pole count and immutable `PricePerPole` snapshot;
- confirmed active `PaidService` appointment;
- verified current payment for the order amount/currency;
- eligible assigned manager;
- no pending price adjustment;
- mission/drone/pre-flight safety gates.

## 4. `SurveyOrderOperationalContextV3`

Synchronous/internal query response:

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `SurveyOrderId` | Guid | yes | Order identity |
| `TenantId`, `FarmId` | Guid | yes | Ownership boundary |
| `ServiceCode`, `ServiceType` | string | yes | Selected paid service |
| `RequestedMissionPurpose` | string | yes | Purpose being evaluated |
| `OrderStatus`, `OrderVersion` | string, uint | yes | Authoritative order state |
| `FarmBoundaryVersionId` | Guid? | conditional | Required after boundary approval |
| `FarmBaseMapVersionId` | Guid? | conditional | Required for paid purpose |
| `RequiresBaselineMapping` | bool | yes | Whether prerequisite mapping remains required |
| `ConfirmedSurveyPoleCount` | int? | conditional | Required after count confirmation |
| `SurveyServicePriceId` | Guid? | conditional | Resolved effective price version |
| `PricePerPole`, `Currency`, `FinalPrice` | decimal?, string?, decimal? | conditional | Immutable pricing snapshot |
| `AppointmentId`, `AppointmentPurpose` | Guid?, string? | conditional | Appointment for requested purpose |
| `AppointmentStartAt`, `AppointmentEndAt`, `AppointmentStatus` | UTC timestamps/string | conditional | Current purpose-specific appointment |
| `PaymentId`, `PaymentStatus` | Guid?, string? | conditional | Paid purpose only |
| `PrimarySystemManagerId` | Guid? | conditional | Current primary assignment |
| `PreviousCompatibleOrderId` | Guid? | no | Same-service longitudinal comparison |
| `IsReady` | bool | yes | Server decision for requested purpose |
| `ReadinessFailures` | string[] | yes | Stable failure codes; empty when ready |
| `EvaluatedAt` | UTC timestamp | yes | Server evaluation time |

## 5. V3 integration events

All events use the existing integration envelope with `schemaVersion = 3`. Business payload IDs are non-empty. Geometry is GeoJSON Polygon/Point in WGS84 longitude/latitude order.

### 5.1 `BaselineMappingCandidatesReadyV3`

Event type: `mapping.baseline-candidates-ready.v3`; direction: BE2 → BE1.

Header fields:

- `CausationId`, `HandoffId`, `SurveyOrderId`, `MissionId`, `FarmId`;
- `FarmBoundaryVersionId`, optional expected current Farm base-map version;
- `JobId`, model/algorithm version, boundary policy version;
- `ProducedAt` UTC;
- Zone batches with ZoneId, grid/spacing parameters and pending candidates.

Each candidate contains candidate ID, source observation/evidence IDs, original GeoJSON Point, row/column proposal, confidence, boundary classification, measured boundary distance, threshold, optional matched PlantId and match confidence. `OutOfBoundary` or `NeedsReview` candidates cannot be auto-published.

### 5.2 `FarmBaseMapPublishedV3`

Event type: `mapping.farm-base-map-published.v3`; direction: BE1 → BE2.

Fields:

- `CausationId`, `PublicationId`, `SurveyOrderId`, `SourceMissionId`, `FarmId`;
- `FarmBoundaryVersionId`, `FarmBaseMapVersionId`, version number;
- `ConfirmedSurveyPoleCount`, confirmer ID and `PublishedAt` UTC;
- Zone map versions and official active Plant mappings;
- applied plant-change decision references when the publication/amendment consumed them.

The confirmed count must equal the count of official active in-scope mappings in the committed publication projection.

### 5.3 `PlantHealthAnalysisReadyV3`

Event type: `health.analysis-ready.v3`; direction: BE2 → BE1.

Header fields:

- `CausationId`, `HandoffId`, `SurveyOrderId`, `MissionId`, `FarmId`;
- `FarmBoundaryVersionId`, `FarmBaseMapVersionId`;
- `JobId`, model version, threshold profile/policy version and `ProducedAt` UTC.

Payload contains:

- immutable health observations referencing official PlantId where matched;
- unresolved/new candidate references without silently creating Plant profiles;
- boundary exceptions with original position, classification, distance and policy version;
- proposed Disease Zones as GeoJSON Polygons with proposed Plant membership and membership version;
- recommendation candidate IDs/versions from the expert catalogue;
- evidence media references, never unrestricted treatment text.

## 6. Authorization and publication rules

- Assigned, active and flight-qualified SystemManager owns operational review decisions.
- SystemAdmin owns catalogue publication and assignment/reassignment; SystemAdmin does not bypass assignment to approve operational output.
- TenantOwner sees only approved/published boundary, base map, Disease Zones, recommendations and results. The owner may see status of their own plant-change reports but not raw AI artifacts.
- Pending/rejected/raw artifacts never contribute to official inventory, confirmed count or published result.

## 7. Compatibility and validation

- V1/V2 DTOs, event types, descriptors, routing and golden JSON remain unchanged.
- Target writes use only per-pole pricing. Legacy per-hectare fields are read-only compatibility data and are never auto-converted.
- V3 payload limit is 10,000 items and 4 MiB. Oversized payload is a permanent validation failure requiring producer-side scope/job splitting.
- Required verification: validator tests, golden JSON, round trip, version-routing, V2 immutability, fresh/upgrade migration, rollback, purpose-aware readiness, pending-data visibility and PostgreSQL transaction tests.
