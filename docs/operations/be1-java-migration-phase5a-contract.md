# BE1 Phase 5A — Farm and Zone API Contract

Status: **locked for implementation**

Contract version: **5A-v1**

Locked on: **2026-10-03**

This document is the source of truth for the Farm and Zone HTTP contract while
BE1 is migrated from .NET to Java. Domain, persistence, and controller code must
conform to this contract. Changing a field, route, status code, validation rule,
or error code requires updating this document, the DTOs, golden JSON fixtures,
and contract tests in the same change.

## 1. Compatibility decisions

- JSON uses camelCase.
- UUID values are JSON strings.
- Timestamps are ISO-8601 UTC strings.
- Decimal areas are JSON numbers expressed in hectares and must fit database
  precision `numeric(12,4)` (at most 8 integer and 4 fractional digits).
- `status` keeps the .NET numeric wire representation: `0 = Active`,
  `1 = Inactive`.
- GeoJSON objects keep their public shape (`type` and `coordinates`). They are
  not exposed as JTS or persistence types.
- Optional response properties are retained as explicit `null` values. This
  avoids silently changing the response shape during migration.
- Page numbering is one-based (`page >= 1`), and `size` is between 1 and 100.
- Archived Farms and Zones are excluded from active list/detail routes.
- Legacy Farm create and restore flows remain disabled and return
  `410 Gone` with `LegacyFlow.Disabled`.

## 2. HTTP routes

| Method | Route | Allowed actor/scope | Success | Request | Response |
|---|---|---|---:|---|---|
| GET | `/api/farms` | active TenantOwner; qualified assigned SystemManager, scoped | 200 | query: `pageNumber`, `pageSize` | `PageResponse<FarmListItemResponse>` |
| GET | `/api/farms/archived` | active TenantOwner | 200 | query: `pageNumber`, `pageSize` | `PageResponse<ArchivedFarmResponse>` |
| GET | `/api/farms/{farmId}/archived` | active TenantOwner | 200 | — | `ArchivedFarmResponse` |
| GET | `/api/farms/{farmId}` | active TenantOwner or qualified primary assigned SystemManager | 200 | — | `FarmDetailResponse` |
| PUT | `/api/farms/{farmId}` | qualified primary assigned SystemManager | 200 | `UpdateFarmRequest` | `UpdateFarmResponse` |
| PUT | `/api/farms/{farmId}/archive` | qualified primary assigned SystemManager | 204 | `ArchiveFarmRequest` | no body |
| GET | `/api/farms/{farmId}/zones` | active TenantOwner or qualified primary assigned SystemManager | 200 | — | `ZoneListItemResponse[]` |
| GET | `/api/farms/{farmId}/zones/{zoneId}` | active TenantOwner or qualified primary assigned SystemManager | 200 | — | `ZoneDetailResponse` |
| POST | `/api/farms/{farmId}/zones` | qualified primary assigned SystemManager | 201 | `CreateZoneRequest` | `CreateZoneResponse` plus `Location` header |
| PUT | `/api/farms/{farmId}/zones/{zoneId}` | qualified primary assigned SystemManager | 200 | `UpdateZoneRequest` | `UpdateZoneResponse` |
| PUT | `/api/farms/{farmId}/zones/{zoneId}/archive` | qualified primary assigned SystemManager | 204 | `ArchiveZoneRequest` | no body |
| POST | `/api/farms` | legacy gate; no target actor | 410 | legacy flow | `ApiErrorResponse` |
| PUT | `/api/farms/{farmId}/restore` | legacy gate; no target actor | 410 | legacy flow | `ApiErrorResponse` |

Route path IDs are authoritative. Request DTOs do not repeat `farmId` or
`zoneId`.

`TenantAdmin`, generic `TenantMember`, `FarmWorker`, `FarmMembership`, and
`ZoneAssignment` are not authorization sources in the target contract. Every
allowed actor is re-checked against current database state; a JWT claim alone
is insufficient. Unassigned and cross-tenant resources are masked as `404`.

## 3. Request validation

### UpdateFarmRequest

| Field | Rule |
|---|---|
| `name` | required, not blank, maximum 150 characters |
| `address` | optional, maximum 200 characters |
| `boundary` | optional valid GeoJSON Polygon |
| `centerPoint` | optional valid GeoJSON Point |
| `areaHectares` | optional, greater than or equal to zero, `numeric(12,4)` |
| `expectedVersion` | required, greater than zero |

### CreateZoneRequest

| Field | Rule |
|---|---|
| `code` | required, maximum 30 characters, `^[A-Za-z][A-Za-z0-9_]*$` |
| `name` | required, not blank, maximum 100 characters |
| `boundary` | optional valid GeoJSON Polygon |
| `areaHectares` | optional, greater than or equal to zero, `numeric(12,4)` |

### UpdateZoneRequest

`name`, `boundary`, and `areaHectares` use the same rules as create. Zone code
is immutable and therefore is not accepted. `expectedVersion` is required and
must be greater than zero.

### Archive requests

`ArchiveFarmRequest.expectedVersion` and
`ArchiveZoneRequest.expectedVersion` are required and must be greater than
zero.

### GeoJSON structural rules

- Point: `type` must equal `Point` (case-insensitive); coordinates must contain
  exactly longitude and latitude; both must be finite; longitude is within
  `[-180, 180]` and latitude within `[-90, 90]`.
- Polygon: `type` must equal `Polygon` (case-insensitive); at least one ring is
  required; each ring has at least four positions; every position contains
  exactly two finite, in-range coordinates; the first and last position of
  each ring must be equal.
- Structural validation belongs to the API boundary. Geometry validity,
  containment, overlap, and other spatial business rules belong to Phase 5B.

Validation failures use HTTP `422` and error code `Validation.Failed`.

## 4. Response fields

### Farm responses

- `FarmListItemResponse`: `id`, `tenantId`, `code`, `name`, `address`,
  `boundary`, `centerPoint`, `areaHectares`, `status`, `createdAt`, `createdBy`.
- `FarmDetailResponse`: all list fields plus `updatedAt`, `version`.
- `ArchivedFarmResponse`: detail fields plus `archivedAt`.
- `UpdateFarmResponse`: `id`, `tenantId`, `code`, `name`, `address`,
  `boundary`, `centerPoint`, `areaHectares`, `status`, `updatedAt`, `version`.

### Zone responses

- `ZoneListItemResponse`: `zoneId`, `farmId`, `code`, `name`, `boundary`,
  `areaHectares`, `status`, `version`, `createdAt`.
- `ZoneDetailResponse`: all list fields plus `createdBy`, `updatedAt`.
- `CreateZoneResponse`: detail fields without `updatedAt`.
- `UpdateZoneResponse`: `zoneId`, `farmId`, `code`, `name`, `boundary`,
  `areaHectares`, `status`, `version`, `updatedAt`.

## 5. Stable error contract

All failures use the shared `ApiErrorResponse` envelope.

| HTTP | Stable code | Meaning |
|---:|---|---|
| 400 | `Request.InvalidJson` | Request body cannot be parsed |
| 401 | `Authentication.Required` | No valid authenticated principal |
| 403 | `Farm.AccessDenied` | Farm access is forbidden |
| 403 | `FarmZone.AccessDenied` | Zone access is forbidden |
| 404 | `Farm.NotFound` | Farm is absent or not visible to the caller |
| 404 | `FarmZone.NotFound` | Zone is absent or not visible to the caller |
| 409 | `Farm.FarmCodeAlreadyExist` | Farm code conflicts; spelling is retained for compatibility |
| 409 | `FarmZone.CodeAlreadyExists` | Zone code conflicts within its Farm |
| 409 | `Farm.ConcurrentUpdate` | Farm optimistic version conflict |
| 409 | `FarmZone.ConcurrentUpdate` | Zone optimistic version conflict |
| 409 | `Farm.ActiveDependenciesExist` | Farm cannot be archived yet |
| 409 | `FarmZone.ActiveDependenciesExist` | Zone cannot be archived yet |
| 409 | `FarmZone.BoundaryOverlaps` | Zone overlaps another active Zone |
| 410 | `LegacyFlow.Disabled` | Farm create or restore flow is disabled |
| 422 | `Validation.Failed` | Request validation failed |
| 422 | `Farm.InvalidBoundary` | Farm boundary violates a spatial rule |
| 422 | `Farm.InvalidCenterPoint` | Farm center violates a spatial rule |
| 422 | `Farm.CenterPointOutsideBoundary` | Farm center is outside its boundary |
| 422 | `Farm.InvalidArea` | Farm area violates a business rule |
| 422 | `Farm.FarmNotFound` | Referenced Farm was not found during a mutation |
| 422 | `FarmZone.InvalidBoundary` | Zone boundary violates a spatial rule |
| 422 | `FarmZone.InvalidArea` | Zone area violates a business rule |
| 422 | `FarmZone.BoundaryOutsideFarm` | Zone boundary is outside its Farm |

Tenant isolation deliberately maps cross-tenant reads to the same `404` codes
as missing resources so that resource existence is not disclosed.

## 6. Executable contract assets

- Java DTOs and GeoJSON validators:
  `src/main/java/com/agridrone/be1/farm/api/contract`
- Numeric wire values: `FarmWireValues`
- Stable error constants: `FarmErrorCodes` and `FarmZoneErrorCodes`
- Golden JSON: `src/test/resources/contracts/farm`
- Serialization and validation tests:
  `FarmContractSerializationTest` and `FarmRequestValidationTest`

Domain entities, repositories, Flyway migrations, application use cases, and
runtime Farm controllers are intentionally outside this contract-locking step.
