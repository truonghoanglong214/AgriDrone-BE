# V3-02 — Catalogue, shared policies và atomic Order approval

Plan date: **2026-11-11**. Implementation status (2026-11-12): **Implementation present — PostgreSQL integration acceptance pending**. Design gate: [V3-01 locked design](../domain/v3-01-locked-design.md), [ADR 0019](../adr/0019-v3-01-model-contract-and-migration-design.md). Business source: [rules](../../Codex-Plan/Business-Rules-v3.md); source evidence: [audit](../operations/v3-01-source-audit.md).

Tài liệu này được ban hành ở giai đoạn lập kế hoạch. Các mục “future work”/“chưa tạo” bên dưới mô tả thời điểm ban hành; trạng thái triển khai và evidence hiện tại nằm trong execution checklist. Public activation vẫn off. V3 Follow-up chưa thể mở production khi chưa có durable release proof; không dùng `Completed` + `Published` thay bằng chứng bàn giao.

Checklist thực thi hiện hành: [V3-02 execution checklist](v3-02-execution-checklist.md), chia 13 work items có output/dependency/status và exit gate chi tiết cho 02A-1. Tài liệu A–F này giữ vai trò technical specification; checklist là bảng theo dõi thực hiện, không thay quyết định V3-01.

## 1. Outcome và ranh giới

V3-02 phải cung cấp MAP/COMBO catalogue và một shared deposit policy, versioned commercial bundle, public/existing-owner requests, secure tracking, atomic approval + Order commercial snapshots/history và explicit activation gate.

Không triển khai full capture/Re-flight pipeline, disease ingestion mới, result release, claim/refund transfer hoặc dashboard trong phase này. Chốt đủ references/interfaces để phase sau nối được; không create hết tables của các phase sau chỉ để làm schema đầy đủ.

**Public activation giữ off.** Intake/checkout tiền thật chỉ mở khi required capture/release/visibility/claims/refund flow và rollout acceptance đã đạt. Các tests phase dùng isolated fixture/configuration, không tự bật production hoặc giả operational readiness evidence.

## 2. Dependency checklist trước code

- [x] MAP/COMBO và shared deposit, demo amounts, no Follow-up tier đã chốt.
- [x] Policy/snapshot cadence, version lineage, legacy scope/enum strategy và `/api/v3` route boundary đã chốt tại V3-01.
- [x] New capture attempt và internal-map/settlement-release seams đã chốt cho các phase kế.
- [ ] Lưu source baseline/diff khi bắt đầu implementation để không ghi đè working tree của chủ dự án.
- [ ] Inspect actual DB schema/enums/triggers/constraints nếu sẽ chạy migration; chưa có evidence live DB từ design phase.
- [ ] Record migration plan/generated SQL diff và rollback/roll-forward review trước mọi DB change. Không cần bật gateway/AI để triển khai policy foundation.

## 3. V3-02A — Persistence và commercial policy foundation

Đây là **bước code đầu tiên khi được phép implementation**.

### A1. Service definitions và legacy compatibility

- Reuse `survey.survey_services` và `survey.survey_service_prices`, giữ mọi existing ID/value.
- New v3 MAP/COMBO records dùng versioned service revisions/capabilities và immutable code; legacy `service_type` nullable chỉ cho v3 scope có complete definition. Không thêm MAP vào old PlantHealth enum hoặc seed lại existing service GUIDs.
- Explicit immutable ContractScope phân biệt LEGACY/V3; thiếu required v3 snapshot là invalid/quarantine, không tự downgrade thành legacy dựa null field. New service/Order scope do server factory quyết định, không nhận override từ client.
- Old queries/routes explicitly legacy-filter trước projection nullable enum; architecture/contract checks bảo vệ old payload shape. V3 business code không gọi old `IsSellable(PlantHealth)` làm gate.
- V3 service publication chỉ Active valid definitions. Experimental/Retired không nhận new commercial requests, không tự reuse permissive Active/Experimental logic hiện tại.

### A2. Versioned configuration

- Add SurveyServiceRevision, SurveyDepositPolicy, ClaimPolicy, CancellationPolicy, BusinessPolicyVersion và activation/history records theo locked design.
- Reuse PricePerPole/decimal semantics; v3 positive whole-VND validation, no global schema coercion trên legacy money.
- Effective-window constraints, unique stream version, predecessor same stream, once-only EffectiveTo closure + audit. Không silently fallback version mới nhất khi có overlap/missing required dependency.
- Bundle pins shared deposit/claim/cancellation/service revisions; selected service price resolve trong cùng snapshot transaction. Config publishing và resolving share activation guard lock order.

### A3. Persistence integration

- Register policy entities trong SurveysDbContext, canonical AgriDroneSchemaDbContext và approval DbContext khi phase D sử dụng; migrations thuộc schema context hiện có.
- Extend existing Order checks theo immutable policy scope. Legacy constraints về baseline-only cọc, price/count cùng lúc, required mapped fields không được áp nguyên xi cho new v3 Order có cọc nhưng chưa billable count. Preserve legacy constraints cho legacy rows.
- New seed/config provisioning phải có real authorized actor/system bootstrap identity và reason; không fake FK GUIDs. Demo values chỉ data `1000/2000/100000`, ClaimPolicy days `7`, no-show allowance `1` đã chốt; unknown spatial thresholds không seed tạm.

Exit A: model build/schema diff reviewed, empty/legacy-upgrade DB fixtures, nullable enum isolation, version/window/lineage/integer-money checks. A chưa đổi public commercial intake.

## 4. V3-02B — Admin publish và public catalogue

- Add commands publish new service-definition/price/deposit/claim/cancellation/bundle versions; existing v3 price command phải cùng config guard protocol, không bypass bằng old endpoint.
- Admin input có expected version, amount/config body, reason/evidence; server owns actor/time/predecessor/version, window closure và activation transition.
- MVP chỉ immediate effective publishing với server time; future scheduling nếu chưa hỗ trợ thì reject rõ, không silently normalize client date. Effective interval `[from,to)`.
- Public v3 catalogue response: service ID/code/revision/name/description/capabilities, unit-price version/amount/VND, one shared deposit version/amount, policy version/evaluatedAt, preliminary limitation và `isBookable` từ actual activation gate.
- Nonbookable preview có thể hiển thị giá nhưng request/payment mutation bị gate; không để FE suy giá demo đồng nghĩa đã launch.
- Query không leak internal policy evidence/staff/secret data. Read required bundle/price/deposit nhất quán; no mixed version response khi Admin đổi config đồng thời.

Exit B: publication/replay/stale writer/overlap races, exactly one deposit stream, Active-only choices, no Experimental commercial intake, no C# money fallback.

## 5. V3-02C — Request intake, quote và tracking

- Three explicit request kinds: NewCustomer, ExistingTenantNewFarm, ExistingFarmFollowUp. Reuse existing handlers/validation/caller-scope/idempotency seams qua v3 contracts; don't change old JSON snapshots.
- Submit atomically stores Request + indicative quote snapshot/source versions + initial request history + acknowledgement/outbox/idempotency result. No Tenant/Farm/Order provisioning.
- ExistingOwner requests resolve own Tenant/Farm and activation; Follow-up requires pinned delivered Baseline/current eligible proof, independent of selected MAP/COMBO price stream.
- Request quote not binding price; approval will take then-current commercial terms. Show Order snapshot before deposit, retain both quotes for audit.
- Public tracking token is opaque high-entropy, lookup by stored hash; never token in query string/log/metrics or Admin list. Minimal status/decision endpoint uses body token, rate limits và redacted failures.
- Idempotent acknowledgement must safely replay token to same authorized caller/key without storing plaintext secret broadly: encrypted acknowledgement receipt with bounded retention/protected key handling. Do not store only a nonrecoverable token hash then promise identical lost-response replay.
- Changed input same idempotency key conflicts; different public caller scope cannot fetch another acknowledgement. Cross-version command fingerprint prevents unintended legacy/v3 key reuse.

Exit C: no early provisioning, secure lost-response replay, tracking privacy, config missing/changed scenarios and Owner scope validation.

## 6. V3-02D — Same-transaction resolver và three approval flows

### D1. Snapshot port and locking

Define `ISurveyCommercialSnapshotResolver` and immutable `ResolvedSurveyCommercialSnapshot` under Surveys Application abstraction, implemented in `AgriDrone.Database/Surveys` using existing approval connection/transaction. It does not use cached catalogue/current config through another DbContext.

Sequence:

1. Authorize Admin; lock Request and claim existing idempotency operation using current seam. Replay returns original saved snapshot after authorization.
2. Acquire config activation shared lock; capture single server evaluation time; resolve selected service revision/price/global deposit/claim/cancellation/bundle and check activation.
3. Validate request kind, Owner/Farm/Manager/Baseline reference under same approval transaction and stable lock order.
4. Build immutable snapshot with version IDs, resolved amounts/terms, currency, policy fingerprint; reject missing/overlap/retired/mismatched currency before provisioning.
5. Pass snapshot as required factory/orchestrator input. No `DefaultBaselineDepositAmountVnd`, no bool-mapped Farm switch setting deposit Paid/0.

### D2. Provisioning and histories

| Kind | Writes allowed |
|---|---|
| NewCustomer | Tenant + Farm + Order/commercial snapshot + primary assignment + Owner invitation + request decision/Order history + audit/Outbox/idempotency receipt |
| ExistingTenantNewFarm | New Farm/Order/snapshot/assignment + existing Owner/Tenant references; no duplicate account/Tenant/invitation |
| ExistingFarmFollowUp | New paid Order/snapshot + pinned delivered Baseline/assignment references; no new Tenant/Farm or artificial paid state |

Initial v3 Order: AwaitingAppointment, deposit unpaid, no finalized count/Total/release/window. `BusinessPolicyVersionId` and snapshot are required for v3; legacy rows remain unchanged. Initial OrderStateHistory and existing request review/predecessor records persist in same transaction, no second logger DbContext commit.

### D3. Failure/idempotency

- Inject failure after Request approval/resource/snapshot/history/invitation/audit/Outbox/receipt writes → full rollback, including operation claim.
- Concurrent approvals have global Request→Order uniqueness even across route versions; no second Order by changing Idempotency-Key.
- Config writer locks activation exclusive; approval pins a complete version set, never mix old deposit with new bundle in a race.
- Exact retry after catalogue update returns original price/deposit/policy snapshot. Changed fingerprint conflicts; revoked caller denied.
- Baseline withdrawn/replaced concurrently: approval either pins still-eligible released version under lock or fails; no half-approved Follow-up.

Exit D: meaningful DB/concurrency/rollback checks with existing UoW; no long network call inside transaction, no real payment performed.

## 7. V3-02E — API contracts, DI và integration handoff

- Additive `/api/v3/catalog`, `/api/v3/public`, `/api/v3/tenant-owner`, `/api/v3/system-admin` route families. Existing SePay callback path unchanged. New DTO service code/capability structure does not expose legacy enum.
- Extend actual approval replay response/audit/receipt with snapshot IDs and server amounts; no invitation token/password/PII leak. Internal tests do not bypass activation on public endpoints.
- Register new resolver/repositories/query handlers on same appropriate scopes and DbContexts; no fake `AgriDrone.Modules.Payments` dependency.
- New OpenAPI export only after implementation runtime exists; preserve current JSON fixtures/snapshots. Compare against [API intent](../openapi/survey-business-v3.openapi-intent.md) and [locked contracts](../contracts/survey-business-v3.md).
- Refresh FE handoff with actual routes/fields/versions and bookability status. No call to capture/release consumer under old PlantHealth-only contract for new MAP/COMBO Order.

Exit E: startup/DI/API versioning/auth/validation/idempotency documented with actual evidence; downstream phase contracts ready, public go-live still gated.

## 8. V3-02F — Migration review, meaningful checks và phase handoff

Migration bundles are target scopes, not exact generated timestamp/class names:

1. **Expand commercial config:** service revisions, scoped legacy discriminator, price lineage, policy tables/activation, effective-window/index/FK guards. Keep current enum values/old seed rows.
2. **Expand v3 intake/approval:** quote/tracking snapshot, Order commercial snapshot/history/policy refs, old/new scope constraints and idempotency replay shape versioning.
3. **Provision demo configuration:** admin/system-authorized version records/bundle with public intake off; missing actor/key/evidence fails instead of fake IDs.

Before applying: read-only inventory actual DB, inspect old trigger/constraints/enum mappings, generate upgrade SQL and test fresh + representative populated legacy fixtures. No rewrite existing migration files/model history. Populated Down should refuse destructive rollback; roll-forward once commercial facts exist.

### Acceptance mapping

| Checks | Existing acceptance / focus |
|---|---|
| Configuration data, integer money, version lineage and no hardcoded runtime defaults | V3-T01, T05; targeted domain/DB rules |
| Request privacy/quote/idempotent tracking | V3-T02; lost acknowledgement replay, no PII leak |
| Approval snapshots/rollback/concurrency | V3-T03/04/05; failure checkpoints and real same-transaction reads |
| Access + config writer/approval race | V3-T28; Admin/Owner/caller scope, shared guard lock protocol |
| Old contracts/nullable enum/legacy upgrade isolation | V3-T33/34; no automatic PLANT_HEALTH→COMBO |
| Activation gate | Requests/deposit checkout cannot go live on a V3-02-only runtime |

Tests phải xác minh invariant và DB behavior, không chỉ mirror handler output with mocks. Run focused checks once after relevant edits; broader build/architecture/integration at integration boundary. Record failures/limitations accurately; do not claim all v3 tests passed because catalogue tests pass.

## 9. Future file manifest — existing edit locations

Các đường dẫn là inspected source, link chỉ để reviewer định vị; đợt plan không sửa chúng.

| Area | Existing files / responsibility |
|---|---|
| Catalogue domain | [SurveyService.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyService.cs), [SurveyServicePrice.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServicePrice.cs), [SurveyServiceMvpPolicy.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServiceMvpPolicy.cs), [SurveyServiceType.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServiceType.cs): legacy/v3 scope separation, no reinterpret enum |
| Request/Order | [SurveyRequest.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Requests/SurveyRequest.cs), [SurveyOrder.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs): quote/snapshot inputs, initial history, no fixed cọc |
| Catalogue query ports/adapters | [ISurveyCatalogueQueries.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Abstractions/Queries/ISurveyCatalogueQueries.cs), [SurveyCatalogueQueries.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Queries/SurveyCatalogueQueries.cs): consistent v3 config and legacy filters |
| Price publishing | [CreateSurveyServicePriceCommandHandler.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Catalogue/CreateSurveyServicePrice/CreateSurveyServicePriceCommandHandler.cs): scope-aware publish/locks/history, no PlantHealth-only global gate |
| Request handlers | [NewCustomer](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Requests/SubmitNewCustomerSurveyRequest/SubmitNewCustomerSurveyRequestCommandHandler.cs), [NewFarm](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Requests/SubmitNewFarmSurveyRequest/SubmitNewFarmSurveyRequestCommandHandler.cs), [ExistingFarm](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Requests/SubmitExistingFarmSurveyRequest/SubmitExistingFarmSurveyRequestCommandHandler.cs): v3 request/quote scope, no provisioning |
| Approval application | [Prerequisite resolver](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/Common/SurveyApprovalPrerequisiteResolver.cs), [prerequisite/compatibility helpers](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/Common/SurveyApprovalPrerequisites.cs), [approval handler](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/ApproveSurveyRequestCommandHandler.cs): inject resolved snapshot/current permissions, update replay/audit shape |
| Three orchestration flows | [NewCustomer](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/NewCustomerApprovalOrchestrator.cs), [NewFarm](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/ExistingTenantNewFarmApprovalOrchestrator.cs), [ExistingFarm](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/ExistingFarmSurveyApprovalOrchestrator.cs): required common snapshot factory input |
| Approval transaction | [SurveyApprovalDbContext.cs](../../backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyApprovalDbContext.cs), [reference queries](../../backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyApprovalReferenceQueries.cs), [UoW](../../backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyApprovalUnitOfWork.cs): new policy/snapshot sets, guard locks/same transaction |
| Persistence/configuration | [SurveysDbContext.cs](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/SurveysDbContext.cs), [Service configuration](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyServiceConfiguration.cs), [Price configuration](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyServicePriceConfiguration.cs), [Order configuration](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderConfiguration.cs): scoped checks/FKs/lineage; [schema context](../../backend/src/BuildingBlocks/AgriDrone.Database/AgriDroneSchemaDbContext.cs) owns new migrations |
| PostgreSQL mapping | [PostgreSqlEnumMappings.cs](../../backend/src/BuildingBlocks/AgriDrone.Database/PostgreSqlEnumMappings.cs): audit every registration/converter assuming nonnullable legacy enum; preserve released enum labels/ordinals |
| API composition | [PublicSurveyServicesController.cs](../../backend/src/AgriDrone.Api/Controllers/PublicSurveyServicesController.cs), [PublicSurveyRequestsController.cs](../../backend/src/AgriDrone.Api/Controllers/PublicSurveyRequestsController.cs), [SystemSurveyServicesController.cs](../../backend/src/AgriDrone.Api/Controllers/SystemSurveyServicesController.cs), [SystemSurveyRequestsController.cs](../../backend/src/AgriDrone.Api/Controllers/SystemSurveyRequestsController.cs), [Surveys DI](../../backend/src/Modules/AgriDrone.Modules.Surveys/DependencyInjection.cs): coexist v3 route/controllers/DTOs without breaking legacy snapshots |

### New file groups đã chọn, chưa tạo

- `Surveys/Domain/Catalogue/SurveyServiceRevision.cs`; `Domain/Policies/{SurveyDepositPolicy,ClaimPolicy,CancellationPolicy,BusinessPolicyVersion,BusinessPolicyActivation}.cs`.
- `Surveys/Domain/Orders/{SurveyOrderCommercialSnapshot,OrderStateHistory}.cs`; `Domain/Requests/SurveyRequestQuoteSnapshot.cs` và tracking-secret acknowledgement receipt fields under existing idempotency boundary.
- `Surveys/Application/Abstractions/Pricing/{ISurveyCommercialSnapshotResolver,ResolvedSurveyCommercialSnapshot}.cs` và new config repositories/query abstractions.
- `Database/Surveys/SurveyCommercialSnapshotResolver.cs`, new policy EF configurations và additive generated migrations trong canonical Database project.
- `Surveys/Application/Features/Policies/` publish/get/history use cases; v3 request-tracking query handlers; `AgriDrone.Api/Contracts/Surveys/V3/` và corresponding versioned controllers.
- Focused tests dưới existing UnitTests/IntegrationTests/ArchitectureTests projects; no new test framework/module required.

## 10. Suggested review/commit units

| Unit | Concrete scope | Dependency |
|---|---|---|
| 02A-1 | Versioned service/policy entities + config guard/lineage/value invariants | Locked design |
| 02A-2 | EF configurations/new expand migration + populated legacy compatibility | 02A-1 |
| 02B | Admin publishing + coherent public catalogue + activation off | 02A-2 |
| 02C | Requests/quotes/secure tracking + idempotent acknowledgement | 02B |
| 02D | Same-transaction resolver + 3 approval flows/snapshots/history | 02C |
| 02E/F | API/DI/exported actual schema, focused security/DB/concurrency evidence + handoff | 02D |

Không commit tự động trong task lập kế hoạch. Khi implementation bắt đầu, chỉ làm **02A-1 trước**, sau đó schema/compatibility review 02A-2; không nhảy thẳng sang checkout/release hay reset Mission.

Ranh giới 02A-1: chỉ new versioned domain/policy models và focused domain checks; chưa đổi legacy enum nullability, existing Order factory, DbContext, schema, seeds hoặc HTTP routes. Những thay đổi compatibility cần compile/integration đồng thời nằm trong 02A-2 theo [checklist](v3-02-execution-checklist.md).

## 11. Definition of Done và phần tiếp theo

V3-02 đủ technical acceptance khi valid MAP/COMBO configuration có thể tạo đúng three-kind requests/Orders/snapshots/history atomic qua tested v3 contracts, replay giữ terms gốc và legacy behavior được isolate. No runtime hardcoded business defaults, no false paid/full count/release fields.

Ghi rõ public activation vẫn off nếu full flow chưa accepted. V3-03 tiếp theo là appointment + all-Order deposit readiness + new CaptureAttempt/new Mission/attempt-scoped media + cancellation/Re-flight policy; V3-06 mới là settlement/release. Các bước đó không là code đã hoàn thành chỉ vì V3-02 plan hoặc schema đã có.
