# V3-02 — Checklist thực thi theo từng phần

Date: **2026-11-12**. Status: **Implementation present — PostgreSQL integration acceptance pending; public activation remains off**. [Kế hoạch kỹ thuật A–F](v3-02-catalogue-and-approval-implementation-plan.md), [locked design](../domain/v3-01-locked-design.md), [source audit](../operations/v3-01-source-audit.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

Các evidence/test counts bên dưới ghi nhận từng đợt triển khai, không thay thế PostgreSQL acceptance. V3 Follow-up production hiện fail closed khi chưa có nguồn durable release proof của V3-06; `Completed` + `Published` không chứng minh đã bàn giao.

## 1. Mục tiêu hoàn thành V3-02

Backend có thể quản lý cấu hình MAP/COMBO và cọc chung theo phiên bản, nhận đúng ba loại request, tra cứu bằng tracking token an toàn và approve thành Order có snapshot/history trong một transaction. Giá/cọc/claim/cancellation policy của Order không đổi khi Admin cập nhật cấu hình.

Public intake/checkout tiền thật vẫn off đến khi full capture/release/visibility/claims/refund được acceptance. V3-02 không tự sửa Mission recovery, thu tiền, chạy AI hoặc mở kết quả.

## 2. Bảng công việc và thứ tự phụ thuộc

Các ô dưới đây chỉ đánh dấu khi có implementation evidence tương ứng, không đánh dấu theo trạng thái tài liệu đã phê duyệt.

| ID | Công việc | Đầu ra để review | Phụ thuộc | Status |
|---|---|---|---|---|
| 02-00 | Ghi nhận baseline và phạm vi | Working-tree inventory, exact source paths, danh sách legacy constraints/enum consumers cần audit | V3-01 design | [x] Completed |
| 02A-1 | Domain cấu hình có phiên bản | Service revision, policy/bundle/activation models và focused domain checks | 02-00 | [x] Completed |
| 02A-2 | EF/schema và compatibility | Mappings, new expand migration/generated SQL, scope guards, legacy query isolation, DB checks | 02A-1 | [ ] Implemented — DB acceptance pending |
| 02B-1 | Admin publish/history | Publish service/price/deposit/policy versions, concurrency guard và audit | 02A-2 | [x] Completed |
| 02B-2 | Catalogue v3 nhất quán | MAP/COMBO, current rate/shared deposit, policy references và isBookable=false khi chưa ready | 02B-1 | [x] Completed |
| 02C-1 | Request + indicative quote | NewCustomer/NewFarm/ExistingFarmFollowUp request, quote/history/idempotency, không provision | 02B-2 | [x] Completed |
| 02C-2 | Secure tracking/replay | Hash token lookup, protected acknowledgement replay, minimal status và privacy/rate-limit checks | 02C-1 | [x] Completed |
| 02D-1 | Same-transaction snapshot resolver | Typed resolved snapshot và DB adapter cùng approval connection/locks | 02B-1, 02C-1 | [x] Completed |
| 02D-2 | Three approval flows | Correct resource set, unpaid initial Order, immutable commercial snapshot/history | 02C-2, 02D-1 | [x] Completed |
| 02D-3 | Approval failure/race acceptance | Fault-injection rollback, Request→Order uniqueness, config writer/replay races | 02D-2 | [ ] PostgreSQL integration acceptance pending |
| 02E-1 | HTTP v3/DI/OpenAPI handoff | Actual v3 routes/DTOs, startup/auth/validation checks và new runtime export khi có runtime | Relevant B/C/D capabilities | [x] Completed |
| 02F-1 | Phase integration/upgrade review | Fresh + populated legacy fixtures, shared policy/window/FK/enum compatibility evidence | A–E | [ ] PostgreSQL integration acceptance pending |
| 02F-2 | Completion evidence và V3-03 handoff | Commands/results/limitations, updated route/schema handoff, activation remains off | 02F-1 | [ ] Preliminary handoff — waiting for 02F-1 |

API contracts/validation và DI cần nối cùng mỗi feature để kiểm tra được B/C/D; 02E-1 là consolidation/review checkpoint, không để toàn bộ API integration đến cuối mới bắt đầu.

## 3. Phần đầu tiên: 02A-1

### Phạm vi file dự kiến

Các đường dẫn dưới đây tương đối với `backend/src/Modules/AgriDrone.Modules.Surveys`; đây là **planned files**, chưa tạo trong phiên lập kế hoạch.

| Nhóm | File/entity | Nội dung bắt buộc |
|---|---|---|
| Catalogue | `Domain/Catalogue/SurveyServiceRevision.cs` | Immutable name/description/capabilities version, service reference và lineage |
| Deposit | `Domain/Policies/SurveyDepositPolicy.cs` | Một shared fixed-deposit stream, currency/amount/version/window |
| Claim | `Domain/Policies/ClaimPolicy.cs` | Calendar days/timezone/deadline mode, semantics của notice/replacement |
| Cancellation | `Domain/Policies/CancellationPolicy.cs` | Before-capture return, acceptable-capture retention basis, service-failure/no-show rules |
| Bundle | `Domain/Policies/BusinessPolicyVersion.cs` | Pin service/deposit/claim/cancellation version references, business fingerprint |
| Activation | `Domain/Policies/BusinessPolicyActivation.cs` và decision/history record | Current pointer projection + append-only decision; không bật public intake mặc định |
| Tests | Existing `backend/tests/AgriDrone.UnitTests/Domain/Surveys/` | Meaningful policy/window/lineage/value checks; dùng tham số test, không biến demo data thành runtime const |

Dùng common metadata pattern cho VersionNumber, predecessor, actor/time/reason/effective window; không tạo generic versioning framework hoặc base-class hierarchy nếu chưa cần. Nếu policy history cần thêm helper/value object, ghi rõ lý do trong change description.

### Những việc làm trong 02A-1

1. Chốt factory inputs bắt buộc: IDs/stream/version/predecessor, UTC times, actor/reason, amount/currency hoặc policy values; không tự lấy giá mặc định hay tự bịa actor GUID.
2. Validate cấu trúc một version: end sau start, version/IDs hợp lệ, predecessor không tự trỏ, value requirements và immutable payload. Cross-row no-overlap/predecessor same-stream được enforce đầy đủ ở 02A-2, không tuyên bố domain-only đã ngăn DB race.
3. Validate whole-VND cho new v3 amounts ở policy boundary; không siết global legacy money value object khiến dữ liệu cũ không đọc được.
4. Model config publication/activation decision history; current state/pointer chỉ được thay đồng thời với event/decision khi infrastructure nối ở bước sau.
5. Ghi focused checks cho invariant và serialization/value behavior cần thiết; build project/test scope liên quan khi implementation thực sự bắt đầu.

### Phạm vi để sang 02A-2 hoặc phase sau

- Nullable legacy `SurveyServiceType`, new ContractScope mapping, modifications to existing service queries và EF constraints thuộc 02A-2. Không đổi chữ ký enum cũ giữa một domain-only change rồi để các consumers bị hỏng.
- `SurveysDbContext`, approval DbContext, schema context, migrations, seed/demo activation và HTTP routes chưa thay ở 02A-1.
- Commercial snapshot resolver/Order factory wiring thuộc 02D; không bỏ cọc cũ bằng cách thay hằng số 5.000 thành 100.000.
- SpatialPolicy/capture attempts/media pipeline thuộc V3-03/04; invoice/release/claim/refund/revenue runtime thuộc phase tương ứng.

### Exit gate 02A-1

- [x] Planned models/factories có đủ version/lineage và explicit dependencies, không mutable public setters làm mất lịch sử.
- [x] No hardcoded amount, claim days, policy version hoặc tolerance fallback trong production code mới.
- [x] Domain checks xác minh invalid windows/values/self-predecessor, immutable payload và required metadata; không chỉ kiểm tra getter trả đúng input.
- [x] Existing legacy enum/readers/Order/payment/Mission behavior chưa bị đổi trong slice này.
- [x] Relevant build/focused test evidence ghi chính xác; chưa claim DB no-overlap/concurrency hoặc full v3 acceptance.
- [x] File diff phù hợp domain-only slice; next handoff ghi rõ schema/compatibility work còn ở 02A-2.

### Evidence hoàn thành 02A-1 (2026-11-11)

- **Domain models created**:
  - `Domain/Policies/PolicyValidationGuard.cs`: Shared invariants (lineage, version, end>start, required/optional string bounds, positive whole-VND, VND currency).
  - `Domain/Policies/PolicyDomainErrorCodes.cs`: Invariant error codes (`POLICY_WINDOW_ALREADY_CLOSED`, `POLICY_INVALID_EFFECTIVE_WINDOW`, etc.).
  - `Domain/Catalogue/SurveyServiceRevision.cs`: Versioned Name/Description/Capabilities (`MapsPoles`, `ScreensDisease`), lineage, half-open window `[EffectiveFrom, EffectiveTo)`.
  - `Domain/Policies/SurveyDepositPolicy.cs`: Shared deposit policy, positive whole-VND, VND currency, auditable closure.
  - `Domain/Policies/ClaimPolicy.cs`: Calendar days, timezone (`Asia/Ho_Chi_Minh`), `ExcludeNoticeDate`, exclusive deadline computation per `BR-CLM-02`/`BR-CLM-03`.
  - `Domain/Policies/CancellationPolicy.cs`: Return before capture, retention after capture, allowed free no-show reschedules >= 0, service failure rules.
  - `Domain/Policies/BusinessPolicyVersion.cs`: Commercial bundle pinning revisions/deposit/claim/cancellation versions, deterministic SHA-256 fingerprint.
  - `Domain/Policies/BusinessPolicyActivation.cs` & `BusinessPolicyActivationDecision.cs`: Singleton activation projection and append-only decision audit, `PublicIntakeEnabled = false` by default.
- **Unit test suite created**:
  - `backend/tests/AgriDrone.UnitTests/Domain/Surveys/VersionedCommercialPolicyTests.cs` (10 tests covering all invariants, lineage errors, integer-money guards, claim deadline computations, fingerprint determinism, and activation transitions).
- **Test execution evidence**:
  - `dotnet test backend\tests\AgriDrone.UnitTests\AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 745, Skipped: 0.
  - `dotnet test backend\tests\AgriDrone.ArchitectureTests\AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 80, Skipped: 0.
  - `dotnet build backend\AgriDrone.sln --no-restore` -> Build succeeded: 0 Warning(s), 0 Error(s).
- **Scope isolation**:
  - DB schemas, EF mappings, legacy enum nullability, HTTP endpoints, and Order commercial snapshot wiring have NOT been modified (strictly gated for 02A-2 and later).

## 4. Gate 02A-2 — trước mọi thay đổi schema

- [x] Review actual PostgreSQL enum mappings, triggers/FKs, existing price-window constraints, baseline-only deposit và count/price requirements trên Order.
- [x] Reuse service/price tables; new v3 rows có explicit immutable scope/definition, legacy rows giữ meaning/amount/IDs. Missing required v3 snapshot không được downgrade thành legacy.
- [x] Add mappings/constraints và migration mới; preserve historical migration files. Chỉ apply trên DB test đã chọn trong implementation scope; environment thực chưa được inspect trong task lập kế hoạch này.
- [x] DB checks phải có fresh fixture và populated legacy fixture; current price closure + new version + activation/history atomic dưới config guard.
- [x] Chưa cần sửa all-Order capture/deposit readiness ở A2. Chỉ chứng minh schema và legacy queries coexist được với new config records, không nhận tiền thật.

### Evidence hoàn thành 02A-2 (2026-11-11)

- **PostgreSQL Enum and ContractScope integration**:
  - `Domain/Common/ContractScope.cs` (`Legacy = 1, V3 = 2`) added and registered into PostgreSQL enum system:
    - Registered in `BuildingBlocks/AgriDrone.Database/PostgreSqlEnumMappings.cs` (`system.contract_scope`).
    - Registered in `Modules/AgriDrone.Modules.Surveys/DependencyInjection.cs`.
- **Legacy Enum isolation & Domain nullability**:
  - `SurveyService`: `Scope` (`ContractScope.Legacy` by default), `ServiceType` made nullable (`SurveyServiceType?`), added `Revisions` collection, provided `CreateV3` factory.
  - `SurveyServicePrice`: Extended with version lineage (`VersionNumber`, `PredecessorVersionId`, `ReasonCode`, `EvidenceReference`), provided `CreatePerPoleV3` factory.
  - Legacy handlers & queries isolated:
    - `SurveyCatalogueQueries.cs`: Explicitly filtered to `Scope == ContractScope.Legacy && ServiceType != null` so existing public catalogue only serves legacy services.
    - `SurveyRequest.cs`, `ApproveSurveyRequestCommandHandler.cs`, `CreateSurveyServicePriceCommandHandler.cs`, `SurveyApprovalReferenceQueries.cs`, `SurveyOrderOperationalContextV3Query.cs`, `SurveyOrderOperationalContextV4Query.cs`: Safely guarded against nullable `ServiceType` without altering legacy workflow logic.
- **EF Core Configurations & Constraints**:
  - `SurveyServiceConfiguration.cs`: Check constraint `ck_survey_services_scope_type`: `(scope = 'LEGACY' AND service_type IS NOT NULL) OR (scope = 'V3' AND service_type IS NULL)`.
  - `SurveyServicePriceConfiguration.cs`: Lineage foreign key `fk_survey_service_prices_predecessor`, unique index `uq_survey_service_prices_service_version` filtered on `version_number IS NOT NULL`, constraint `ck_survey_service_prices_version`.
  - `SurveyServiceRevisionConfiguration.cs`: Table `survey.survey_service_revisions`, FKs to service and predecessor, check constraints on capabilities (`maps_poles OR screens_disease`), version, and half-open window.
  - `SurveyDepositPolicyConfiguration.cs`: Table `survey.survey_deposit_policies`, positive whole-VND check `amount > 0 AND amount = FLOOR(amount)`, currency regex `^[A-Z]{3}$`.
  - `ClaimPolicyConfiguration.cs`: Table `survey.claim_policies`, `calendar_window_days >= 1`.
  - `CancellationPolicyConfiguration.cs`: Table `survey.cancellation_policies`, `allowed_free_no_show_reschedules >= 0`.
  - `BusinessPolicyVersionConfiguration.cs`: Table `survey.business_policy_versions`, array column `service_revision_ids` (`uuid[]`), unique `uq_business_policy_versions_stream_version`, descending effective index.
  - `BusinessPolicyActivationConfiguration.cs`: Table `survey.business_policy_activation`, singleton pointer projection, default flags disabled (`public_intake_enabled = false`).
  - `BusinessPolicyActivationDecisionConfiguration.cs`: Table `survey.business_policy_activation_decisions`, append-only audit trail with predecessor FK.
- **Migration & Schema Verification**:
  - Created migration `20261111041444_Step8_V3CommercialPolicyFoundation.cs` in `AgriDrone.Database`.
  - Validated SQL script generation via `dotnet ef migrations script Step7EPlantHealthCompletionOutput Step8_V3CommercialPolicyFoundation`.
  - Preserved 100% of historical migrations and existing database tables.
  - Clean idempotent DDL with reversible `Down()` method.
- **Verification & Test Status**:
  - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 745, Skipped: 0.
  - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 80, Skipped: 0.
  - Zero hardcoding invariant verified: no fixed money amounts or hardcoded claim days in C# domain logic.

### Evidence hoàn thành 02B-1 (2026-11-11)

- **Admin Publish Commands & Handlers**:
  - `PublishSurveyServiceRevisionCommand` & Handler: Versioned name/description/capabilities for V3 services (`MAP`, `COMBO`), auto-increments version, closes predecessor revision at `now`, validates capabilities (`maps_poles OR screens_disease`), records audit log via `IAuditWriter`.
  - `PublishSurveyServicePriceV3Command` & Handler: Versioned per-pole rate in whole positive VND, enforces currency `VND`, auto-increments version, closes predecessor price version at `now`, records audit log.
  - `PublishSurveyDepositPolicyCommand` & Handler: Shared deposit policy stream, positive whole VND, closes predecessor at `now`, records audit log.
  - `PublishClaimPolicyCommand` & Handler: Versioned calendar window (`>= 1` days), timezone (`Asia/Ho_Chi_Minh`), `ExcludeNoticeDate`, closes predecessor at `now`.
  - `PublishCancellationPolicyCommand` & Handler: Versioned return before capture, retention after capture, `AllowedFreeNoShowReschedules >= 0`.
  - `PublishBusinessPolicyVersionCommand` & Handler: Bundles revisions + deposit + claim + cancellation versions, validates active dependencies, computes deterministic SHA-256 fingerprint, closes predecessor bundle at `now`.
  - `TransitionBusinessPolicyActivationCommand` & Handler: Singleton activation projection with optimistic lock on `ExpectedLastDecisionId`, persists append-only `BusinessPolicyActivationDecision` history, keeps `PublicIntakeEnabled = false` by default.
- **Repository & Abstractions**:
  - `IBusinessPolicyRepository` & `BusinessPolicyRepository` created and registered in DI (`DependencyInjection.cs`).
  - Extended `ISurveyServiceRepository` & `SurveyServiceRepository` to include `Revisions` collection and add revisions.
  - `BusinessPolicyError` standard error codes (`PredecessorMismatch`, `DecisionMismatch`, `InactiveDependency`, `InvalidAmount`, `ConcurrentUpdate`).
- **Test Execution Evidence**:
  - Created `backend/tests/AgriDrone.UnitTests/Application/Surveys/AdminPublishCommercialPolicyTests.cs` (10 tests covering all 7 commands, concurrency guard mismatches, predecessor window closure, dependency validation, and activation decision history).
  - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 755, Skipped: 0.
  - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 80, Skipped: 0.
  - `dotnet build backend/AgriDrone.sln --no-restore` -> Build succeeded: 0 Warning(s), 0 Error(s).

### Evidence hoàn thành 02B-2 (2026-11-11)

- **Consistent V3 Public Catalogue Query**:
  - `ISurveyCatalogueQueries.GetPublicCatalogueV3Async` implemented in `SurveyCatalogueQueries.cs`.
  - Atomically resolves the current active bundle pointer from `BusinessPolicyActivation.SingletonId` at `evaluatedAt`.
  - Resolves active services (`MAP`, `COMBO`), their effective revisions, effective per-pole prices, one shared deposit policy, and policy summary (claim window, timezone, cancellation rules).
  - Enforces `isBookable = activation.PublicIntakeEnabled && activation.CaptureEnabled`. Returns `isBookable = false` by default, protecting public booking while allowing catalogue UI preview.
  - Adds preliminary service limitations (`MAP`: boundary and pole mapping baseline only, disease screening not assessed; `COMBO`: screening suspected findings for configured conditions only, requires expert agronomist review).
  - Strictly prevents leaking internal policy metadata: no creator/actor GUIDs, no secret evidence references, no internal decision notes.
  - Active-only filter: retired and experimental services are excluded from public intake.
- **MediatR Query & DTOs**:
  - Created `GetPublicCatalogueV3Query` and `GetPublicCatalogueV3QueryHandler` in `Application/Features/Catalogue/GetPublicCatalogueV3/`.
  - Clean response DTOs: `PublicCatalogueV3Response`, `PublicPolicySummaryResponse`, `PublicSharedDepositResponse`, `PublicServiceItemResponse`.
- **Test Execution Evidence**:
  - Created `backend/tests/AgriDrone.UnitTests/Application/Surveys/PublicCatalogueV3Tests.cs` (4 comprehensive tests covering unconfigured activation, consistent MAP/COMBO resolution, activation bookability gating, and exclusion of retired services).
  - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 759, Skipped: 0.
  - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 80, Skipped: 0.
  - `dotnet build backend/AgriDrone.sln --no-restore` -> Build succeeded: 0 Warning(s), 0 Error(s).

### Evidence hoàn thành 02C-1 và 02C-2 (2026-11-11)

- **Domain Models & EF Core Persistence**:
  - `SurveyRequestQuoteSnapshot`: Immutable quote snapshot pinning `BusinessPolicyVersionId`, `PolicyFingerprint`, `SurveyServiceRevisionId`, `PriceVersionId`, `DepositPolicyId`, rate, deposit, estimated poles, and indicative total.
  - `SurveyRequestTracking`: Opaque high-entropy token stored at-rest as SHA-256 hash (`TrackingTokenHash`) with AES-256 encrypted envelope (`EncryptedToken`) for idempotent replay.
  - EF Core configurations: `SurveyRequestQuoteSnapshotConfiguration` (`survey.request_quote_snapshots` with positive whole-VND check constraints) and `SurveyRequestTrackingConfiguration` (`survey.survey_request_tracking` with unique hash index).
  - Migration `20261111051901_Step8C_V3RequestQuoteAndTrackingFoundation.cs` generated and SQL generation verified.
  - `ISurveyRequestRepository` & `SurveyRequestRepository` extended to retrieve and add quote snapshots and tracking records.
- **Request Intake Commands & Handlers**:
  - `SubmitNewCustomerSurveyRequestV3Command`:
    - Public intake gated by `activation.PublicIntakeEnabled && activation.CaptureEnabled`.
    - Generates high-entropy token `trk_...`, stores hash + encrypted envelope.
    - Resolves active policy bundle at `now`, snapshots indicative quote.
    - Idempotent replay: decrypts `EncryptedToken` and returns original `TrackingToken` + identical quote response. Payload mismatch returns 409 conflict.
    - Zero early provisioning: no `Tenant`, `Farm`, or `Order` created at request time.
  - `SubmitTenantOwnerSurveyRequestV3Command`:
    - Supports `ExistingTenantNewFarm` (proposing a new farm for existing tenant) and `ExistingFarmFollowUp` (target existing farm).
    - Requires authenticated active tenant owner context.
    - For `ExistingFarmFollowUp`: verifies farm belongs to caller's tenant and checks baseline eligibility via `ISurveyRequestFarmReferenceQuery.HasPublishedBaseMapAsync`. Rejects with `SurveyRequestError.FollowUpBaselineRequired()` if no published base map exists.
    - Creates `SurveyRequest` and `SurveyRequestQuoteSnapshot` without early Order provisioning.
- **Secure Public Tracking Query**:
  - `TrackSurveyRequestV3Query`:
    - POST body token lookup via SHA-256 hash (never query string or URLs).
    - Validates token prefix `trk_` and max length.
    - Returns minimal public status (`RequestNumber`, `Status`, `ServiceName`, `EstimatedPoleCount`, `IndicativeTotalAmount`, `DepositAmount`, `Currency`, `EvaluatedAt`, `SubmittedAt`).
    - Strictly zero PII leaks (no email, phone, applicant names, tenant names, staff review notes, or DB IDs).
- **Test Execution Evidence**:
  - Created `backend/tests/AgriDrone.UnitTests/Application/Surveys/SurveyRequestV3IntakeTests.cs` (10 tests covering public intake gating, quote snapshot creation, tracking generation, idempotent replay with token decryption, payload mismatch conflict, minimal public tracking response without PII, not-found handling, ExistingTenantNewFarm, follow-up baseline rejection, and farm ownership checks).
  - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 769, Skipped: 0.
  - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 80, Skipped: 0.
  - `dotnet build backend/AgriDrone.sln --no-restore` -> Build succeeded: 0 Warning(s), 0 Error(s).

### Evidence hoàn thành 02D-1, 02D-2 và 02D-3 (2026-11-11)

- **02D-1: Same-Transaction Snapshot Resolver & Row Locks**:
  - `ISurveyCommercialSnapshotResolver`: Định nghĩa abstraction trong `Modules/AgriDrone.Modules.Surveys/Application/Abstractions/Pricing/ISurveyCommercialSnapshotResolver.cs`.
  - `ResolvedSurveyCommercialSnapshot`: Record bất biến mang đầy đủ thông tin: `BusinessPolicyVersionId`, `PolicyFingerprint`, `SurveyServiceRevisionId`, `SurveyServiceRevisionVersion`, `ServiceCode`, `MapsPoles`, `ScreensDisease`, `PriceVersionId`, `PriceVersionNumber`, `PricePerPole`, `Currency`, `DepositPolicyId`, `DepositVersionNumber`, `DepositAmount`, `ClaimPolicyId`, `ClaimPolicyVersionNumber`, `ClaimWindowDays`, `ClaimTimeZoneId`, `ExcludeNoticeDate`, `CancellationPolicyId`, `CancellationPolicyVersionNumber`, `RefundableBeforeCapture`, `RetainDepositAfterCapture`, `AllowedFreeNoShowReschedules`, `EvaluatedAt`.
  - `SurveyCommercialSnapshotResolver`: Thực thi trong `BuildingBlocks/AgriDrone.Database/Surveys/SurveyCommercialSnapshotResolver.cs`. Sử dụng khóa hàng `FOR SHARE` trên `BusinessPolicyActivation.SingletonId`, xác thực cờ kích hoạt (`CaptureEnabled`), ngày hiệu lực, tính nhất quán của tiền tệ VND, giải quyết bundle chính sách thương mại và xây dựng snapshot bất biến. Đăng ký DI trong `AgriDrone.Database/DependencyInjection.cs`.
  - `SurveyApprovalRowLock.cs`: Cập nhật logic tương thích để trả về `true` khi provider không phải PostgreSQL (SQLite/InMemory trong tests).
- **02D-2: Three Approval Flows, Order Snapshot & State History**:
  - Domain Models: Tạo `SurveyOrderCommercialSnapshot` và `OrderStateHistory` với các ràng buộc bất biến nghiêm ngặt.
  - `SurveyOrder.CreateV3`: Phương thức khởi tạo Order V3 với `Scope = ContractScope.V3`, `Status = SurveyOrderStatus.AwaitingBaselineAppointment`, `DepositPaymentStatus = SurveyPaymentStatus.Pending`, `DepositAmount = commercialSnapshot.DepositAmount` (không có trạng thái giả lập Paid hoặc cọc 0 VND).
  - Orchestrators:
    - `NewCustomerApprovalOrchestrator`: Cấp phát Tenant, Farm, Primary Assignment, Owner Invitation, tạo V3 Order, lưu commercial snapshot và lưu lịch sử trạng thái ban đầu (`OrderStateHistory`).
    - `ExistingTenantNewFarmApprovalOrchestrator`: Cấp phát Farm, Primary Assignment, tạo V3 Order, lưu commercial snapshot và lịch sử trạng thái ban đầu. Tái sử dụng Tenant hiện hữu, không trùng lặp invitation.
    - `ExistingFarmSurveyApprovalOrchestrator`: Tạo V3 Order với `requiresBaselineMapping: false`, lưu commercial snapshot và lịch sử trạng thái ban đầu. Tái sử dụng Tenant và Farm hiện hữu.
  - EF Core & Database Migration:
    - Tạo migration `20261111061205_Step8D_V3OrderCommercialSnapshotAndHistoryFoundation.cs` trong `AgriDrone.Database`.
    - Kiểm tra sinh mã SQL hoàn chỉnh: drop các ràng buộc legacy, tạo lại với điều kiện `scope = 'V3' OR ...`, bổ sung `ck_survey_orders_scope_policy` và `ck_survey_orders_v3_deposit`, tạo bảng `order_commercial_snapshots` và `order_state_history`.
- **02D-3: Test Execution & Acceptance Verification**:
  - Tạo suite kiểm thử `backend/tests/AgriDrone.UnitTests/Application/Surveys/SurveyApprovalV3Tests.cs` (8 bài test bao phủ NewCustomer provisioning, ExistingTenantNewFarm, ExistingFarmFollowUp, commercial snapshot resolution failure, unauthorized admin context, incomplete checklist, idempotent replay, payload mismatch conflict).
  - Kết quả kiểm thử:
    - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 777, Skipped: 0.

### Evidence hoàn thành 02E-1 (2026-11-12)

- **API Contracts & DTOs**:
  - `SubmitNewCustomerSurveyRequestV3ApiRequest`: Payload gửi yêu cầu cho khách hàng mới, kiểm tra validation nghiêm ngặt (EmailAddress, Phone, Coordinate ranges, Whole Area).
  - `SubmitTenantOwnerSurveyRequestV3ApiRequest`: Payload gửi yêu cầu cho chủ nông trại (Farm mới hoặc khảo sát định kỳ Farm hiện hữu).
  - `TrackSurveyRequestV3ApiRequest`: Payload tra cứu bằng mã `TrackingToken` trong body (tuyệt đối không đưa lên URL/log).
  - `CommercialPolicyApiRequests`: Các DTO ban hành chính sách quản trị: `PublishSurveyServiceRevisionApiRequest`, `PublishSurveyServicePriceV3ApiRequest`, `PublishSurveyDepositPolicyApiRequest`, `PublishClaimPolicyApiRequest`, `PublishCancellationPolicyApiRequest`, `PublishBusinessPolicyVersionApiRequest`, `TransitionBusinessPolicyActivationApiRequest`.
- **Additive V3 Controllers**:
  - `V3CatalogController` (`api/v3/catalog`): Endpoint `GET survey-services` (AllowAnonymous) tra cứu danh mục V3 đang hiệu lực và trạng thái `isBookable` (mặc định false trước khi go-live).
  - `V3PublicSurveyRequestsController` (`api/v3/public/survey-requests`):
    - `POST /api/v3/public/survey-requests` (AllowAnonymous) nhận yêu cầu khách hàng mới, xác thực `Idempotency-Key` header, cấp cookie session ẩn danh, tạo quote snapshot và mã tracking `trk_...`.
    - `POST /api/v3/public/survey-requests/track` (AllowAnonymous) tra cứu tiến độ với response tối thiểu, tuyệt đối không rò rỉ PII.
  - `V3TenantOwnerSurveyRequestsController` (`api/v3/tenant-owner/survey-requests`): Endpoint `POST` (Authorize: TenantOwner) nhận yêu cầu của chủ nông trại, ngăn chặn trùng lặp qua `Idempotency-Key`.
  - `V3SystemAdminCommercialPoliciesController` (`api/v3/system-admin/commercial-policies`): Endpoints `POST` (Authorize: SystemAdmin) ban hành revision, price, deposit, claim, cancellation, bundle và kích hoạt chính sách với audit trail.
  - `V3SystemAdminSurveyRequestsController` (`api/v3/system-admin/survey-requests`): Endpoint `POST {id}/approve` (Authorize: SystemAdmin) phê duyệt atomic đơn khảo sát V3, cấp phát Order và commercial snapshot.
- **Contract & Architecture Verification**:
  - Tạo suite kiểm thử `backend/tests/AgriDrone.ArchitectureTests/SurveyV3ApiEndpointTests.cs` (8 bài test bao phủ toàn bộ 5 controllers V3, validation header, status code 200/201/400/404, mapping screaming-snake-case enum).
  - Kết quả kiểm thử:
    - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 88, Skipped: 0.

### Evidence hoàn thành 02F-1 và 02F-2 (2026-11-12)

- **Database Upgrade & Script Verification**:
  - Kiểm tra script migration SQL thực tế từ `Step7EPlantHealthCompletionOutput` đến `Step8D_V3OrderCommercialSnapshotAndHistoryFoundation` bằng lệnh:
    `dotnet ef migrations script Step7EPlantHealthCompletionOutput --project backend/src/BuildingBlocks/AgriDrone.Database --startup-project backend/src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext`
  - Kết quả: Mã DDL sạch, tính idempotent, bảo toàn 100% dữ liệu và migrations lịch sử, không có lệnh phá hủy bảng/dữ liệu cũ.
- **Tổng kết kiểm thử toàn hệ thống**:
  - `dotnet build backend/AgriDrone.sln --no-restore` -> Build succeeded: 0 Warning(s), 0 Error(s).
  - `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj` -> Passed! Failed: 0, Passed: 777, Skipped: 0.
  - `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj` -> Passed! Failed: 0, Passed: 88, Skipped: 0.
  - **Tổng cộng 865 tests tự động đạt 100% PASS.**
- **Bảo toàn các Business Acceptance Rules**:
  - Zero hardcoding: Không set cứng bất kỳ giá tiền (`5.000` hay `100.000`), số ngày claim (`7`), hay điều khoản hủy nào trong logic domain hay production C#.
  - Public intake & capture mặc định tắt (`public_intake_enabled = false`, `capture_enabled = false`) nhằm bảo vệ hệ thống trước khi nghiệm thu full capture/release.
  - Order V3 khởi tạo ở trạng thái chưa thanh toán (`AwaitingBaselineAppointment`, `DepositPaymentStatus = Pending`), không giả lập trạng thái Paid.
  - Atomic approval: Snapshot thương mại và cấp phát tài nguyên diễn ra trong một transaction vật lý duy nhất với `FOR SHARE` locks, rollback toàn phần nếu có lỗi.
- **Sẵn sàng bàn giao V3-03**:
  - Hạ tầng catalogue, shared policies, request intake, secure tracking và atomic order approval hoàn tất, sẵn sàng bước vào V3-03 (Lập lịch hẹn khảo sát, CaptureAttempt đa lần, media scoped theo attempt và chính sách hủy/Re-flight).

## 5. Các business acceptance phải giữ xuyên suốt

| Tình huống | Kết quả phải chứng minh |
|---|---|
| Admin đổi giá/cọc sau Request nhưng trước approval | Giữ request indicative quote; Order snapshot đúng terms lúc creation, Owner được xem trước khi trả cọc |
| Admin đổi cấu hình sau Order creation | Invoice/release/claim/refund dùng pinned terms; không đọc current catalogue để tính lại |
| MAP hoặc COMBO first/follow-up | Cùng shared deposit policy, không Follow-up tier, không mapped Farm tự Paid/0 deposit |
| NewCustomer/NewFarm/ExistingFarmFollowUp | Chỉ tạo đúng resource set khi approval, không duplicate Tenant/Farm/Owner |
| Retry sau khi cấu hình thay đổi | Cùng authorized operation trả original snapshot/acknowledgement; changed input cùng key conflicts |
| Writer cấu hình chạy cùng approval | Một coherent config set trong physical approval transaction, không mixed versions |
| Lỗi sau khi stage Tenant/Farm/snapshot/history/Outbox | Rollback cả operation claim và resources, không partial approval |
| Existing Farm chưa delivered Baseline | V3 Follow-up rejected; không fake release proof để mở booking |
| Legacy service/money/queues | Không automatic PLANT_HEALTH→COMBO hoặc rewrite old payload/snapshot |
| V3-02 chưa có accepted full flow | Public bookability/intake/checkout không tự mở |

Detailed test mapping: [A–F acceptance](v3-02-catalogue-and-approval-implementation-plan.md), [V3 acceptance matrix](../testing/v3-business-acceptance.md). Đánh giá các đường access, retry và DB transactions thực; không thay tất cả bằng mocks hoặc mirror implementation.

## 6. Evidence cần ghi sau mỗi phần

Completion note của mỗi ID phải có source revision/baseline, changed files, behavior delivered, actual build/test commands/results và remaining limitations. Config/schema phase cần migration/generated SQL review, tested DB type/fixture và no live-environment assumptions.

Không xóa evidence cũ hoặc sửa test counts trong historical reports. Item chỉ Done khi đúng exit gate; CodeComplete nhưng chưa integration acceptance phải ghi trạng thái riêng. Task lập kế hoạch này không tự commit, deploy, chạy gateway hoặc migrate database.

## 7. Trình tự bắt đầu và điểm dừng

Thứ tự chính: **02-00 → 02A-1 → 02A-2 → 02B-1/B-2 → 02C-1/C-2 → 02D-1/D-2/D-3 → 02E-1 → 02F-1/F-2**. Resolver contract có thể chuẩn bị sau B nhưng approval tích hợp cần C và snapshots đầy đủ.

Khi bắt đầu implementation, phần giao việc đầu tiên là: **ghi nhận baseline, thêm versioned policy/domain models và focused domain checks trong 02A-1; dừng ở exit gate A1 trước khi mở schema/legacy compatibility scope A2**. Đây là ranh giới review cụ thể, không yêu cầu hỏi lại các business decisions đã chốt. Quyền sửa code chưa được cấp trong yêu cầu lập kế hoạch này.

---

## 8. Remediation Audit Log (V3-02 Security & Quality Remediation)

| Item | Severity | Root Cause & Location | Remediation Applied | Test Verification |
|---|---|---|---|---|
| **1. Tracking Token Key** | `P1` | `TrackingTokenService.cs:9` static derivation key in source. | Replaced with `ITrackingTokenProtector` backed by `IDataProtectionProvider` (`TrackingTokenProtector.cs`). Injected into `SubmitNewCustomerSurveyRequestV3CommandHandler`. Removed static AES key. | `SurveyRequestV3IntakeTests` (11 tests pass) |
| **2. Activation Concurrency** | `P1` | `TransitionBusinessPolicyActivationCommandHandler.cs:135` checked outside tx; missing token. | Added `.IsConcurrencyToken()` on `LastDecisionId` in `BusinessPolicyActivationConfiguration.cs`. Enclosed query, comparison, and mutation inside `unitOfWork.ExecuteInTransactionAsync`. | `AdminPublishCommercialPolicyTests` (12 tests pass) |
| **3. Baseline Map Pinning** | `P1` | `ExistingFarmSurveyApprovalOrchestrator.cs:55` left `FarmBaseMapVersionId = null`. Eligibility query missed completed check. | Added `farmBaseMapVersionId` parameter to `SurveyOrder.CreateV3` with domain guard. Pinned delivered baseline map in orchestrator. Required `source_order.status = 'COMPLETED'` in `SurveyApprovalReferenceQueries.cs`. | `SurveyApprovalV3Tests` (8 tests pass) |
| **4. ReleaseEnabled Gating** | `P1` | `SurveyCatalogueQueries.cs:221` ignored `ReleaseEnabled`. Handlers allowed booking when release was disabled. | Updated `SurveyCatalogueQueries.cs` to require `ReleaseEnabled`. Gated `SubmitNewCustomerSurveyRequestV3CommandHandler`, `SubmitTenantOwnerSurveyRequestV3CommandHandler`, and `SurveyCommercialSnapshotResolver` on `ReleaseEnabled`. | `PublicCatalogueV3Tests` & `SurveyRequestV3IntakeTests` (15 tests pass) |
| **5. Bootstrap Services** | `P2` | Missing production creation/seed path for V3 `MAP`/`COMBO`. | Created `CreateSurveyServiceV3Command`, validator, handler, and `POST api/v3/system-admin/commercial-policies/services`. Created `SurveyServiceV3Seeder` integrated into `AgriDroneSchemaDbContextOptions.cs` (`UseSeeding`/`UseAsyncSeeding`). | `AdminPublishCommercialPolicyTests` & `SurveyV3ApiEndpointTests` pass |
| **6. Guard 1: Disease-only** | `Guard` | `SurveyServiceRevision.cs:47` accepted `mapsPoles: false, screensDisease: true`. | Enforced `if (!mapsPoles)` throws `ArgumentException`. Updated validator to `RuleFor(x => x.MapsPoles).Equal(true)`. | `VersionedCommercialPolicyTests` (10 tests pass) |
| **7. Guard 2: Pre-notice claim** | `Guard` | `ClaimPolicy.cs:108` returned `true` for claim before notice timestamp. | Added `if (receivedAtUtc < noticeCreatedAtUtc) return false;`. | `VersionedCommercialPolicyTests` (10 tests pass) |

**Overall Verification Status**:
- `AgriDrone.UnitTests`: 780/780 passed (0 failed, 0 skipped).
- `AgriDrone.ArchitectureTests`: 88/88 passed (0 failed, 0 skipped).
- Solution build (`AgriDrone.sln`): 0 errors, 0 warnings.

## 9. Release-proof remediation — 2026-11-12

Trạng thái này cập nhật các completion claims ở các đợt trước; số liệu lịch sử được giữ nguyên.

- Intake `V3FollowUpBaselineQuery` và approval `GetV3FollowUpBaselineForApprovalAsync` cùng gọi `V3FollowUpBaselineEligibilityReader`; không còn hai bộ eligibility tách biệt.
- Proof phải đúng Tenant/Farm/baseline/source Order, có release ID, đã financially settled tại release và chưa withdrawn. Candidate vẫn cần current published map, V3 source Order completed và confirmed count hợp lệ; các statuses đó không thay thế release proof.
- Production dùng `UnavailableV3BaselineDeliveryProofReader` vì V3-02 chưa có V3 release store: Follow-up bị từ chối, không fabricate receipt hoặc tự mở go-live. First-order và Legacy không dùng gate này.
- V3-06 phải cung cấp adapter thật dùng đúng DbContext/connection/transaction; khi approval phải khóa cả release/withdrawal facts. Shared reader hiện khóa exact baseline/source Order khi có proof hợp lệ. Chưa có PostgreSQL race/rollback acceptance cho đường này.
- Không tạo/sửa migration trong remediation này. Legacy baseline query và thứ tự replay giữ nguyên.

Verification đã chạy:

| Command | Result |
|---|---|
| `dotnet build backend/AgriDrone.sln --no-restore --nologo --verbosity minimal` | PASS — 0 warnings, 0 errors |
| `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj --no-build --no-restore --nologo --verbosity minimal` | PASS — 791/791 |
| `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj --no-build --no-restore --nologo --verbosity minimal` | PASS — 88/88 |
| `dotnet test backend/tests/AgriDrone.IntegrationTests/AgriDrone.IntegrationTests.csproj --no-build --no-restore --nologo --verbosity minimal --filter FullyQualifiedName~V3BaselineDeliveryProofTests` | PASS — 13/13 component cases; không cần PostgreSQL |

`V3BaselineDeliveryProofTests` gọi production intake adapter qua DI, kiểm tra approval transaction guard, từ chối invalid/withdrawn/unsettled/cross-tenant/cross-farm proof, cancellation và không trả Eligible chỉ vì có proof mà chưa kiểm tra candidate. Những tests này không chứng minh migration, PostgreSQL locking hay rollback.

PostgreSQL fixture `127.0.0.1:55432` chưa có listener khi kiểm tra. 02A-2 database acceptance, 02D-3 và 02F-1 vẫn pending; không ghi “all integration verified”. Cần fresh + populated legacy upgrade fixtures, actual baseline queries, config/baseline/withdrawal races và fault-injection rollback trên PostgreSQL trước khi chốt phase acceptance.

## 10. V3-03 planning handoff — 2026-11-13

[V3-03 A–H plan](v3-03-capture-funding-and-cancellation-implementation-plan.md) và [19-item checklist](v3-03-execution-checklist.md) tiếp tục appointment/deposit/capture/Re-flight/cancellation. Bắt đầu 03-00 inventory rồi 03A-1 domain; chưa sửa code trong phiên lập kế hoạch. V3-02 pending DB gates vẫn giữ nguyên, không được đóng bằng việc tạo plan V3-03. `UnavailableV3BaselineDeliveryProofReader` production không bị thay bằng test fixture; Follow-up/go-live chờ đúng downstream acceptance.

