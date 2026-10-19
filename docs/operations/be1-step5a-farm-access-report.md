# BE1 Step 5A — Farm Access and Operational Context Report

- Date: 2026-10-19
- Scope: Step 5A (UC07) SystemManager Farm operational context query, assignment authorization and read API
- Status: CodeComplete — AwaitingFinalTest

## Summary of Delivered Surface

### 1. Abstraction & Data Transfer Objects
- Created `IFarmOperationalContextQueries.cs` at `Farms/Application/Abstractions/Queries/`:
  - `Task<FarmOperationalContextDto?> GetAssignedFarmOperationalContextAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken)`
  - DTOs: `FarmOperationalContextDto`, `ApprovedBoundaryDto`, `OperationalZoneDto`, `PublishedBaseMapSummaryDto`

### 2. Infrastructure Query Implementation
- Created `FarmOperationalContextQueries.cs` at `Farms/Infrastructure/Queries/`:
  - Đọc `Farm` theo `tenantId` và `farmId` (non-deleted).
  - Lấy current approved `FarmBoundary` (`Status == Approved` và `SupersededAt == null`).
  - Lấy danh sách active `FarmZones` của farm.
  - Lấy latest published `FarmBaseMapVersion` của farm.
  - Đăng ký DI trong `AgriDrone.Modules.Farms/DependencyInjection.cs`.

### 3. Application CQRS Query & Handler
- Created feature folder `Farms/Application/Features/GetAssignedFarmOperationalContext/`:
  - `GetAssignedFarmOperationalContextQuery.cs`
  - `GetAssignedFarmOperationalContextQueryValidator.cs`
  - `GetAssignedFarmOperationalContextQueryHandler.cs` (kiểm tra `ISystemManagerAccessService` trước khi truy vấn dữ liệu)
  - `FarmOperationalContextResponse.cs`

### 4. API Controller & Contracts
- Created contract `FarmOperationalContextApiResponse.cs` tại `AgriDrone.Api/Contracts/Farms/`.
- Created controller `SystemManagerFarmsController.cs` tại `AgriDrone.Api/Controllers/`:
  - Route: `GET /api/system-manager/farms/{farmId}/operational-context`
  - Policy: `AccessAuthorizationPolicies.SystemManager`
  - GeoJSON mapping dùng `GeoJsonGeometryMapper`.

### 5. Authorization & Invariant Enforcement
- `UpdateFarmDetailHandler.cs` đã tích hợp `ISystemManagerAccessService` để kiểm tra phân công quản lý farm.
- `GetAssignedFarmOperationalContextQueryHandler` từ chối nếu actor không phải SystemManager được phân công trên farm tương ứng (`FarmError.AccessDenied`).
- TenantOwner và actor khác không gọi được route của SystemManager (`403 Forbidden`).

## Verification Evidence

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Unit Tests: 605/605 passed (bao gồm 4 test cases cho `GetAssignedFarmOperationalContextHandlerTests`).
- Architecture Tests: 90/90 passed.

## Next Phase

Sẵn sàng chuyển sang **Phase 5B — Boundary policy, correction và Zone verification**.
