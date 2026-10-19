# BE1 Step 5D Completion Report: Archive Safety Checks & Operational Dependencies

**Date:** 2026-10-19  
**Status:** Completed (CodeComplete & Tested)  
**Task:** BE1 — Step 5 Phase 5D (Cross-Module Archive Safety Checks)

---

## 1. Summary of Changes

Phase 5D strengthens archive safety guarantees across modules by ensuring that active operational dependencies (active Zones, active Missions, and active Survey Orders) block the archiving of farms:

1. **`ISurveyOrderArchiveReferenceQuery` Integration Contract:**
   - Location: [`backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Farms/IArchiveDependencyReferenceQueries.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Farms/IArchiveDependencyReferenceQueries.cs)
   - Interface method:
     ```csharp
     Task<int> CountActiveForFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);
     ```

2. **Surveys Query Implementation:**
   - Location: [`backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Queries/SurveyOrderArchiveReferenceQuery.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Queries/SurveyOrderArchiveReferenceQuery.cs)
   - Counts all `SurveyOrder` records for the farm where status is neither `Completed` nor `Cancelled`.
   - Registered in [`AgriDrone.Modules.Surveys/DependencyInjection.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Surveys/DependencyInjection.cs).

3. **`ArchiveDependencySummary` & `FarmError` Upgrades:**
   - In [`IFarmArchiveDependencyQuery.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Application/Abstractions/Queries/IFarmArchiveDependencyQuery.cs):
     `ArchiveDependencySummary` extended with `ActiveSurveyOrderCount` (default 0), with `HasAny` evaluating `ActiveZoneCount > 0 || ActiveMissionCount > 0 || ActiveSurveyOrderCount > 0`.
   - In [`FarmError.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Application/Errors/FarmError.cs):
     `ActiveDependenciesExist` updated to report active survey orders in error message when present.

4. **`ArchiveFarmHandler` & `LegacyReadOnlyFarmArchiveDependencyQuery` Integration:**
   - In [`FarmArchiveDependencyQuery.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Infrastructure/Queries/FarmArchiveDependencyQuery.cs):
     `LegacyReadOnlyFarmArchiveDependencyQuery` injected with optional `ISurveyOrderArchiveReferenceQuery?`, querying active survey orders on farm archive requests.
   - In [`ArchiveFarmHandler.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/src/Modules/AgriDrone.Modules.Farms/Application/Features/ArchiveFarm/ArchiveFarmHandler.cs):
     Rejects archive request if any active zone, mission, or survey order remains.

---

## 2. Verification & Test Evidence

- **Unit Tests:**
  - Added `ArchiveFarmRejectsActiveSurveyOrders` to [`ArchiveFarmZoneHandlerTests.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/tests/AgriDrone.UnitTests/Application/Farms/ArchiveFarmZoneHandlerTests.cs).
  - Entire Unit Test suite passed: **630/630** tests passed.
- **Architecture Tests:**
  - Updated [`LegacyEndpointSafetyTests.cs`](file:///d:/Project/ProjectMonHoc/DoAn/AgriDrone/backend/tests/AgriDrone.ArchitectureTests/LegacyEndpointSafetyTests.cs) to assert read-only dependency `ISurveyOrderArchiveReferenceQuery`.
  - Architecture Test suite passed: **90/90** tests passed.

---

## 3. Completion Checklist

- [x] `ISurveyOrderArchiveReferenceQuery` created in `IntegrationContracts/Farms`.
- [x] `SurveyOrderArchiveReferenceQuery` implemented in `Surveys/Infrastructure/Queries/`.
- [x] Scoped DI registration in `AddSurveysModule`.
- [x] `ArchiveDependencySummary` supports `ActiveSurveyOrderCount`.
- [x] `FarmError.ActiveDependenciesExist` updated with survey order details.
- [x] `ArchiveFarmHandler` updated to propagate active survey order count.
- [x] `LegacyReadOnlyFarmArchiveDependencyQuery` checks active survey orders.
- [x] Unit tests updated and 100% passing (630/630).
- [x] Architecture tests updated and 100% passing (90/90).
