# BE1 Java Migration Phase 0 — endpoint inventory

Baseline: `GET /swagger/v1/swagger.json` from the mixed .NET API after all EF migrations were applied on 2026-09-28. The immutable snapshot is `docs/openapi/be1-java-migration-phase0.openapi.json` (SHA-256 `d334e10a3360f82328826afe074071771d12c5cf707fa6ce40a16b1f59eefdc5`). It contains 63 operations.

Disposition meanings:

- `BE1_PORT`: preserve the public behavior in Java, subject to parity tests.
- `BE1_REAUTHORIZE`: preserve the business read/update capability, but replace legacy TenantAdmin/Member policy with the target TenantOwner/SystemManager actor model.
- `BE2_KEEP`: remains in the .NET BE2 deployment and is not implemented by Java BE1.
- `SPLIT_OPS`: Java owns BE1 messaging operations; .NET exposes separate BE2-only operations.
- `LEGACY_GONE`: retain only the stable compatibility `410` during route cutover; do not port its handler to Java.
- `BE2_REDESIGN`: the exact legacy mutation remains disabled; any replacement belongs to BE2 and must use the target SurveyOrder-driven flow.

| # | Method | Route | Current actor/policy | Owner and disposition |
|---:|---|---|---|---|
| 1 | POST | `/api/auth/login` | Anonymous | BE1 — `BE1_PORT` |
| 2 | POST | `/api/auth/forgot-password` | Anonymous | BE1 — `BE1_PORT` |
| 3 | POST | `/api/auth/reset-password` | Anonymous | BE1 — `BE1_PORT` |
| 4 | POST | `/api/auth/select-tenant` | Anonymous with selection token | BE1 — `BE1_PORT` |
| 5 | GET | `/api/farms` | TenantAdmin | BE1 — `BE1_REAUTHORIZE` |
| 6 | POST | `/api/farms` | TenantAdmin; deprecated/gated | BE1 compatibility — `LEGACY_GONE` |
| 7 | GET | `/api/farms/archived` | TenantOwner | BE1 — `BE1_PORT` |
| 8 | GET | `/api/farms/{farmId}/archived` | TenantOwner | BE1 — `BE1_PORT` |
| 9 | GET | `/api/farms/{farmId}` | TenantMember | BE1 — `BE1_REAUTHORIZE` |
| 10 | PUT | `/api/farms/{farmId}` | SystemManager | BE1 — `BE1_PORT` |
| 11 | POST | `/api/farms/{farmId}/zones` | SystemManager | BE1 — `BE1_PORT` |
| 12 | GET | `/api/farms/{farmId}/zones` | TenantMember | BE1 — `BE1_REAUTHORIZE` |
| 13 | GET | `/api/farms/{farmId}/zones/{zoneId}` | TenantMember | BE1 — `BE1_REAUTHORIZE` |
| 14 | PUT | `/api/farms/{farmId}/zones/{zoneId}` | SystemManager | BE1 — `BE1_PORT` |
| 15 | PUT | `/api/farms/{farmId}/zones/{zoneId}/archive` | SystemManager | BE1 — `BE1_PORT` |
| 16 | PUT | `/api/farms/{farmId}/archive` | SystemManager | BE1 — `BE1_PORT` |
| 17 | PUT | `/api/farms/{farmId}/restore` | TenantOwner; deprecated/gated | BE1 compatibility — `LEGACY_GONE` |
| 18 | POST | `/api/system/messaging/outbox/{messageId}/redrive` | SystemAdmin | BE1 and BE2 operations — `SPLIT_OPS` |
| 19 | POST | `/api/system/messaging/dead-letters/{consumerName}/redrive` | SystemAdmin | BE1 and BE2 operations — `SPLIT_OPS` |
| 20 | POST | `/api/missions/{missionId}/farms/{farmId}/media` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 21 | GET | `/api/missions/{missionId}/farms/{farmId}/media` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 22 | GET | `/api/missions/{missionId}/farms/{farmId}/media/{mediaId}` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 23 | GET | `/api/missions/{missionId}/farms/{farmId}/media/{mediaId}/download-url` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 24 | POST | `/api/farms/{farmId}/missions` | Authenticated; deprecated/gated | BE2 — `BE2_REDESIGN` |
| 25 | PATCH | `/api/farms/{farmId}/missions/{missionId}/schedule` | Authenticated; deprecated/gated | BE2 — `BE2_REDESIGN` |
| 26 | PATCH | `/api/farms/{farmId}/missions/{missionId}/status` | Authenticated; deprecated/gated | BE2 — `BE2_REDESIGN` |
| 27 | GET | `/api/farms/{farmId}/missions/{missionId}` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 28 | POST | `/api/missions/{missionId}/farms/{farmId}/telemetry/imports` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 29 | POST | `/api/missions/{missionId}/farms/{farmId}/telemetry/normalize` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 30 | POST | `/api/missions/{missionId}/farms/{farmId}/upload/finalize` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 31 | GET | `/api/missions/{missionId}/farms/{farmId}/upload/readiness` | Authenticated + farm/mission access | BE2 — `BE2_KEEP` |
| 32 | GET | `/api/catalog/health-levels` | Authenticated | BE1 — `BE1_PORT` |
| 33 | GET | `/api/catalog/plant-conditions` | Authenticated | BE1 — `BE1_PORT` |
| 34 | GET | `/api/system/drones` | SystemAdmin | BE2 — `BE2_KEEP` |
| 35 | POST | `/api/system/drones` | SystemAdmin | BE2 — `BE2_KEEP` |
| 36 | PATCH | `/api/system/drones/{droneId}/status` | SystemAdmin | BE2 — `BE2_KEEP` |
| 37 | PUT | `/api/system/farms/{farmId}/primary-manager` | SystemAdmin | BE1 — `BE1_PORT` |
| 38 | PUT | `/api/system/farms/{farmId}/primary-manager/end` | SystemAdmin | BE1 — `BE1_PORT` |
| 39 | GET | `/api/system-manager/farms/{farmId}/drones/available` | SystemManager | BE2, with BE1 assignment decision — `BE2_KEEP` |
| 40 | POST | `/api/auth/system-manager-invitations/preview` | Anonymous | BE1 — `BE1_PORT` |
| 41 | POST | `/api/auth/system-manager-invitations/accept` | Anonymous | BE1 — `BE1_PORT` |
| 42 | POST | `/api/system/managers` | SystemAdmin | BE1 — `BE1_PORT` |
| 43 | PUT | `/api/system/managers/{profileId}/activate` | SystemAdmin | BE1 — `BE1_PORT` |
| 44 | PUT | `/api/system/managers/{profileId}/suspend` | SystemAdmin | BE1 — `BE1_PORT` |
| 45 | PUT | `/api/system/managers/{profileId}/availability` | SystemAdmin | BE1 — `BE1_PORT` |
| 46 | PUT | `/api/system/managers/{profileId}/qualification` | SystemAdmin | BE1 — `BE1_PORT` |
| 47 | POST | `/api/system/managers/invitations` | SystemAdmin | BE1 — `BE1_PORT` |
| 48 | GET | `/api/system-manager/farms` | SystemManager | BE1 — `BE1_PORT` |
| 49 | POST | `/api/system/plant-conditions` | SystemAdmin | BE1 — `BE1_PORT` |
| 50 | POST | `/api/system/plant-conditions/{conditionId}/versions` | SystemAdmin | BE1 — `BE1_PORT` |
| 51 | PUT | `/api/system/plant-conditions/{conditionId}/retire` | SystemAdmin | BE1 — `BE1_PORT` |
| 52 | POST | `/api/system/tenants` | SystemAdmin | BE1 — `BE1_PORT` |
| 53 | PUT | `/api/system/tenants/{tenantId}/activate` | SystemAdmin | BE1 — `BE1_PORT` |
| 54 | PUT | `/api/system/tenants/{tenantId}/deactivate` | SystemAdmin | BE1 — `BE1_PORT` |
| 55 | POST | `/api/system/tenants/{tenantId}/owner-provisionings` | SystemAdmin | BE1 — `BE1_PORT` |
| 56 | GET | `/api/system/tenants/all` | SystemAdmin | BE1 — `BE1_PORT` |
| 57 | GET | `/api/system/users/{userId}/tenants` | SystemAdmin | BE1 — `BE1_PORT` |
| 58 | POST | `/api/auth/invitations/preview` | Anonymous | BE1 — `BE1_PORT` |
| 59 | POST | `/invitations/accept` | Anonymous | BE1 — `BE1_PORT` (preserve absolute legacy path during compatibility) |
| 60 | POST | `/api/tenants/current/transfer-ownership` | TenantOwner; deprecated/gated | BE1 compatibility — `LEGACY_GONE` |
| 61 | GET | `/api/users` | SystemAdmin | BE1 — `BE1_PORT` |
| 62 | PUT | `/current/profile` | Authenticated | BE1 — `BE1_PORT` (preserve absolute legacy path during compatibility) |
| 63 | PUT | `/current/change-password` | Authenticated | BE1 — `BE1_PORT` (preserve absolute legacy path during compatibility) |

## Negative inventory — do not port to Java

- Handlers behind the four BE1 `LEGACY_GONE` routes: direct Farm create, direct Farm restore, Tenant ownership transfer, plus their old authorization path.
- TenantAdmin/Member mutation and Farm Manager/Worker/FarmMembership/ZoneAssignment flows. Current farm reads marked `BE1_REAUTHORIZE` are capabilities to retain, not permission models to copy.
- The three gated direct Mission mutations. Mission creation/scheduling/lifecycle remains BE2-owned and must be driven by the target SurveyOrder integration, not copied into Java.
- Public self-registration, FieldTask, Harvest Management, tenant-owned Drone semantics and any controller/domain type already asserted absent by `LegacyEndpointSafetyTests`.
- No Survey business controller exists in this baseline. The catalogue query in Core Step 1A remains runtime foundation and must not be reported as a completed public API.
