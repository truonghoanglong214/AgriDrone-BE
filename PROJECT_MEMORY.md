# AgriDrone Project Memory Bank & Status Tracker

**Repository**: `D:\Project\ProjectMonHoc\DoAn\AgriDrone`  
**Current Active Branch**: `LongTH`  
**Last Updated**: 2026-11-19
**Tech Stack**: .NET 10, C# 14, Clean Architecture, Entity Framework Core, PostgreSQL 17 + PostGIS 3.5, NetTopologySuite, RabbitMQ 4.2.7, Redis 8.8.1, MinIO.

---

## 1. Project Overview & Architecture
AgriDrone là hệ thống quản lý và điều phối dịch vụ khảo sát bay drone cho cây trồng (thanh long, cây ăn trái).
- **Kiến trúc**: Modular Monolith + Clean Architecture + Domain-Driven Design (DDD).
- **Modules cốt lõi**:
  - `AgriDrone.Modules.Surveys`: Quản lý yêu cầu, báo giá, chính sách giá, cọc, lịch hẹn, hợp đồng khảo sát, kết quả và hủy đơn.
  - `AgriDrone.Modules.Missions`: Quản lý drone, phi vụ bay (`DroneMission`), checklist kiểm tra an toàn (`Preflight`), tải media và xử lý AI.
  - `AgriDrone.Modules.Farms`: Quản lý ranh giới trang trại, các phiên bản bản đồ số (`FarmBaseMapVersion`).
  - `AgriDrone.Modules.Plants`: Quản lý định danh cây trồng (`PlantProfile`), biến động cây trồng (`PlantChangeEvent`), phát hiện bệnh (`DiseaseObservation`).
  - `AgriDrone.Modules.Identity`: Quản lý phân quyền, phân công quản lý trang trại (`FarmManagerAssignment`).
  - `AgriDrone.Database`: Shared DbContext (`AgriDroneSchemaDbContext`), điều phối viên nghiệp vụ (`SurveyCaptureExecutionCoordinator`), migrations, outbox processor.

---

## 2. Master Plan Phase Tracker (BE1)

| Phase | Phạm vi nghiệp vụ | Trạng thái | Ghi chú & Exit Gate |
|---|---|---|---|
| **V3-00** | Business rules, traceability, documentation | **[x] Done** | Design locked, ADR 0019/0020 |
| **V3-01** | Policy model, contracts, scope separation | **[x] Done** | Tách MAP/COMBO, First/Follow-up |
| **V3-02** | Catalogue, shared deposit, approval snapshots | **[x] Done** | Published price revisions, deposit policies |
| **V3-03** | Capture execution, funding, re-flight, cancellation | **[x] Done (Code)** | Hoàn thành `03A` → `03H-2` (Commit `16210b3`), `03H-3` handoff |
| **V3-04** | **Mapping, Plant Identity & Spatial Lineage** | **[x] Done (Remediated)** | **G0–G18 closed; E1/S1 migration chain sạch; build 0W/0E; tests deferred to V3-10** |
| **V3-05** | **Combo review, invoice finalization, pole count** | **[ ] Planned — Next** | Có thể bắt đầu lập kế hoạch sau khi V3-04 đã đóng đủ exit gates |
| **V3-06** | Settlement, atomic release, customer notice | **[ ] Planned** | Release proof, financial facts |
| **V3-07** | Owner portal, media viewer, comparison | **[ ] Planned** | Historical comparison UI |
| **V3-08** | Claims, refund execution, post-release correction | **[ ] Planned** | Transfer proofs, complaint window |
| **V3-09** | Financial reporting, revenue dashboard, Excel | **[ ] Planned** | Ledger reconciliation |
| **V3-10** | **Comprehensive Acceptance, Cutover & Release** | **[ ] Final Gate** | **Toàn bộ Integration Test Suite chạy tập trung tại đây** |

---

## 3. Tóm tắt kết quả Phase V3-03 vừa hoàn thành (Commit `16210b3`)
- **Điều phối viên bay (`SurveyCaptureExecutionCoordinator`)**:
  - Quản lý vòng đời `CaptureAttempt`: `Planned` → `InFlight` → `AwaitingUpload` → `Processing` → `Accepted` / `ReflightRequired`.
  - Checksum & Media thật: `FreezeInputManifestAsync` đọc trực tiếp `MissionMedia`, kiểm tra SHA-256 (64 hex), dung lượng > 0, xác thực với `IObjectStorage.GetInfoAsync`.
  - Bay lại chất lượng (Quality Re-flight): Tạo successor attempt cùng order, không thu thêm cọc.
- **Quy trình Thanh toán & Đảo cọc (`ProcessSurveyPaymentEventCommandHandler`)**:
  - Hỗ trợ First vs Follow-up × MAP vs COMBO.
  - Xử lý hoàn trả/chargeback (`SurveyFundingReversal`): ghi nhận fact đảo tiền bất biến, giảm cọc trên `SurveyDepositObligation`, thu hồi readiness về `AwaitingDeposit` nếu chưa bay.
- **Hủy đơn & Hoàn cọc (Cancellation & Refund Cap)**:
  - Phân loại: CustomerBeforeCapture, CustomerAfterAcceptableCapture (yêu cầu Admin + bằng chứng kỹ thuật), OperationalPostponement, ServiceFailure (hoàn 100%).
  - Refund Cap: Khấu trừ `completedRefunds` và `activeReservations`, ngăn chặn hoàn vượt receipt (`REFUND_RECONCILIATION_HOLD`).
  - No-Show: Đối chiếu hạn mức `AllowedFreeNoShowReschedules` (dưới hạn mức -> FreeReschedule, vượt hạn mức -> AdminReviewRequired).
- **Chống xung đột đa luồng & TOCTOU**:
  - Row lock PostgreSQL `FOR SHARE` cho assignment, `FOR UPDATE` cho cancel và revoke handler.
  - Chặn hủy phân công khi có chuyến bay đang thực hiện (`ACTIVE_CAPTURE_IN_FLIGHT`).

---

## 4. Tiến độ Phase V3-04 (Canonical Remediation V2 — Done 2026-11-18)
> **Trạng thái**: Hoàn thành G0–G18. Chủ dự án xác nhận local là database duy nhất; lịch sử migration và backlog local đã đủ để chốt Nhánh A. Migration canonical mới không lẫn phase khác.
> **Nguyên tắc**: Tests NOT RUN (dành tập trung cho V3-10); `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 Warning, 0 Error.

Các hạng mục đã hoàn thành toàn diện:
1. **Migration Gate M0 — Đã đóng theo Nhánh A**:
   - Chủ dự án xác nhận không có database nào khác ngoài local. Local preflight lúc 11:05 ngày 2026-11-18 (Asia/Bangkok) cho thấy các migration V3-04 cũ chưa từng apply; migration mới nhất local là `20261109071540_Step7EPlantHealthCompletionOutput`.
   - Local V3 backlog: outbox chưa xử lý = 0, inbox chưa hoàn tất = 0; RabbitMQ main/retry/dead queues của `be1-baseline-mapping-candidates-ready-v3` đều 0.
   - Đã loại hai remediation migration cũ `20261116145210` và `20261117050302`. Chain canonical: `20261116131222_V3_04MappingAndPlantIdentity` → `20261118040000_V3_04AddInternalApprovedMapStatus` → `20261118042702_V3_04CanonicalMappingRemediation`.
   - E1 thêm enum ngoài transaction và có `Down` no-op. S1 chỉ chứa scope V3-04, backfill provenance theo expand → exact join → validate → constrain; không có pricing/appointment/post-flight.
   - Generated SQL đã được inspect nhưng không apply database; script preflight read-only giữ tại `docs/operations/v3-04-remediation-preflight.sql`.
2. **Provenance & Metadata Thật**:
   - `MissionPlantGpsPublisher` đọc metadata từ `AiProcessingJob` hoàn thành, sinh deterministic candidate UUIDs v5 từ `jobId + treeId`, loại bỏ mọi GUID / timestamp ngẫu nhiên (`Guid.NewGuid()`, `UtcNow`).
3. **CaptureAttemptId Xuyên suốt**:
   - Lưu `CaptureAttemptId` trong `MappingCandidateBatch`, xác thực đúng order/mission/boundary, truyền sang `SurveyOrderPoleCountSnapshot`, `FarmBaseMapInternallyApprovedV1`, và `FarmBaseMapInternallyApprovedV1Handler`.
4. **Phân định rõ `PlantId` và `PlantProfileId`**:
   - `FarmBaseMapInternalApprovalOrchestrator` tra cứu đúng thứ tự: `targetPlantId` → `Plant` → `targetProfileId` → `PlantProfile`. Không nhầm ID giữa 2 aggregate.
5. **Spatial Policy & True Geodesic Boundary**:
   - `GeodesicSpatialCalculator` và PostGIS geography tính toán mét thực, initial bearing degrees. Tiêu thụ đủ 4 trường tolerances; fails closed nếu thiếu policy hoặc tolerance <= 0.
6. **Xóa Pipeline V3 Khỏi Runtime**:
   - Xóa bỏ hoàn toàn `BaselineMappingCandidatesReadyV3Handler` và `BaselineMappingCandidatesReadyV3Processor` khỏi DI và codebase.
7. **Khóa DB Invariants & Restrict FKs**:
   - Check constraint `ck_farm_base_map_versions_lineage`, `ck_survey_order_pole_count_snapshot_items_acceptance`.
   - Unique filtered index `(tenant_id, farm_id)` cho baseline (`is_baseline=true`) và draft (`status='DRAFT'`).
   - Unique filtered index `uq_plants_farm_plant_profile` trên `(farm_id, plant_profile_id)`.
   - Restrict toàn bộ FK snapshot evidence (`MappingCandidateBatch`, `PlantProfile`, `MappingCandidate`).
8. **Bảo toàn Semantics Nội bộ (Zero Published Trong V3-04)**:
   - `FarmBaseMapInternalApprovalOrchestrator` chỉ phê duyệt nội bộ (`InternalApproved`), không gán các trường mang nghĩa Published.
   - `DroneMission` ghi nhận `InternallyApprovedMapVersionId` và `MapInternallyApprovedAt` với outcome-based method (`DroneMissionApprovalOutcome`), không chạm `PublishedMapVersionId`/`MapPublishedAt`.
9. **Verification cuối**:
   - Sửa ba filtered index baseline/draft/published thành ba EF model-index name riêng, tránh EF ghi đè metadata khi cùng dùng `(TenantId, FarmId)`.
   - `SurveyPayments:Enabled` ở Development đã trả về `false`, giữ fail-closed đến V3-10.
   - Static scans đạt; `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 Warning, 0 Error.
   - **TESTS NOT RUN — deferred to V3-10**.

---

## 5. Kế hoạch & Nhiệm vụ tiếp theo: Phase V3-05 (Combo Review, Invoice Finalization, Pole Count)
Trọng tâm công việc Phase V3-05:
1. **Rà soát Combo Đa dịch vụ (Combo Review Workflow)**:
   - Tổng hợp kết quả bay định kỳ và kết quả khảo sát sức khỏe cây trồng theo đợt.
   - Đối chiếu số trụ phát hiện với số trụ đã chốt tại V3-04.
2. **Khóa Hóa đơn & Bảng giá Cuối cùng (`InvoiceFinalization` / `InvoiceRevision`)**:
   - Tính toán `FinalPrice = VerifiedBillablePoleCount * PricePerPoleSnapshot * (1 - ComboDiscountPercentage)`.
   - Lập biên bản quyết toán, trừ tiền cọc (`SurveyFundingReceipt`), xác định số dư còn phải trả (`RemainingAmount`).
3. **Quản lý Bất biến & Không ngoại lệ (Fail-closed)**:
   - Đảm bảo snapshot thương mại bất biến trước khi phát hành hóa đơn cho chủ vườn.

---

## 6. Nguyên tắc Vận hành Bắt buộc cho AI Agent
1. **KHÔNG CHẠY TEST TÍCH HỢP TRONG CÁC PHASE CODE (V3-01 -> V3-09)**:
   - Nghiêm cấm chạy `dotnet test` với PostgreSQL container / integration test suites vì gây treo máy và tốn thời gian.
   - Toàn bộ test nghiệm thu tích hợp sẽ chạy tập trung ở **Phase V3-10**.
   - Chỉ được phép chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo` để kiểm tra biên dịch (thời gian ~5–8s).
2. **KẾ HOẠCH TRƯỚC KHI CODE (Superpowers: writing-plans)**:
   - Luôn lập checklist và xác định rõ file cần sửa/tạo mới trước khi chạm vào mã nguồn.
3. **BẢO TỒN DỮ LIỆU & BẤT BIẾN (Immutability)**:
   - Không UPDATE đè lên các bản ghi lịch sử, receipt, snapshot hoặc sự kiện domain.
   - Dùng Result pattern (`Result<T>`), không throw exception để điều khiển luồng nghiệp vụ.

---

## 7. AWS Lightsail Demo Deployment Readiness (Phases P0–P5 Completed 2026-11-19)
> **Trạng thái**: Toàn bộ codebase packaging, proxy safety, compose architecture, scripts và CI/CD đã hoàn tất 100%. Sẵn sàng deploy lên AWS Lightsail (Singapore).
- **Packaging & Containerization**:
  - `backend/src/AgriDrone.Api/Dockerfile`: Multi-stage .NET 10 SDK -> ASP.NET 10 Runtime non-root (`$APP_UID`), build sẵn `efbundle` self-contained Linux-x64, healthcheck nhẹ.
  - `backend/src/AgriDrone.Api/Dockerfile.migrator`: Dedicated migrator image chạy one-shot migration.
  - `backend/services/metashape-service/Dockerfile`: PyTorch / Torchvision CPU-only từ official wheel index, pinned packages, non-root user `appuser`.
- **Proxy Safety & Configuration**:
  - `Program.cs`: `ForwardedHeadersOptions` cấu hình `KnownIPNetworks` cho các dải subnet Docker container bridge (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), `ForwardLimit = 1`.
  - `Program.cs` & `appsettings.json`: Dynamic CORS fail-closed đọc từ `Cors:AllowedOrigins` (mặc định rỗng, không wildcard credentials khi production).
  - `appsettings.json`: `DatabaseInitialization:ApplyMigrationsOnStartup = false` (tách migration ra khỏi API startup).
- **MinIO Public/Internal Split**:
  - `MinioStorageOptions.cs` thêm `PublicEndpoint` (URL presigned dành cho browser bên ngoài `https://media.domain.com`), trong khi `Endpoint` giữ `http://minio:9000` cho internal calls trong Docker bridge.
- **Production Compose & Edge Caddy**:
  - `compose.production.yaml`: Hai profiles: `core` (Caddy, API, Postgres 17, RabbitMQ, Redis, MinIO, one-shot Migrator) và `processing` (FastAPI, NodeODM concurrency=1).
  - `infrastructure/caddy/Caddyfile`: Tự động Let's Encrypt HTTPS cho `api.domain` và `media.domain`; chỉ publish duy nhất port 80/443; chặn console 9001 và Caddy admin.
- **Operations & CI/CD**:
  - `infrastructure/scripts/`: `bootstrap-lightsail.sh` (cài Docker, 16GB Swapfile, folder `/opt/agridrone`), `deploy.sh` (flock lock, pre-deploy backup, one-shot migration, smoke test, automatic rollback), `backup.sh` (`pg_dump -Fc` nén + sha256), `restore.sh` (diễn tập an toàn), `smoke-test.sh`.
  - `.github/workflows/`: `ci.yml` (build check .NET 10 0W/0E, compose config validation; không chạy test) và `deploy-lightsail.yml` (GHCR push immutable tags, SSH deploy).
- **Verification Evidence**:
  - `dotnet build backend/AgriDrone.sln --no-restore --nologo`: **0 Warning, 0 Error**.
  - `docker compose config`: 100% Valid, Published ports: 80 & 443 ONLY.
  - Integration Tests: Deferred to V3-10 per project governance.
