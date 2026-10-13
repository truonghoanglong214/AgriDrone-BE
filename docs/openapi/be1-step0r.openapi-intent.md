# BE1 Step 0R — OpenAPI intent

- Date: 2026-10-08
- Status: `CodeComplete — AwaitingFinalTest`
- Runtime API added by Step 0R: none

Step 0R khóa business vocabulary, state model, persistence và integration
contracts cần cho các feature API ở những step sau. Tài liệu này ghi intent để
controller/OpenAPI tương lai không quay lại assumption `PricePerHa` hoặc yêu cầu
payment cho Baseline Mapping. Đây không phải generated Swagger snapshot và không
tuyên bố các route dưới đây đang hoạt động.

## Intended HTTP surface

| Route intent | Actor | Contract intent |
|---|---|---|
| `POST /api/system-manager/survey-orders/{orderId}/boundary-scope/verify` | Assigned eligible SystemManager | Approve/version FarmBoundary và scope; unresolved boundary exception không được trở thành official data. |
| `POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm` | Assigned eligible SystemManager | Snapshot positive `ConfirmedSurveyPoleCount`, FarmBoundary version và current published Farm base-map version trước khi resolve `PricePerPole`. |
| `POST /api/system-manager/survey-orders/{orderId}/disease-zones/{zoneId}/review` | Assigned eligible SystemManager | Review/correct proposed geometry and membership; select one applicable catalogue recommendation or reject all candidates with reason. |
| `POST /api/system-manager/survey-orders/{orderId}/health-results/approve` | Assigned eligible SystemManager | Approve reviewed findings/zones without exposing pending AI artifacts to TenantOwner. |
| `POST /api/system-manager/survey-orders/{orderId}/health-results/publish` | Assigned eligible SystemManager | Atomically publish SurveyResult, DiseaseZone/recommendation provenance, Plant current health, audit and outbox. |
| `GET /api/tenant-owner/farms/{farmId}/map` | Owning TenantOwner | Return only the current published Farm base map and official Plant mappings. |
| `GET /api/tenant-owner/survey-orders/{orderId}/result` | Owning TenantOwner | Return only published official results, published Disease Zones and selected expert-catalogue recommendation versions. |
| `POST /api/tenant-owner/farms/{farmId}/plant-inventory-change-reports` | Owning TenantOwner | Submit an idempotent pending removal/replacement/new-plant report; never mutate official Plant inventory directly. |
| `POST /api/system-manager/plant-inventory-change-reports/{reportId}/review` | Assigned eligible SystemManager | Record review/evidence decisions; a new plant still requires later-survey evidence before application. |

SystemAdmin catalogue/reassignment endpoints are separate administrative flows.
TenantOwner has no Mission, Drone, raw-AI, boundary-approval, map-publication or
direct Plant mutation endpoint.

## Schema intent

- Pricing uses positive `PricePerPole` in VND and positive
  `ConfirmedSurveyPoleCount`. `FinalPrice` is server-derived; target requests do
  not accept `PricePerHa` or derive pole count from area.
- Readiness is evaluated using a stable string `MissionPurpose`:
  `BASELINE_MAPPING`, `PLANT_HEALTH` or `HARVEST_READINESS`.
  Baseline Mapping does not require price/payment. Paid-service purposes require
  confirmed count, immutable price snapshot, confirmed paid appointment and
  verified payment.
- Geometry is RFC 7946 GeoJSON in WGS84 longitude/latitude order at the HTTP and
  integration boundary. EF Core and NetTopologySuite types never appear in API
  schemas.
- Boundary, Disease Zone, recommendation and plant-change lifecycle values are
  serialized as stable strings, not numeric enum ordinals.
- Mutation requests carry the expected resource version and the relevant
  idempotency key. Derived state such as `IsReady`, official publication status
  and calculated price is server-owned.
- Pending/raw/rejected artifacts are excluded from TenantOwner response schemas.

## Error intent

Business APIs use RFC 7807 Problem Details with stable `errorCode` extensions:

| HTTP status | Meaning |
|---:|---|
| `401` | Missing or invalid authentication. |
| `403` | Actor is authenticated but lacks the required role, assignment or ownership. |
| `404` | Resource is absent or intentionally hidden by the cross-tenant anti-enumeration policy. |
| `409` | Optimistic-version, idempotency or current-publication conflict. |
| `422` | Syntactically valid request violates a domain/business invariant. |

Stable code families locked by Step 0R include `SurveyOrder.*`,
`FarmBoundary.*`, `BoundaryException.*`, `DiseaseZone.*`,
`TreatmentRecommendation.*`, `PlantInventoryChange.*` and the existing
`MAPPING_*` publication codes. Human-readable messages may improve without
changing these machine-consumed codes.

## Integration and snapshot boundary

The V3 event routes remain messaging contracts, not HTTP endpoints:

- `mapping.baseline-candidates-ready.v3`;
- `mapping.farm-base-map-published.v3`;
- `health.analysis-ready.v3`.

V1/V2 event contracts and existing OpenAPI snapshots remain immutable. Generated
Swagger is updated only when the owning feature controllers are implemented in
later steps. Golden JSON, authorization/response tests and generated-snapshot
diffs remain gates in the Final Test Phase.
