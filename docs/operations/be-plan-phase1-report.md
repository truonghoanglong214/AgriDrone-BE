# Be-Plan Phase 1 Report

- Status: **Passed**
- Verified: 2026-09-23 (Asia/Bangkok)
- Scope: đóng entry point sai nghiệp vụ, không xóa handler/entity/schema legacy

## Kết quả xác minh

| Gate | Kết quả | Bằng chứng |
|---|---:|---|
| Solution build | PASS | 0 warnings, 0 errors |
| Unit tests | PASS | 384 passed, 0 failed, 0 skipped |
| Architecture tests | PASS | 39 passed, 0 failed, 0 skipped |
| Integration tests | PASS | 10 passed, 0 failed, 0 skipped trên PostgreSQL/RabbitMQ/Redis thật |
| Test stack | PASS | PostgreSQL, RabbitMQ và Redis đều `healthy` |
| Disabled endpoint smoke test | PASS | `POST /api/auth/register` trả `410`, `LegacyFlow.Disabled`, route `auth.register` |
| Retained endpoint smoke test | PASS | `POST /api/auth/login` với payload rỗng đi qua handler và trả validation `400` |
| OpenAPI contract | PASS | 54 paths, 60 operations, 48 schemas; 19 deprecated operations và cả 19 khai báo `410` |

## Safety coverage

Architecture/API safety tests khóa các invariant sau:

1. Tất cả 19 legacy operations có metadata và stable route name duy nhất.
2. Gate mặc định trả `410` mà không gọi downstream handler; rollback flag được kiểm thử riêng.
3. Login, password recovery, tenant selection, invitation preview/accept và read routes không bị gate nhầm.
4. Metric ghi đúng bounded `route` và normalized `actor` tags.
5. FieldTask write route tương lai không thể được thêm mà thiếu legacy gate.
6. Farm archive compatibility adapter chỉ phụ thuộc read query/context, không có command/repository/Harvest dependency.
7. `LegacyFeatures` mặc định disabled.

## OpenAPI snapshot

- Artifact: `docs/openapi/be-plan-phase1.openapi.json`
- SHA-256: `A283B06DBD5C7B61697026570C186432B7B761388E5509C33A4E992B3B2A93FD`
- Capture source: `GET /swagger/v1/swagger.json` từ API Phase 1.
- Phase 0 snapshot được giữ nguyên để review compatibility.

## Lệnh xác minh

```powershell
dotnet build AgriDrone.sln --no-restore
dotnet test tests\AgriDrone.UnitTests\AgriDrone.UnitTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.ArchitectureTests\AgriDrone.ArchitectureTests.csproj --no-build --no-restore
dotnet test tests\AgriDrone.IntegrationTests\AgriDrone.IntegrationTests.csproj --no-build --no-restore
docker compose -f compose.step1-test.yaml ps
```

## Kết luận exit gate

- Public/tenant self-registration và direct farm/member mutation bị chặn trước handler.
- Tenant-scoped drone mutation/availability và direct mission control bị chặn trước handler.
- Không có FieldTask API; Harvest catalogue/mutations bị chặn nên Phase 1 không tạo thêm Worker/FieldTask/Harvest qua các public API đã kiểm kê.
- Login và các route compatibility/read-only cần cho onboarding, migration, lịch sử vẫn được giữ.
- Không sửa hoặc xóa migration/schema legacy trong Phase 1.
