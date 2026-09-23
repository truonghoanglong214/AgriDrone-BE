# Be-Plan Phase 0 Legacy Data Disposition

- Inventory time: 2026-09-22 13:57:41 UTC
- Target: existing local development database `agridrone`
- Isolation: `REPEATABLE READ, READ ONLY`
- Script: `docs/operations/preflight-be-plan-alignment.sql`
- Result: completed and committed without writes

Không lưu email, UUID hoặc thông tin định danh cá nhân vào report này. Output chi tiết chỉ nên được lưu trong kho vận hành có kiểm soát truy cập.

## Aggregate inventory

| Legacy category | Count | Scope | Remediation owner | Auto-migrate? |
|---|---:|---|---|---|
| Active TenantAdmin membership | 4 | 2 Tenants, 3 users | Identity + Product | No |
| Active Member membership | 1 | 1 Tenant, 1 user | Identity + Product | No |
| Active Farm Manager membership | 2 | 2 Farms | Operations + SystemAdmin | No |
| Inactive Farm Manager membership | 1 | 1 Farm | Operations + SystemAdmin | No |
| Farm Worker membership | 0 | — | Operations + SystemAdmin | N/A |
| ZoneAssignment | 0 | — | Operations + SystemAdmin | N/A |
| Drone | 0 | — | Fleet + SystemAdmin | N/A |
| Global drone identifier collision | 0 | code/serial/registration | Fleet + SystemAdmin | N/A |
| Mission without SurveyOrder | 0 | current schema has no `survey_order_id` | Operations + Product | N/A for current rows |
| Farm | 3 | 2 Tenants | GIS + Operations | Requires review |
| Confirmed ZoneMapVersion | 0 | 3 Farms; one Farm has one Zone | GIS + Operations | No base-map backfill available |
| FieldTask | 0 | — | Data Governance + Product | N/A |
| HarvestBatch | 0 | — | Data Governance + Product | N/A |
| PlantHarvestRecord | 0 | — | Data Governance + Product | N/A |

## Data that must not be auto-migrated

1. **Five TenantAdmin/Member memberships.** Identity/Product must confirm which human is the TenantOwner, which accounts should remain linked only for history, and which access should be revoked. None may be promoted automatically to TenantOwner or SystemManager.
2. **Three Farm Manager memberships.** Operations/SystemAdmin must verify the person, active employment/account status, flight qualification, qualification expiry and availability before creating any SystemManager profile or primary Farm assignment. The inactive record is retained as history.
3. **Three Farms without a confirmed base map.** GIS/Operations must not fabricate a `FarmBaseMapVersion`. Their first approved SurveyOrder must require Baseline Mapping. The Farm with an existing Zone keeps that geometry, but it still has no approved map/plant identity baseline.

## Zero-count categories still protected

- Future/global duplicate drones require manual physical-asset reconciliation; records must never be merged by identifier alone.
- Any legacy Mission found on another snapshot must be matched to an approved SurveyOrder using operational evidence or retained as legacy-only history.
- FieldTask/Harvest records found on another snapshot stay read-only until Phase 11 retention approval; they are not converted into Harvest Readiness data.

Before a production migration, rerun the same script on the approved production-like snapshot and archive its detailed output. A zero count in this local inventory is not permission to skip that environment-specific check.
