# BE1 — Unified Business Phase và .NET Implementation Plan

> Ngày lập: 2026-09-25  
> Rebaseline nghiệp vụ và kiến trúc .NET: 2026-10-06  
> Hợp nhất kế hoạch phase và implementation: 2026-10-07  
> Nguồn nghiệp vụ cao nhất: `Codex-Plan/Be-Plan.md`  
> Use case nguồn: `Codex-Plan/BE1-Use-Case-Specification.md`  
> Kế hoạch kỹ thuật hiện hành: tài liệu này trên `backend/AgriDrone.sln`  
> Phạm vi: phase/status/evidence từ Phase 0–15 và backlog triển khai chi tiết từ Step 0–11.

**Trạng thái tài liệu:** đây là kế hoạch BE1 duy nhất; phase roadmap, baseline/evidence và implementation backlog đã được hợp nhất tại đây. Các đường dẫn `.cs`, `.csproj`, `DbContext`, EF Core configuration, MediatR/FluentValidation handler và file manifest trong tài liệu này là target implementation hiện hành. Mọi thay đổi tiếp tục trên .NET modular monolith; không có bước port BE1 sang Java/Spring Boot.

Phase 0–6 và Core Step 0 là baseline đã có evidence. Source audit ngày 2026-10-07 xác nhận Core Step 1A runtime wiring chưa hoàn thành. Trước Step 1B phải hoàn thành rebaseline delta 2026-10-06 cho per-pole pricing, purpose-aware readiness, FarmBoundary, Disease Zone/recommendation và owner-reported plant change; sau đó hoàn thành Step 1A rồi tiếp tục Step 1B–Step 11 trực tiếp trên .NET.

## 1. Mục tiêu

Tài liệu này chuyển toàn bộ `BE1-Use-Case-Specification.md` thành backlog .NET có thứ tự, dependency, API intent, transaction boundary, test và exit gate cụ thể.

Kết quả cuối cùng phải hoàn thành ba entry flow và một execution flow chung:

```text
NewCustomer
→ Public Survey Request
→ SystemAdmin approve
→ Tenant + Farm + SurveyOrder + primary manager assignment + Owner invitation

ExistingTenantNewFarm
→ TenantOwner Survey Request
→ SystemAdmin approve
→ Farm + SurveyOrder + primary manager assignment trong Tenant hiện hữu

ExistingFarmSurvey
→ TenantOwner Survey Request
→ SystemAdmin approve
→ SurveyOrder cho Farm hiện hữu + previous compatible order nếu có

Flow chung sau approval
→ SystemManager verify FarmBoundary/scope
→ nếu Farm chưa có base map: Baseline appointment/mission chạy trước payment
→ SystemManager publish base map và confirm active pole count
→ BE1 snapshot PricePerPole + pole count và tính FinalPrice
→ paid-service appointment được confirm/reschedule
→ TenantOwner initiate payment; provider/SystemAdmin xác nhận hợp lệ
→ BE1 mở paid-service readiness gate
→ BE2 thực hiện mission/AI
→ BE1 review/publish Plant Health/Disease Zones/recommendations hoặc Harvest Readiness result
→ TenantOwner xem published map/profile/history và gửi pending plant-change report khi cần
```

Không flow nào được xem là hoàn thành nếu còn đường bypass Survey Request, SurveyOrder, appointment, payment, primary manager assignment hoặc human publication gate.

## 2. Phạm vi và ownership

### BE1 phải triển khai

- Survey Service, effective-dated PricePerPole, disease/severity/recommendation catalogue và business master data.
- Ba loại Survey Request và SystemAdmin review.
- Atomic approval/onboarding/SurveyOrder orchestration.
- FarmBoundary/scope, Baseline Mapping, confirmed pole-count/price snapshot, appointment, payment và purpose-aware readiness policy.
- Farm/Zone operational verification theo SystemManager assignment.
- Farm-level base-map publication và persistent Plant identity.
- Plant Health, Disease Zone/recommendation và Harvest Readiness human review/publication.
- TenantOwner plant-inventory change report và manager verification/later-survey evidence workflow.
- TenantOwner published-only queries, Digital Plant Profile, history và follow-up.
- Business outbox events, audit và actor-specific work queues.

### BE1 không triển khai trong plan này

- Drone registry, Mission state machine, pre-flight, media, telemetry hoặc AI job orchestration.
- AI detection, tracking, re-identification algorithm hoặc raw observation storage thuộc BE2.
- Worker/FieldTask/offline domain sync legacy.
- Harvest season, batch, quantity, yield hoặc post-harvest management.
- Web/mobile UI.

BE1 vẫn phải cung cấp/tiêu thụ contract rõ ràng để BE2 hoàn tất các phần phụ thuộc.

## 3. Nguyên tắc thực hiện

1. `Be-Plan.md` và ADR đã Accepted thắng mọi tài liệu legacy khi có xung đột.
2. Không đánh dấu use case `Done` chỉ vì entity, table hoặc EF configuration đã tồn tại.
3. Mỗi mutation phải có domain invariant, authorization, persistence, audit và idempotency/concurrency khi cần. Test tương ứng được ghi vào Final Test Backlog và chỉ viết/chạy ở phase kiểm thử cuối.
4. System actors không phụ thuộc TenantMembership; TenantOwner không nhận quyền system actor.
5. Client không được gửi trạng thái derived như `ReadyForOperations`, `Payment.Confirmed` hoặc `Official`; readiness phải phân biệt Baseline Mapping với selected paid-service mission.
6. Không gọi controller/module khác qua HTTP nội bộ; dùng explicit application port hoặc specialized cross-module unit of work.
7. Transaction tạo nhiều aggregate bắt buộc phải có boundary rõ; không nối nhiều `SaveChanges` rồi hy vọng tất cả thành công.
8. Producer dùng PostgreSQL Outbox; consumer/callback dùng Inbox hoặc deduplication record. RabbitMQ/provider chỉ là transport/source event.
9. Event V1 immutable. V2 dùng contract/routing/consumer riêng và chỉ xóa V1 sau drain/retention/replay gate.
10. TenantOwner query luôn filter ownership và publication state; pending/raw/rejected data không được lộ.
11. Không sửa migration đã phát hành. Mỗi thay đổi dùng expand → backfill/validate → contract; fresh/upgrade database test được gom và chạy ở phase kiểm thử cuối.
12. Không khôi phục code/API FieldTasks, Harvests, TenantAdmin/Member hoặc Farm Manager/Worker để giải quyết nhanh dependency mới.

## 4. Phase status, baseline và evidence

### 4.1. Trạng thái phase thống nhất

| Phase | Nội dung | Step thực thi | Trạng thái | Gate chuyển tiếp |
|---:|---|---:|---|---|
| 0 | Baseline, ADR, inventory và safety net | Step 0 foundation | Done 2026-09-22 | Evidence/test baseline đã lưu |
| 1 | Đóng entry point sai nghiệp vụ | Baseline | Done 2026-09-23 | Không còn public/tenant bypass tạo resource cũ |
| 2 | SystemManager và primary Farm assignment | Baseline | Done 2026-09-23 | Assignment/authorization không dựa FarmMembership |
| 3 | System-owned Drone | BE2 baseline dependency | Done 2026-09-23 | Global availability và cross-tenant conflict pass |
| 4 | Cleanup FieldTasks/Harvests/staff surface | Baseline | Done 2026-09-25, còn external retention gates | Runtime detached; archival data chưa được drop tùy tiện |
| 5 | Survey/base-map/result database foundation | Step 0 foundation | Done theo snapshot 2026-09-25 | Không bao gồm rebaseline delta 2026-10-06 |
| 6 | Internal ports, policies và authorization boundary | Step 0/1A foundation | Done 2026-09-25 | Không mở business API mới |
| 6R | Per-pole, purpose readiness, FarmBoundary, Disease Zone/recommendation, plant-change | Step 0R | **CodeComplete — AwaitingFinalTest** | Code/migration/contract/documentation hoàn tất; chỉ Final Test Phase được chuyển `Done` |
| 7 | Catalogue/master data và Survey Request | Step 1B–1E, Step 2 | **CodeComplete — AwaitingFinalTest** | Step 1/2 implementation và OpenAPI hoàn tất; submit không provision resource |
| 8 | Approval/onboarding/SurveyOrder | Step 3 | **InProgress — 3B CodeComplete** | Ba request kinds atomic và retry-safe |
| 9 | Boundary/scope, baseline count, pricing, appointment, payment | Step 4 | NotStarted | Purpose-aware readiness không bypass được |
| 10 | Order-bound Mission/pre-flight | BE2 implementation + Step 5 BE1 boundary | External dependency | BE2 dùng authoritative BE1 context/readiness contract |
| 11 | Initial Farm base map và persistent Plant identity | Step 6 | NotStarted | Hoàn tất publication V3 atomic, review và count handoff |
| 12 | Plant Health/Disease Zone/recommendation và Harvest Readiness | Step 7–8 | NotStarted | Chỉ human-reviewed Published result lộ cho owner |
| 13 | Digital Plant Profile, plant-change workflow, history/follow-up | Step 9–10 | NotStarted | Hoàn tất published-only, same-service và ownership behavior |
| 14 | Residual cleanup và release preparation | Step 11A–11B | NotStarted | Cleanup/retention/drain và tài liệu sẵn sàng kiểm thử |
| 15 | Final Test Phase | Step 11C–11E + mục 27 | NotStarted | Viết toàn bộ test còn thiếu, rồi chạy test một lần theo thứ tự release |

Quy tắc điều phối:

1. Dùng bảng trên để chọn phase hiện tại; dùng Step tương ứng bên dưới để code.
2. Không bắt đầu Phase 7 trước khi Phase 6R hoàn thành.
3. Baseline Mapping có thể chạy trước payment để tạo confirmed pole count; selected paid-service Mission không được chạy trước Phase 9 commercial gate.
4. Không làm official result Phase 12 trước khi Phase 11 cung cấp current Farm base map và persistent Plant identity.
5. Trong các phase code, có thể ghi `CodeComplete — AwaitingFinalTest` khi implementation/migration/documentation đã xong. Chỉ đổi sang `Done` sau Final Test Phase có evidence thực tế; entity/table/interface hoặc build pass đơn lẻ không đủ.

### 4.2. Baseline đã hoàn thành — giữ lại, không làm lại

Theo Phase 0–6 và source code hiện tại:

- [x] System actor context, `SYSTEM_MANAGER`, profile, qualification, availability và assignment history.
- [x] `ISystemManagerAccessService` và assigned-Farm query không phụ thuộc TenantMembership.
- [x] System-owned Drone foundation và global availability thuộc BE2.
- [x] Legacy self-registration/direct Farm/tenant operational routes đã đóng hoặc bị loại khỏi target surface.
- [x] FieldTasks/Harvests runtime đã bị tháo khỏi target solution; archival schema vẫn chờ external retention gate.
- [x] Internal Tenant/Farm/Owner provisioning ports đã tồn tại.
- [x] Shared geometry, money/currency/rounding, idempotency và `SurveyOrderReadinessPolicy` foundation đã tồn tại.
- [x] `AgriDrone.Modules.Surveys` đã có domain/persistence model cho:
  - Survey Service và legacy price foundation; per-pole target đã hoàn thành code/migration ở Step 0R và đang chờ Final Test Phase.
  - Survey Request/review.
  - SurveyOrder.
  - Appointment, Payment, PaymentEvent và PriceAdjustment.
  - SurveyResult và HarvestReadinessAssessment.
- [x] Database đã có `survey` schema, `farm_base_map_versions`, order/mission association, mission purpose và pre-flight foundation.
- [x] `FarmBaseMapVersion`, Farm provisioning seam, Mission order context và mapping publication seam đã được chuẩn bị.
- [x] Business Phase 5 migration theo snapshot 2026-09-25 và fresh/upgrade PostgreSQL verification đã có evidence; không bao gồm rebaseline delta 2026-10-06.

Các phần trên được mở rộng, không viết lại từ đầu.

### 4.3. External/manual gates còn deferred

Các mục sau không phải công việc code để tự động “xử lý cho xong”; phải có inventory production-like và quyết định dữ liệu/retention:

- [ ] Rerun FieldTasks/Harvests inventory trên snapshot production-like; lưu export/checksum trước mọi contract migration.
- [ ] Chỉ drop `field_task`/`harvest` tables, indexes và enums sau retention approval; hiện chỉ được giữ archival/read-only ngoài runtime.
- [ ] Disposition thủ công năm TenantAdmin/Member và ba Farm Manager records trong Phase 0 report; không auto-promote thành SystemManager/TenantOwner.
- [ ] Archive access history và revoke quyền cũ sau quyết định disposition.
- [ ] Chỉ drop `farm_memberships`, `zone_assignments` và enum liên quan khi không còn unresolved row/reference.

Không được đánh dấu các gate này `Done` dựa trên database test/local trống.

### 4.4. Completion evidence Phase 0–6R

| Phase | Evidence chính |
|---:|---|
| 0 | `docs/domain/be-plan-glossary.md`, `docs/operations/be-plan-phase0-baseline-report.md`, `docs/operations/be-plan-phase0-legacy-data-disposition.md`, `docs/openapi/be-plan-phase0.openapi.json` |
| 1 | `docs/operations/be-plan-phase1-report.md`, `docs/operations/be-plan-phase1-legacy-endpoint-cutover.md`, `docs/openapi/be-plan-phase1.openapi.json` |
| 2 | `docs/operations/be-plan-phase2-report.md`, `docs/operations/be-plan-phase2-legacy-manager-mapping.md` |
| 3 | `docs/operations/be-plan-phase3-report.md`, `docs/operations/be-plan-phase3-drone-preflight.sql` |
| 4 | `docs/operations/be-plan-phase4-report.md`, `docs/operations/be-plan-phase4-legacy-inventory.sql`, `docs/openapi/be-plan-phase4.openapi.json` |
| 5 | `docs/operations/be-plan-phase5-report.md`, `docs/operations/be-plan-phase5-preflight.sql`, `20260925100711_Phase5SurveyDatabaseFoundation` migration |
| 6 | `docs/operations/be-plan-phase6-report.md`, `Phase6BoundaryTests.cs`, `Phase6DomainSeamsTests.cs`, `SurveyPoliciesTests.cs` |
| 6R | `docs/operations/be1-step0r-completion-report.md`, các `be1-step0r-*-migration-report.md`, `docs/openapi/be1-step0r.openapi-intent.md`, ADR-0007–0011 |

## 5. Khoảng trống code hiện tại

Khảo sát source tại thời điểm lập plan cho thấy:

| Khu vực | Hiện trạng | Việc còn thiếu |
|---|---|---|
| Surveys project | Đã có Domain, EF configurations, authorization policy, runtime DI và transaction-port nền tảng | Feature handlers từ Step 1B trở đi và các repository/query theo use case |
| Survey domain | Aggregate đã sở hữu transition rules, stable domain error codes, version guard và các value object nền tảng | Bổ sung factory/invariant theo từng use case ở Step 1–9; không tạo lại central state-machine/error file |
| Surveys persistence | `SurveysDbContext`, module UoW, SurveyService repository, catalogue query service, audit/outbox và health check đã được runtime đăng ký | Repository/query theo các use case sau, compiled/batch query khi có evidence cần thiết |
| API | Đã reference và gọi `AddSurveysModule`; chưa có Survey controllers/contracts mục tiêu | Public, SystemAdmin, SystemManager, TenantOwner và payment callback endpoints |
| IntegrationContracts | V1/V2 baseline được giữ bất biến; V3 target cho mapping/health được chốt lại ở Step 0R | Kết nối producer/consumer thật ở Step 4, 6, 7 và 8; theo dõi drain V1/V2 |
| Approval | Có internal provisioning ports | Atomic cross-module approval orchestration và retry recovery |
| Results | Có SurveyResult/Readiness schema | Health association/import, review/correction/publication handlers và published-only queries |
| Owner portal queries | Chưa có target read models | Farm map, plant count, profile timeline, survey history, same-service comparison |

Step 1A runtime wiring đã hoàn thành ngày 2026-10-08. Step 1B–1E gồm public catalogue, SystemAdmin catalogue, versioned pricing và business master data đạt `CodeComplete — AwaitingFinalTest` ngày 2026-10-09; Phase 7 tiếp tục từ Step 2 Survey Request.

## 6. Cấu trúc code mục tiêu

> Cấu trúc dưới đây là target .NET hiện hành. Mở rộng các module theo capability và giữ boundary; không tạo một backend ngôn ngữ khác song song.

Không bắt buộc tạo project mới. Ưu tiên mở rộng module hiện hữu theo cấu trúc:

```text
AgriDrone.Modules.Surveys/
  Domain/
  Application/
    Abstractions/
    Features/
      Catalogue/
      Requests/
      Approvals/
      Orders/
      Appointments/
      Payments/
      Results/
      Queries/
    Errors/
  Infrastructure/
    Persistence/
    Queries/
    Messaging/
    Payments/
  DependencyInjection.cs

AgriDrone.Api/
  Contracts/Surveys/
  Controllers/
    PublicSurveyServicesController.cs
    PublicSurveyRequestsController.cs
    TenantOwnerSurveyRequestsController.cs
    TenantOwnerSurveyOrdersController.cs
    SystemSurveyServicesController.cs
    SystemSurveyRequestsController.cs
    SystemSurveyPaymentsController.cs
    SystemManagerSurveyOrdersController.cs
    SystemManagerSurveyResultsController.cs

AgriDrone.IntegrationContracts/
  Surveys/
  Mapping/V2/ và Mapping/V3/
  Health/V2/ và Health/V3/
  HarvestReadiness/V2/

AgriDrone.Database/
  Cross-module DbContext/UoW cho approval, map publication và result publication
  Migrations mới theo từng phase
```

Tên file/controller có thể điều chỉnh theo convention hiện tại, nhưng boundary và actor surface không được nhập nhằng.

## 7. Thứ tự triển khai bắt buộc

Trước Step 1B bên dưới, hoàn thành Step 0R để điều chỉnh baseline cũ theo `Be-Plan.md` ngày 2026-10-06. Sau đó tiếp tục tuần tự trên solution .NET hiện tại.

```text
Step 0  Khóa contract, state machine, transaction và security conventions
   ↓
Step 0R Rebaseline per-pole/boundary/Disease Zone/recommendation/plant-change
   ↓
Step 1  Runtime Surveys module + UC03 Catalogue/Master Data
   ↓
Step 2  UC04 Survey Request intake/review (Phase 7)
   ↓
Step 3  UC05 Atomic approval/onboarding/SurveyOrder (Phase 8)
   ↓
Step 4  UC06 Scope/Pricing/Appointment/Payment/Readiness (Phase 9)
   ↓
Step 5  UC07 Farm/Zone operational verification + BE2 readiness boundary
   ↓
Step 6  UC08 Farm base map review/publication V3 (Phase 11)
   ↓
Step 7  UC09 Plant Health official result (Phase 12)
   ↓
Step 8  UC10 Harvest Readiness official result (Phase 12)
   ↓
Step 9  UC11 Digital Plant Profile/History/Follow-up (Phase 13)
   ↓
Step 10 UC12 Work queues/Notifications/Audit completion
   ↓
Step 11 Residual cleanup + E2E/release gate (Phase 14–15)
```

BE2 có thể phát triển order-bound Mission song song sau khi Step 4 chốt contract. BE1 không bắt đầu Step 6 trước khi mission/order/base-map context V3 đã ổn định về schema và semantics; contract test được thực hiện ở Final Test Phase.

---

## 8. Step 0 — Khóa quyết định kiến trúc và contract

### 8.1. State machine chuẩn

- [x] Viết transition table và domain methods cho `SurveyRequest`:
  - `Submitted → UnderReview`.
  - `UnderReview → Approved | Rejected`.
  - `Submitted | UnderReview → Withdrawn` theo policy.
- [x] Rebaseline transition table cho `SurveyOrder` theo hai stage:
  - Unmapped Farm: `PendingBoundaryVerification → AwaitingBaselineAppointment → BaselineReady → BaselineInProgress → AwaitingBaselineReview → AwaitingPricing`.
  - Sau confirmed pole count: `AwaitingPaidAppointment → AwaitingPayment → ReadyForPaidService → InProgress → PendingReview → Completed`.
  - Mapped Farm có thể đi thẳng từ boundary/scope verification tới confirmed pole-count/pricing; không tạo Baseline Mapping giả.
  - `Cancelled` chỉ từ các trạng thái được phép và không xóa lịch sử.
- [x] Viết transition table cho Appointment, Payment, PriceAdjustment và SurveyResult.
- [x] Chốt source-of-truth của mỗi transition; không để controller/client tự set enum.
- [x] Chốt timestamp dùng UTC `DateTimeOffset`/`TimeProvider` và explicit `Version` concurrency token.

### 8.2. Security/error convention

- [x] `401`: chưa xác thực/token không hợp lệ.
- [x] `404`: resource không tồn tại hoặc nằm ngoài tenant/assignment visibility.
- [x] `403`: resource nằm trong visible scope nhưng actor không có action permission.
- [x] `409`: stale version, duplicate/conflicting transition hoặc concurrent approval/publication.
- [x] `422` hoặc business validation response đã thống nhất: request/scope/payment payload đúng cú pháp nhưng vi phạm rule.
- [x] Tạo stable error codes theo nhóm `SurveyRequest.*`, `SurveyOrder.*`, `Appointment.*`, `Payment.*`, `SurveyResult.*`.
- [x] Viết shared authorization tests cho SystemAdmin, assigned/unassigned SystemManager, correct/wrong TenantOwner và public actor.

### 8.3. Transaction boundaries

Chốt ba boundary trước khi viết handler:

1. `ISurveyApprovalUnitOfWork`/specialized DbContext:
   - SurveyRequest + review.
   - Tenant/Farm/Owner invitation theo request kind.
   - FarmManagerAssignment.
   - SurveyOrder.
   - Audit + Outbox.
2. `IMappingPublicationUnitOfWork` mở rộng cho V2:
   - Inbox.
   - FarmBaseMapVersion + ZoneMapVersions.
   - Plants + PlantChangeEvents.
   - Audit + Outbox.
3. `ISurveyResultPublicationUnitOfWork`:
   - Inbox/imported pending findings/assessments.
   - SurveyResult + review history.
   - Derived current Plant health khi publish Plant Health.
   - SurveyOrder status.
   - Audit + Outbox.

- [x] Không dùng distributed transaction hoặc chuỗi SaveChanges độc lập.
- [x] Specialized DbContext chỉ map bảng cần thiết; `AgriDroneSchemaDbContext` vẫn chỉ phục vụ migration/design-time.
- [x] Viết rollback integration test skeleton trước handler orchestration.

### 8.4. V2 contracts với BE2

- [x] Chốt `SurveyOrderOperationalContext`/readiness query contract.
- [x] Chốt current Farm base-map và Plant reference snapshot contract.
- [x] Chốt `BaselineMappingCandidatesApprovedV2`.
- [x] Chốt `FarmBaseMapPublishedV2`.
- [x] Chốt `HealthObservationsReadyV2`.
- [x] Chốt Harvest Readiness pending handoff V2.
- [x] Chốt official result/review state V2 để BE2 complete Mission.
- [x] Mỗi event có MessageId, schema version, occurred-at, correlation/causation, SurveyOrderId, FarmId, source Mission/Job và context đặc thù.
- [x] Golden JSON và validation tests cho V1/V2 song song.

`BaselineMappingCandidatesApprovedV2`, `FarmBaseMapPublishedV2` và `HealthObservationsReadyV2` là baseline lịch sử nhưng không đủ semantics mới. Step 0R phải phát hành Mapping V3 Ready/Published và `PlantHealthAnalysisReadyV3`; không sửa V2 tại chỗ.

### Deliverable Step 0

- ADR bổ sung nếu một trong ba transaction boundary hoặc contract chưa được ADR hiện tại quyết định đủ rõ.
- State-transition/error-code tables được review.
- Interfaces/contract DTO compile nhưng chưa mở public feature API.
- Architecture tests khóa dependency direction.

### 8.4A. Step 0R — Rebaseline delta bắt buộc trước Step 1B

`Be-Plan.md` ngày 2026-10-06 thay đổi các assumption nền. Các mục dưới đây là việc mới, không được thừa hưởng dấu `[x]` của Step 0 cũ:

Decision baseline đã được Product chốt ngày 2026-10-07 tại `docs/adr/0007-per-pole-pricing-and-purpose-aware-readiness.md` đến `docs/adr/0011-v3-survey-mapping-health-contracts.md`; field-level baseline nằm tại `docs/domain/be1-step0r-locked-contract.md`. Các ADR này khóa thiết kế nhưng không đồng nghĩa implementation/migration/test đã hoàn thành.

- [x] Thêm domain/value objects `PricePerPole`, `ConfirmedSurveyPoleCount` và price formula; approximate area chỉ còn dùng cho request feasibility/scope.
- [x] Viết expand/backfill/contract migration mới cho per-pole pricing; giữ adapter/read path legacy rõ ràng và không sửa migration Phase 5.
- [x] Thay một readiness chung bằng decision theo `MissionPurpose`: Baseline Mapping không yêu cầu payment; Plant Health/Harvest Readiness bắt buộc count/price snapshot + payment.
- [x] Thêm approved FarmBoundary version/verification/audit và boundary-exception state `OutOfBoundary/NeedsReview`.
- [x] Thêm aggregate/schema/ports cho proposed/approved Disease Zone và expert-validated treatment recommendation catalogue.
- [x] Thêm `PlantInventoryChangeReport` state machine cho owner report → manager verification → later-survey evidence (đối với cây mới) → applied/rejected.
- [x] Mở rộng mapping/result publication boundaries để commit confirmed active-pole count, Disease Zone/recommendation provenance và plant-change decision đúng transaction.
- [x] Triển khai V3 payload cho boundary exception, proposed Disease Zone, recommendation selection, confirmed pole count và plant-change evidence; giữ V2 bất biến. Golden JSON được viết ở Final Test Phase.
- [x] Cập nhật stable error codes, OpenAPI intent, ADR và glossary; không để assumption cũ “payment trước mọi flight” hoặc “price per hectare” tồn tại trong target path.

Các test Step 0R chưa viết được chuyển xuống mục 27. Step 0R đạt `CodeComplete — AwaitingFinalTest` khi code/migration/contract hoàn tất; chỉ đạt `Done` sau Final Test Phase.

### 8.5. Cách sử dụng hướng dẫn từ Step 1 đến Step 11

> Các hướng dẫn về behavior và transaction tiếp tục có hiệu lực. Convention tên file/lớp C# bên dưới là target .NET hiện hành; điều chỉnh tên theo convention repo nhưng không đổi boundary/responsibility. Toàn bộ test chưa viết được quản lý tại mục 27.

Mỗi Step bên dưới đã được chia thành các phase nhỏ. Làm hết exit gate của phase trước rồi mới sang phase sau; không tạo đồng thời controller, database và handler khi domain flow chưa rõ.

Thứ tự chuẩn trong **mỗi phase**:

1. Viết lại business flow và các trường hợp từ chối bằng ngôn ngữ nghiệp vụ.
2. Bổ sung Domain factory/method/policy/invariant nếu aggregate cần hành vi mới.
3. Tạo Application use case: command/query, validator, handler, response và application error.
4. Tạo Application port cần thiết; sau đó mới viết Infrastructure repository/query/provider/consumer implementation.
5. Thêm API contract/controller action hoặc integration consumer sau khi handler và boundary nghiệp vụ đã hoàn chỉnh.
6. Thêm audit/outbox/inbox/idempotency/concurrency trong cùng boundary của mutation.
7. Ghi các scenario kiểm thử phát sinh vào Final Test Backlog; chưa viết hoặc chạy test ở phase code.
8. Cập nhật OpenAPI/migration report và đánh dấu `CodeComplete — AwaitingFinalTest` khi phần code hoàn chỉnh.

Quy ước cấu trúc file bắt buộc:

- Một use case một folder, ví dụ `Application/Features/Requests/SubmitNewCustomerSurveyRequest/`.
- Mutation tách `Command`, `CommandValidator`, `CommandHandler` và `Response` thành file riêng.
- Query tách `Query`, `QueryHandler` và read-model `Response` thành file riêng.
- Domain chia theo business concept như `Catalogue`, `Requests`, `Orders`, `Appointments`, `Payments`, `Results`; không gom vào `Entities`, `Enums` hoặc một file state machine chung.
- Mỗi repository implementation, message consumer và EF entity configuration một file.
- `Common` chỉ chứa primitive/policy thật sự dùng chung; không chuyển nghiệp vụ của nhiều use case vào một helper lớn.

Ma trận vị trí code theo Step:

| Step | Application feature chính | Infrastructure/Integration chính |
|---|---|---|
| 1 | `Features/Catalogue/` | Surveys repositories, catalogue queries, DI |
| 2 | `Features/Requests/` | Request repository, admin/owner queries |
| 3 | `Features/Approvals/` | Approval specialized UoW/cross-module persistence |
| 4 | `Features/Orders/`, `Appointments/`, `Payments/` | Payment adapter/callback, readiness query |
| 5 | Farms operational features + Surveys integration port | PostGIS queries, BE2 operational-context adapter |
| 6 | Mapping publication handler/policy | V2 consumer, Inbox, mapping publication UoW |
| 7 | `Features/Results/PlantHealth/` | Plant Health V3 consumer, result publication UoW |
| 8 | `Features/Results/HarvestReadiness/` | Readiness V2 consumer, result publication UoW |
| 9 | `Features/Queries/` và Plant amendment features | Published-only read queries, cache/index |
| 10 | `Features/WorkQueues/` | Notification consumers, audit/observability |
| 11 | Không thêm business feature mới | Cleanup, migration, E2E và runbook |

Một phase code chỉ được đánh dấu `[x]` cho phần implementation khi có đủ behavior, persistence và authorization; trạng thái tổng thể là `CodeComplete — AwaitingFinalTest`. Chỉ Final Test Phase mới xác nhận `Done`. Interface/skeleton hoặc build pass đơn lẻ chỉ được xem là đang làm.

### 8.6. Cấu trúc bắt buộc bên trong từng loại file

Phần này mô tả **bên trong file phải có gì**. Các Step phía dưới chỉ cần chỉ ra tên file cụ thể và áp dụng đúng cấu trúc này.

| Loại file | Thứ tự nội dung bên trong | Không được chứa |
|---|---|---|
| `...Command.cs` | Record/class input; IDs; expected version; idempotency key nếu cần; implement request contract trả `Result<Response>` | EF query, authorization, domain transition, mapping HTTP |
| `...CommandValidator.cs` | Rule cú pháp: required, length, range, enum, định dạng; message/error field ổn định | Database query, kiểm tra ownership, kiểm tra trạng thái hiện tại |
| `...CommandHandler.cs` | Constructor dependencies; `Handle`; authorize actor; load authoritative data; gọi domain method; audit/outbox; save/transaction; map response | Set private state bằng reflection, query DbContext trực tiếp, nhiều use case không liên quan |
| `...Query.cs` | Filter, paging, cursor, resource IDs; request contract trả read response | Mutation hoặc EF entity |
| `...QueryHandler.cs` | Authorize; normalize filter; gọi query port; trả response | Business mutation, `SaveChanges`, trả `IQueryable` |
| `...Response.cs` | Immutable scalar/nested response DTO; version và timestamp cần thiết | Navigation property, secret, provider payload, EF type |
| `I...Repository.cs` | Get aggregate theo business need, add, existence/lock query tối thiểu | `IQueryable`, API response, cross-module EF entity |
| `...Repository.cs` | EF implementation đúng interface, Include tối thiểu, cancellation token | Business authorization, HTTP mapping, transaction orchestration đa module |
| `I...Queries.cs` | Read-model methods cho màn hình/use case; input rõ scope | Mutation, `SaveChanges`, domain transition |
| `...Queries.cs` | Projection trực tiếp sang read model, tenant/assignment predicate, paging/sort | Trả entity graph hoặc lọc security sau khi load toàn bộ |
| Domain aggregate | Private setters/constructor; factory; named behavior; invariant; transition; domain event nếu áp dụng | MediatR, EF query, controller/API contract, provider SDK |
| Domain policy/value object | Input/output nghiệp vụ nhỏ, deterministic và test độc lập | Repository/DbContext, actor context, network call |
| `...Configuration.cs` | Table/key/property/index/FK/check constraint/concurrency mapping cho đúng một entity | Nghiệp vụ hoặc mapping nhiều entity không liên quan |
| API request/response | HTTP shape, serialization/GeoJSON/provider raw shape cần thiết | Domain behavior hoặc database access |
| Controller | Route; policy; map API request → command/query; gọi `ISender`; map `Result` → HTTP | Nghiệp vụ, repository, DbContext, transaction |
| Message consumer | Deserialize/envelope validation; gọi application handler; ACK/NACK theo outcome | Publication business logic hoặc EF mutation trực tiếp |

Quy ước type/visibility:

- Command, query và response dùng từ API hoặc module khác: `public sealed`.
- Handler, validator, repository implementation, query implementation và provider adapter: `internal sealed` mặc định.
- Domain entity/aggregate/value object giữ visibility cần cho EF và application; setter không được public.
- Port/interface cross-project phải public; interface chỉ dùng nội assembly giữ internal nếu DI scanning/registration cho phép.
- Namespace theo convention hiện tại của module. Folder dùng để nhóm business concept/use case; không đổi namespace hàng loạt chỉ để phản chiếu folder nếu việc đó làm vỡ migration/model snapshot.
- Mỗi file production có một primary type cùng tên file; nested private type chỉ dùng khi thật sự là chi tiết nội bộ của primary type.

Thứ tự chuẩn trong một `CommandHandler` phải đọc được theo bảy khối: **actor → authoritative load → authorization → domain decision → persistence → audit/outbox → response**. Nếu handler dài vì có nhiều nhánh business khác nhau, tách policy/orchestrator theo business concept; không chuyển tất cả sang `CommonHelper`.

Ví dụ cách đọc blueprint cho feature `CreateSurveyServicePrice` — đây là mô tả cấu trúc, không phải code:

| File | Thành phần phải có bên trong |
|---|---|
| `CreateSurveyServicePriceCommand.cs` | `ServiceId`, `PricePerPole`, `Currency`, `EffectiveFrom`, `EffectiveTo`, `ExpectedVersion`; request trả `CreateSurveyServicePriceResponse` |
| `CreateSurveyServicePriceCommandValidator.cs` | ServiceId khác rỗng; price dương; currency đúng 3 ký tự; `EffectiveTo > EffectiveFrom`; chỉ kiểm tra shape |
| `CreateSurveyServicePriceCommandHandler.cs` | Dependencies: execution context, service repository, catalogue/overlap query, UoW, time provider, audit, outbox; `Handle` theo bảy khối chuẩn; gọi domain method/factory thay vì set property |
| `CreateSurveyServicePriceResponse.cs` | PriceVersionId, ServiceId, amount/currency, effective window, created time và service version mới; không trả entity/navigation |
| `ISurveyServiceRepository.cs` | Method load service cho mutation và add/attach price version theo aggregate design; không có paging/public query |
| `SurveyCatalogueQueries.cs` | Method kiểm tra effective-window overlap và đọc price history/current price bằng projection |
| `CreateSurveyServicePriceRequest.cs` | HTTP body tương ứng; không chứa ActorId/SystemAdminId vì lấy từ token/context |
| `SystemSurveyServicesController.cs` | Route + policy; map request/route sang command; `ISender.Send`; map `Result`; không kiểm tra overlap trong controller |

Với các feature khác, thay field/dependency theo nghiệp vụ nhưng giữ cùng nguyên tắc phân trách nhiệm. Nếu chưa thể ghi rõ input, output, dependencies và bảy bước handler cho một feature thì feature đó chưa đủ rõ để bắt đầu code.

### 8.7. File manifest chính xác theo từng Step

> Toàn bộ manifest trong mục 8.7 là target .NET hiện hành. Đối chiếu source trước khi tạo để sửa file đã có thay vì tạo bản sao; cập nhật manifest trong PR/phase report khi tên thực tế thay đổi.

Các prefix dưới đây được dùng để bảng không quá dài nhưng vẫn xác định được đường dẫn tuyệt đối trong repository:

- `SURVEYS` = `backend/src/Modules/AgriDrone.Modules.Surveys`
- `FARMS` = `backend/src/Modules/AgriDrone.Modules.Farms`
- `PLANTS` = `backend/src/Modules/AgriDrone.Modules.Plants`
- `API` = `backend/src/AgriDrone.Api`
- `DB` = `backend/src/BuildingBlocks/AgriDrone.Database`
- `CONTRACTS` = `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts`

`[Tạo]` nghĩa là file mục tiêu chưa có trong baseline. `[Sửa]` nghĩa là mở rộng file hiện hữu; không tạo bản sao tên khác.

#### File manifest Step 0R

Các tên dưới đây là responsibility target; nếu repo đã có type tương đương thì sửa type đó và ghi mapping trong phase report, không tạo duplicate:

| Thao tác | File/capability | Trách nhiệm |
|---|---|---|
| `[Sửa]` | `SURVEYS/Domain/Catalogue/SurveyServicePrice.cs` | PricePerPole effective/versioned; legacy PricePerHa chỉ còn adapter/read path được đánh dấu |
| `[Sửa]` | `SURVEYS/Domain/Orders/SurveyOrder.cs` | Confirmed pole-count/price snapshot và two-stage order transitions |
| `[Sửa]` | `SURVEYS/Domain/Orders/SurveyOrderReadinessPolicy.cs` | Decision theo MissionPurpose; Baseline không payment, paid service bắt buộc payment |
| `[Tạo/Sửa]` | `FARMS/Domain/Boundaries/FarmBoundary.cs` | Version, approval, reviewer/evidence và active boundary invariant |
| `[Tạo]` | `FARMS/Domain/Boundaries/BoundaryException.cs` | OutOfBoundary/NeedsReview/corrected decision và provenance |
| `[Tạo]` | `SURVEYS/Domain/Results/DiseaseZone.cs` | Proposed/reviewed/published zone geometry + membership version |
| `[Tạo]` | `PLANTS/Domain/Conditions/TreatmentRecommendation.cs` | Expert catalogue theo disease/severity, version/lifecycle/advisory metadata |
| `[Tạo]` | `PLANTS/Domain/Changes/PlantInventoryChangeReport.cs` | Owner report, manager review, later-survey evidence và applied/rejected state |
| `[Sửa/Tạo]` | EF configurations + `<timestamp>_RebaselinePerPoleBoundaryResults.cs` | Expand migration; backfill/contract tách riêng theo dữ liệu thực |
| `[Tạo/Sửa]` | `CONTRACTS/Mapping/BaselineMappingCandidatesReadyV3.cs`, `FarmBaseMapPublishedV3.cs`, `CONTRACTS/Health/PlantHealthAnalysisReadyV3.cs` | Pending mapping/health/zone/recommendation/boundary data và published confirmed count; giữ V2 immutable; golden tests nằm ở mục 27 |

#### File manifest Step 1

Foundation và persistence:

| Thao tác | File | Cấu trúc/trách nhiệm |
|---|---|---|
| `[Tạo]` | `SURVEYS/DependencyInjection.cs` | Một extension method đăng ký DbContext, UoW, repositories, queries, handlers, validators; không chứa nghiệp vụ |
| `[Sửa]` | `SURVEYS/AgriDrone.Modules.Surveys.csproj` | Chỉ thêm project/package reference thật sự cần cho MediatR, validation, infrastructure/audit |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Persistence/ISurveysUnitOfWork.cs` | `SaveChangesAsync` hoặc transaction seam nội module; không thay approval/map/result specialized UoW |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/ISurveyCatalogueQueries.cs` | Public catalogue, admin catalogue và price-history read methods |
| `[Tạo]` | `SURVEYS/Domain/Catalogue/ISurveyServiceRepository.cs` | Get service để mutation, add nếu policy cho create; không trả query response |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyServiceRepository.cs` | EF implementation của repository trên |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/SurveyCatalogueQueries.cs` | Projection public/admin/price history theo server time |
| `[Sửa]` | `SURVEYS/Infrastructure/Persistence/SurveysDbContext.cs` | Bảo đảm DbSet/configuration và UoW behavior được runtime DI sử dụng |
| `[Sửa]` | `API/Program.cs` | Gọi registration của Surveys đúng một lần |

Domain/error files cần sửa hoặc tạo:

| Thao tác | File | Nội dung cần bổ sung |
|---|---|---|
| `[Sửa]` | `SURVEYS/Domain/Catalogue/SurveyService.cs` | Factory nếu cho phép create; activate; mark experimental; retire; update metadata; lifecycle invariants |
| `[Sửa]` | `SURVEYS/Domain/Catalogue/SurveyServicePrice.cs` | Factory cho price version; amount/currency/effective-window invariants; không có method sửa amount lịch sử |
| `[Tạo]` | `SURVEYS/Domain/Catalogue/HarvestReadinessCriterion.cs` | Criteria code/version, lifecycle, granularity, effective window và metadata công bố |
| `[Tạo]` | `SURVEYS/Domain/Catalogue/HarvestReadinessCriterionStatus.cs` | Experimental, Validated, Retired |
| `[Tạo]` | `SURVEYS/Domain/Catalogue/HarvestReadinessGranularity.cs` | Chỉ các granularity đã được domain chốt |
| `[Tạo]` | `SURVEYS/Domain/Catalogue/IHarvestReadinessCriterionRepository.cs` | Lookup/version mutation cho criteria catalogue |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Configurations/HarvestReadinessCriterionConfiguration.cs` | Mapping/index/unique/effective-window constraints của criteria |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/HarvestReadinessCriterionRepository.cs` | EF implementation của criteria repository |
| `[Giữ/Sửa nếu thiếu version rule]` | `PLANTS/Domain/Conditions/PlantCondition.cs` | Disease/non-disease catalogue tiếp tục thuộc Plants; không tạo bản sao trong Surveys |
| `[Tạo/Sửa]` | `PLANTS/Domain/Conditions/TreatmentRecommendation.cs` | Expert-validated advisory catalogue; disease/severity/version mapping, không free-form AI advice |
| `[Tạo]` | `PLANTS/Infrastructure/Persistence/Configurations/TreatmentRecommendationConfiguration.cs` | Unique/version/effective-window/lifecycle constraints |
| `[Giữ/Sửa nếu thiếu query]` | `PLANTS/Application/Features/GetActivePlantConditions/` | Public/internal condition catalogue read path hiện hữu |
| `[Tạo]` | `SURVEYS/Application/Errors/SurveyServiceError.cs` | NotFound, NotPubliclyAvailable, InvalidLifecycle, ActivePriceRequired, PriceWindowOverlap |

Feature folders cần tạo; cột “Files” là danh sách file chính xác bên trong folder:

| Folder | Files | Trách nhiệm |
|---|---|---|
| `SURVEYS/Application/Features/Catalogue/GetPublicSurveyServices/` | `GetPublicSurveyServicesQuery.cs`, `GetPublicSurveyServicesQueryHandler.cs`, `PublicSurveyServiceResponse.cs` | Public catalogue tại server time |
| `SURVEYS/Application/Features/Catalogue/GetSurveyServiceCatalogue/` | `GetSurveyServiceCatalogueQuery.cs`, `GetSurveyServiceCatalogueQueryHandler.cs`, `SurveyServiceCatalogueResponse.cs` | Admin list gồm history/status |
| `SURVEYS/Application/Features/Catalogue/ActivateSurveyService/` | `ActivateSurveyServiceCommand.cs`, `ActivateSurveyServiceCommandValidator.cs`, `ActivateSurveyServiceCommandHandler.cs`, `ActivateSurveyServiceResponse.cs` | Activate có version/audit |
| `SURVEYS/Application/Features/Catalogue/MarkSurveyServiceExperimental/` | `MarkSurveyServiceExperimentalCommand.cs`, `MarkSurveyServiceExperimentalCommandValidator.cs`, `MarkSurveyServiceExperimentalCommandHandler.cs`, `MarkSurveyServiceExperimentalResponse.cs` | Hạ capability về experimental có reason |
| `SURVEYS/Application/Features/Catalogue/RetireSurveyService/` | `RetireSurveyServiceCommand.cs`, `RetireSurveyServiceCommandValidator.cs`, `RetireSurveyServiceCommandHandler.cs`, `RetireSurveyServiceResponse.cs` | Chặn request mới, giữ history |
| `SURVEYS/Application/Features/Catalogue/UpdateSurveyServiceMetadata/` | `UpdateSurveyServiceMetadataCommand.cs`, `UpdateSurveyServiceMetadataCommandValidator.cs`, `UpdateSurveyServiceMetadataCommandHandler.cs`, `UpdateSurveyServiceMetadataResponse.cs` | Đổi metadata không làm đổi snapshot cũ |
| `SURVEYS/Application/Features/Catalogue/CreateSurveyServicePrice/` | `CreateSurveyServicePriceCommand.cs`, `CreateSurveyServicePriceCommandValidator.cs`, `CreateSurveyServicePriceCommandHandler.cs`, `CreateSurveyServicePriceResponse.cs` | Tạo price version, kiểm tra overlap |
| `SURVEYS/Application/Features/Catalogue/GetSurveyServicePriceHistory/` | `GetSurveyServicePriceHistoryQuery.cs`, `GetSurveyServicePriceHistoryQueryHandler.cs`, `SurveyServicePriceHistoryResponse.cs` | Đọc lịch sử giá cho admin |

API files:

| Thao tác | File | Nội dung |
|---|---|---|
| `[Tạo]` | `API/Contracts/Surveys/Catalogue/CreateSurveyServicePriceRequest.cs` | Amount, currency, effective-from/to, expected service version nếu policy dùng |
| `[Tạo]` | `API/Contracts/Surveys/Catalogue/UpdateSurveyServiceMetadataRequest.cs` | Name/description và expected version |
| `[Tạo]` | `API/Contracts/Surveys/Catalogue/ChangeSurveyServiceStatusRequest.cs` | Expected version và reason; không nhận status tùy ý |
| `[Tạo]` | `API/Controllers/PublicSurveyServicesController.cs` | Chỉ GET public catalogue |
| `[Tạo]` | `API/Controllers/SystemSurveyServicesController.cs` | Admin catalogue, lifecycle và price routes |

Tên và vị trí test tương ứng được quy định tại Final Test Backlog ở mục 27; không tạo test trong lúc triển khai feature.

#### File manifest Step 2

Core files:

| Thao tác | File | Cấu trúc/trách nhiệm |
|---|---|---|
| `[Tạo]` | `SURVEYS/Domain/Requests/ISurveyRequestRepository.cs` | Get by ID, get by caller-scope/idempotency, add và mutation load |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/ISurveyRequestQueries.cs` | Admin inbox/detail và owner history projections |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyRequestRepository.cs` | EF repository implementation |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/SurveyRequestQueries.cs` | Scoped/paged request read models |
| `[Sửa]` | `SURVEYS/Domain/Requests/SurveyRequest.cs` | Ba factories, StartReview/Reject/Withdraw và kind-specific invariants |
| `[Sửa]` | `SURVEYS/Application/Errors/SurveyRequestError.cs` | IdempotencyPayloadMismatch, NotReviewable, Ownership/visibility và validation errors còn thiếu |

Feature folders:

| Folder | Files |
|---|---|
| `SURVEYS/Application/Features/Requests/SubmitNewCustomerSurveyRequest/` | `SubmitNewCustomerSurveyRequestCommand.cs`, `SubmitNewCustomerSurveyRequestCommandValidator.cs`, `SubmitNewCustomerSurveyRequestCommandHandler.cs`, `SubmitNewCustomerSurveyRequestResponse.cs` |
| `SURVEYS/Application/Features/Requests/SubmitNewFarmSurveyRequest/` | `SubmitNewFarmSurveyRequestCommand.cs`, `SubmitNewFarmSurveyRequestCommandValidator.cs`, `SubmitNewFarmSurveyRequestCommandHandler.cs`, `SubmitNewFarmSurveyRequestResponse.cs` |
| `SURVEYS/Application/Features/Requests/SubmitExistingFarmSurveyRequest/` | `SubmitExistingFarmSurveyRequestCommand.cs`, `SubmitExistingFarmSurveyRequestCommandValidator.cs`, `SubmitExistingFarmSurveyRequestCommandHandler.cs`, `SubmitExistingFarmSurveyRequestResponse.cs` |
| `SURVEYS/Application/Features/Requests/GetSurveyRequestInbox/` | `GetSurveyRequestInboxQuery.cs`, `GetSurveyRequestInboxQueryValidator.cs`, `GetSurveyRequestInboxQueryHandler.cs`, `SurveyRequestInboxItemResponse.cs` |
| `SURVEYS/Application/Features/Requests/GetSurveyRequestDetail/` | `GetSurveyRequestDetailQuery.cs`, `GetSurveyRequestDetailQueryValidator.cs`, `GetSurveyRequestDetailQueryHandler.cs`, `SurveyRequestDetailResponse.cs` |
| `SURVEYS/Application/Features/Requests/StartSurveyRequestReview/` | `StartSurveyRequestReviewCommand.cs`, `StartSurveyRequestReviewCommandValidator.cs`, `StartSurveyRequestReviewCommandHandler.cs`, `StartSurveyRequestReviewResponse.cs` |
| `SURVEYS/Application/Features/Requests/RejectSurveyRequest/` | `RejectSurveyRequestCommand.cs`, `RejectSurveyRequestCommandValidator.cs`, `RejectSurveyRequestCommandHandler.cs`, `RejectSurveyRequestResponse.cs` |

API files:

- `[Tạo] API/Contracts/Surveys/Requests/SubmitNewCustomerSurveyRequest.cs`
- `[Tạo] API/Contracts/Surveys/Requests/SubmitNewFarmSurveyRequest.cs`
- `[Tạo] API/Contracts/Surveys/Requests/SubmitExistingFarmSurveyRequest.cs`
- `[Tạo] API/Contracts/Surveys/Requests/RejectSurveyRequest.cs`
- `[Tạo] API/Contracts/Surveys/Requests/GetSurveyRequestsRequest.cs`
- `[Tạo] API/Controllers/PublicSurveyRequestsController.cs`
- `[Tạo] API/Controllers/TenantOwnerSurveyRequestsController.cs`
- `[Tạo] API/Controllers/SystemSurveyRequestsController.cs`

API request files chỉ mô tả HTTP input; application command tự nhận actor/tenant từ execution context trong handler.

#### File manifest Step 3

| Thao tác | File | Cấu trúc/trách nhiệm |
|---|---|---|
| `[Giữ/Sửa khi cần]` | `SURVEYS/Application/Abstractions/Persistence/ISurveyApprovalUnitOfWork.cs` | Một transaction seam cho approval; không thêm method SaveChanges rời rạc để handler bypass transaction |
| `[Tạo]` | `SURVEYS/Domain/Orders/ISurveyOrderRepository.cs` | Get/add order và previous-compatible lookup nếu lookup thuộc aggregate persistence |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/ISurveyApprovalReferenceQueries.cs` | Service/manager/owner/Farm/base-map reference checks không thuộc aggregate repository |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ApproveSurveyRequestCommand.cs` | RequestId, manager ID, checklist/reason, expected version, idempotency key |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ApproveSurveyRequestCommandValidator.cs` | Shape validation, không query database |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ApproveSurveyRequestCommandHandler.cs` | Revalidate + dispatch đúng orchestration kind trong specialized transaction |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ApproveSurveyRequestResponse.cs` | Request/Order/Farm/Tenant IDs phù hợp kind; không trả token |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/NewCustomerApprovalOrchestrator.cs` | Chỉ orchestration NewCustomer |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ExistingTenantNewFarmApprovalOrchestrator.cs` | Chỉ orchestration existing tenant/new Farm |
| `[Tạo]` | `SURVEYS/Application/Features/Approvals/ApproveSurveyRequest/ExistingFarmSurveyApprovalOrchestrator.cs` | Chỉ orchestration existing Farm |
| `[Tạo]` | `DB/Surveys/SurveyApprovalDbContext.cs` | Specialized cross-module mappings cần cho atomic transaction |
| `[Tạo]` | `DB/Surveys/SurveyApprovalUnitOfWork.cs` | Implementation của approval UoW, một commit/rollback boundary |
| `[Tạo]` | `DB/Surveys/SurveyApprovalReferenceQueries.cs` | Cross-module authoritative lookups trong boundary đã chốt |
| `[Sửa]` | `DB/DependencyInjection.cs` | Đăng ký specialized context/UoW/query |
| `[Tạo]` | `API/Contracts/Surveys/Approvals/ApproveSurveyRequest.cs` | Manager, checklist, reason, expected version, idempotency key |
| `[Sửa]` | `API/Controllers/SystemSurveyRequestsController.cs` | Thêm duy nhất approve action gọi MediatR |

Ba orchestrator không tự commit và không inject DbContext; chúng nhận transaction context/ports từ handler/UoW và trả kết quả nghiệp vụ cho cùng một transaction.

#### File manifest Step 4

Domain/persistence files:

| Thao tác | File |
|---|---|
| `[Tạo]` | `SURVEYS/Domain/Orders/ISurveyOrderRepository.cs` nếu Step 3 chưa tạo |
| `[Tạo]` | `SURVEYS/Domain/Appointments/ISurveyAppointmentRepository.cs` |
| `[Tạo]` | `SURVEYS/Domain/Payments/ISurveyPaymentRepository.cs` |
| `[Tạo]` | `SURVEYS/Domain/Payments/IPriceAdjustmentRepository.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyOrderRepository.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyAppointmentRepository.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyPaymentRepository.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/PriceAdjustmentRepository.cs` |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/ISurveyOrderQueries.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/SurveyOrderQueries.cs` |

Feature folders và exact files:

| Folder | Files |
|---|---|
| `.../Features/Orders/VerifySurveyOrderBoundaryAndScope/` | `VerifySurveyOrderBoundaryAndScopeCommand.cs`, validator, handler, response |
| `.../Features/Orders/ConfirmSurveyOrderPoleCount/` | `ConfirmSurveyOrderPoleCountCommand.cs`, validator, handler, response; authoritative inventory source required |
| `.../Features/Appointments/ProposeSurveyAppointment/` | `ProposeSurveyAppointmentCommand.cs`, `ProposeSurveyAppointmentCommandValidator.cs`, `ProposeSurveyAppointmentCommandHandler.cs`, `ProposeSurveyAppointmentResponse.cs` |
| `.../Features/Appointments/ConfirmSurveyAppointment/` | `ConfirmSurveyAppointmentCommand.cs`, `ConfirmSurveyAppointmentCommandValidator.cs`, `ConfirmSurveyAppointmentCommandHandler.cs`, `ConfirmSurveyAppointmentResponse.cs` |
| `.../Features/Appointments/RequestSurveyAppointmentReschedule/` | `RequestSurveyAppointmentRescheduleCommand.cs`, `RequestSurveyAppointmentRescheduleCommandValidator.cs`, `RequestSurveyAppointmentRescheduleCommandHandler.cs`, `RequestSurveyAppointmentRescheduleResponse.cs` |
| `.../Features/Payments/InitiateSurveyPayment/` | `InitiateSurveyPaymentCommand.cs`, `InitiateSurveyPaymentCommandValidator.cs`, `InitiateSurveyPaymentCommandHandler.cs`, `InitiateSurveyPaymentResponse.cs` |
| `.../Features/Payments/ProcessSurveyPaymentEvent/` | `ProcessSurveyPaymentEventCommand.cs`, `ProcessSurveyPaymentEventCommandValidator.cs`, `ProcessSurveyPaymentEventCommandHandler.cs`, `ProcessSurveyPaymentEventResponse.cs` |
| `.../Features/Payments/ReconcileSurveyPayment/` | `ReconcileSurveyPaymentCommand.cs`, `ReconcileSurveyPaymentCommandValidator.cs`, `ReconcileSurveyPaymentCommandHandler.cs`, `ReconcileSurveyPaymentResponse.cs` |
| `.../Features/Payments/RequestPriceAdjustment/` | `RequestPriceAdjustmentCommand.cs`, `RequestPriceAdjustmentCommandValidator.cs`, `RequestPriceAdjustmentCommandHandler.cs`, `RequestPriceAdjustmentResponse.cs` |
| `.../Features/Payments/ApprovePriceAdjustment/` | `ApprovePriceAdjustmentCommand.cs`, `ApprovePriceAdjustmentCommandValidator.cs`, `ApprovePriceAdjustmentCommandHandler.cs`, `ApprovePriceAdjustmentResponse.cs` |
| `.../Features/Payments/RejectPriceAdjustment/` | `RejectPriceAdjustmentCommand.cs`, `RejectPriceAdjustmentCommandValidator.cs`, `RejectPriceAdjustmentCommandHandler.cs`, `RejectPriceAdjustmentResponse.cs` |
| `.../Features/Orders/GetSurveyOrderReadiness/` | `GetSurveyOrderReadinessQuery.cs`, `GetSurveyOrderReadinessQueryHandler.cs`, `SurveyOrderReadinessResponse.cs` |

Với các dòng bắt đầu `.../Features`, prefix đầy đủ là `SURVEYS/Application/Features`.

Payment adapter/API files:

- `[Tạo] SURVEYS/Application/Abstractions/Payments/ISurveyPaymentGateway.cs`
- `[Tạo] SURVEYS/Application/Abstractions/Payments/PaymentSessionResult.cs`
- `[Tạo] SURVEYS/Infrastructure/Payments/SurveyPaymentGateway.cs`
- `[Tạo] SURVEYS/Infrastructure/Payments/SurveyPaymentProviderOptions.cs`
- `[Tạo] SURVEYS/Infrastructure/Payments/SurveyPaymentCallbackVerifier.cs`
- `[Tạo] API/Controllers/SystemManagerSurveyOrdersController.cs`
- `[Tạo] API/Controllers/TenantOwnerSurveyOrdersController.cs`
- `[Tạo] API/Controllers/SurveyPaymentCallbacksController.cs`
- `[Tạo] API/Controllers/SystemSurveyPaymentsController.cs`
- `[Tạo] API/Contracts/Surveys/Orders/VerifySurveyOrderBoundaryAndScopeRequest.cs`
- `[Tạo] API/Contracts/Surveys/Orders/ConfirmSurveyOrderPoleCountRequest.cs`
- `[Tạo] API/Contracts/Surveys/Appointments/ProposeSurveyAppointmentRequest.cs`
- `[Tạo] API/Contracts/Surveys/Appointments/ConfirmSurveyAppointmentRequest.cs`
- `[Tạo] API/Contracts/Surveys/Appointments/RequestSurveyAppointmentRescheduleRequest.cs`
- `[Tạo] API/Contracts/Surveys/Payments/InitiateSurveyPaymentRequest.cs`
- `[Tạo] API/Contracts/Surveys/Payments/ReconcileSurveyPaymentRequest.cs`
- `[Tạo] API/Contracts/Surveys/Payments/RequestPriceAdjustmentRequest.cs`
- `[Tạo] API/Contracts/Surveys/Payments/ReviewPriceAdjustmentRequest.cs`

#### File manifest Step 5

Step này chủ yếu mở rộng Farms và integration query; không chuyển Farm/Zone entity sang Surveys.

| Thao tác | File | Nội dung cần viết |
|---|---|---|
| `[Sửa]` | `FARMS/Application/Features/UpdateFarmDetail/UpdateFarmDetailHandler.cs` | Thay/siết authorization bằng current assignment policy; giữ handler chỉ cho update detail |
| `[Tạo/Sửa]` | `FARMS/Application/Features/VerifyFarmBoundary/` | Command/validator/handler/response; version/evidence/reason/audit và PostGIS validation |
| `[Tạo]` | `FARMS/Application/Abstractions/Boundaries/IFarmBoundaryPolicy.cs` | Point-in-polygon + near-boundary classification dùng chung |
| `[Sửa]` | `FARMS/Application/Features/CreateZone/CreateZoneHandler.cs` | Assignment + PostGIS geometry validation + audit |
| `[Sửa]` | `FARMS/Application/Features/UpdateZone/UpdateZoneHandler.cs` | Assignment + expected version + within-Farm/non-overlap checks |
| `[Sửa]` | `FARMS/Application/Features/ArchiveFarm/ArchiveFarmHandler.cs` | Dùng dependency query gồm active SurveyOrder/Mission/map/result |
| `[Sửa]` | `FARMS/Application/Features/ArchiveZone/ArchiveZoneHandler.cs` | Không archive khi còn operational dependency |
| `[Tạo]` | `FARMS/Application/Abstractions/Queries/IFarmOperationalContextQueries.cs` | Read port cho assigned-manager Farm/Zone context |
| `[Tạo]` | `FARMS/Infrastructure/Queries/FarmOperationalContextQueries.cs` | Projection có assignment predicate và geometry/version summary |
| `[Tạo]` | `FARMS/Application/Features/GetAssignedFarmOperationalContext/GetAssignedFarmOperationalContextQuery.cs` | FarmId/order context input |
| `[Tạo]` | `FARMS/Application/Features/GetAssignedFarmOperationalContext/GetAssignedFarmOperationalContextQueryValidator.cs` | ID/filter validation |
| `[Tạo]` | `FARMS/Application/Features/GetAssignedFarmOperationalContext/GetAssignedFarmOperationalContextQueryHandler.cs` | Authorize assignment rồi gọi query port |
| `[Tạo]` | `FARMS/Application/Features/GetAssignedFarmOperationalContext/FarmOperationalContextResponse.cs` | Farm/Zone/map/version data cần cho manager, không trả EF entity |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/SurveyOrderOperationalContextQuery.cs` | Implementation của operational/readiness contract cho BE2 |
| `[Giữ để drain]` | `CONTRACTS/Surveys/SurveyOrderOperationalContextV2.cs` | Giữ bất biến cho consumer cũ |
| `[Tạo]` | `CONTRACTS/Surveys/SurveyOrderOperationalContextV3.cs` | MissionPurpose, approved boundary/scope, confirmed pole count/price state và purpose-aware readiness |
| `[Tạo]` | `API/Controllers/SystemManagerFarmsController.cs` | Assigned-manager operational read/update routes; không tạo Farm |

Nếu cần trạng thái “Zone đã verify”, phải tạo migration/domain field riêng trước khi tạo `VerifyFarmZone` feature. Không dùng một boolean tạm trong response. Khi model đã chốt, folder phải gồm `VerifyFarmZoneCommand.cs`, `VerifyFarmZoneCommandValidator.cs`, `VerifyFarmZoneCommandHandler.cs`, `VerifyFarmZoneResponse.cs`.

#### File manifest Step 6

| Thao tác | File | Nội dung cần viết |
|---|---|---|
| `[Tạo]` | `DB/Mapping/BaselineMappingCandidatesReadyV3Consumer.cs` | Transport consumer; validate envelope và gọi pending-import processor |
| `[Tạo]` | `DB/Mapping/BaselineMappingCandidatesReadyV3Processor.cs` | Inbox claim, authoritative validation và import pending candidates; không publish trước review |
| `[Tạo]` | `DB/Mapping/BaselineMappingBatchValidator.cs` | Validate toàn Farm/Zone/candidate set; không persistence |
| `[Giữ/Sửa]` | `DB/Mapping/IMappingPublicationUnitOfWork.cs` | V2 atomic operation contract đã khóa ở Step 0 |
| `[Sửa]` | `DB/MappingPublicationDbContext.cs` | Map Inbox, Farm map, Zone maps, Plants, change events, audit và outbox cho một transaction |
| `[Sửa]` | `DB/Mapping/MappingPublicationErrorCodes.cs` | V2 permanent/transient/conflict error codes ổn định |
| `[Sửa]` | `FARMS/Domain/Maps/FarmBaseMapVersion.cs` | Draft/publish/supersede behavior và expected-version invariant |
| `[Sửa]` | `FARMS/Domain/Maps/ZoneMapVersion.cs` | Liên kết Farm map header, không tự publish độc lập |
| `[Sửa]` | `PLANTS/Domain/Plants/Plant.cs` | Match/relocate/retire/replace behavior cần thiết; không gắn AI ingestion logic |
| `[Sửa]` | `PLANTS/Domain/Mapping/PlantChangeEvent.cs` | Append-only provenance cho create/relocate/replace/retire |
| `[Tạo]` | `DB/Mapping/FarmBaseMapPublicationOrchestrator.cs` | Điều phối batch decisions trong một UoW; không transport code |
| `[Tạo]` | `DB/Mapping/FarmBaseMapPublishedV3OutboxFactory.cs` | Map committed publication + confirmed pole count sang V3 payload |
| `[Giữ để drain]` | `CONTRACTS/Mapping/BaselineMappingCandidatesApprovedV2.cs` | Immutable V2 compatibility contract; không dùng cho target approval semantics |
| `[Tạo]` | `CONTRACTS/Mapping/BaselineMappingCandidatesReadyV3.cs` | Target input contract cho pending candidates cần SystemManager review |
| `[Giữ để drain]` | `CONTRACTS/Mapping/FarmBaseMapPublishedV2.cs` | Immutable V2 output contract |
| `[Tạo]` | `CONTRACTS/Mapping/FarmBaseMapPublishedV3.cs` | Target published map + confirmed active-pole count contract |

Các file test mapping cần tạo đã chuyển xuống mục 27.3.

Không sửa `MappingCandidatesApprovedHandler.cs` V1 thành V2. Giữ V1 để drain và tạo consumer/processor V2 riêng.

#### File manifest Step 7

Core/result files:

| Thao tác | File |
|---|---|
| `[Tạo]` | `SURVEYS/Domain/Results/ISurveyResultRepository.cs` |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/IPlantHealthResultQueries.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Persistence/Repositories/SurveyResultRepository.cs` |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/PlantHealthResultQueries.cs` |
| `[Sửa]` | `SURVEYS/Domain/Results/SurveyResult.cs` |
| `[Sửa]` | `SURVEYS/Application/Errors/SurveyResultError.cs` |
| `[Giữ/Sửa khi mapping cần]` | `SURVEYS/Application/Abstractions/Persistence/ISurveyResultPublicationUnitOfWork.cs` |
| `[Tạo]` | `DB/Surveys/SurveyResultPublicationDbContext.cs` |
| `[Tạo]` | `DB/Surveys/SurveyResultPublicationUnitOfWork.cs` |

Ingestion và feature folders:

| Folder/file | Files hoặc trách nhiệm |
|---|---|
| `SURVEYS/Infrastructure/Messaging/PlantHealthAnalysisReadyV3Consumer.cs` | Transport validation cho findings/zones/recommendations/boundary exceptions; V2 consumer chỉ drain |
| `.../Features/Results/PlantHealth/ImportPlantHealthObservations/` | `ImportPlantHealthObservationsCommand.cs`, validator, handler, response |
| `.../Features/Results/PlantHealth/GetPlantHealthReviewQueue/` | query, validator, handler, `PlantHealthReviewQueueItemResponse.cs` |
| `.../Features/Results/PlantHealth/ReviewPlantHealthFinding/` | command, validator, handler, response |
| `.../Features/Results/PlantHealth/ReviewPlantHealthFindingBatch/` | command, validator, handler, response |
| `.../Features/Results/PlantHealth/GetDiseaseZoneReviewQueue/` | query, validator, handler, zone geometry/membership response |
| `.../Features/Results/PlantHealth/ReviewDiseaseZone/` | command, validator, handler, response; giữ original/corrected geometry |
| `.../Features/Results/PlantHealth/SelectTreatmentRecommendation/` | command, validator, handler, response; resolve catalogue disease/severity/version |
| `.../Features/Results/PlantHealth/ApprovePlantHealthSurveyResult/` | command, validator, handler, response |
| `.../Features/Results/PlantHealth/PublishPlantHealthSurveyResult/` | command, validator, handler, response |
| `.../Features/Results/PlantHealth/GetPublishedPlantHealthResult/` | query, validator, handler, response |

Trong các folder trên, tên file command/handler phải lấy đầy đủ tên folder, ví dụ `ReviewPlantHealthFindingCommandHandler.cs`; không dùng tên chung `Handler.cs`.

API files:

- `[Tạo] API/Controllers/SystemManagerSurveyResultsController.cs`
- `[Tạo] API/Contracts/Surveys/Results/ReviewPlantHealthFindingRequest.cs`
- `[Tạo] API/Contracts/Surveys/Results/ReviewPlantHealthFindingBatchRequest.cs`
- `[Tạo] API/Contracts/Surveys/Results/ReviewDiseaseZoneRequest.cs`
- `[Tạo] API/Contracts/Surveys/Results/SelectTreatmentRecommendationRequest.cs`
- `[Tạo] API/Contracts/Surveys/Results/PublishSurveyResultRequest.cs`

Nếu pending health finding tiếp tục dùng các entity `ConditionDetection`/`ConditionDetectionReview` hiện có, chỉ bổ sung Order/Farm/base-map/source association và repository/query cần thiết; không tạo entity duplicate trong Surveys. Quyết định reuse này phải được ghi trong migration/ADR trước khi tạo schema mới.

#### File manifest Step 8

Domain/master-data files:

| Thao tác | File | Nội dung |
|---|---|---|
| `[Giữ/Sửa]` | `SURVEYS/Domain/Catalogue/HarvestReadinessCriterion.cs` | Dùng criteria catalogue đã tạo ở Step 1; chỉ bổ sung behavior khi review/publication thực sự cần |
| `[Giữ]` | `SURVEYS/Domain/Catalogue/HarvestReadinessCriterionStatus.cs` | Không tạo enum trạng thái thứ hai trong Results |
| `[Giữ]` | `SURVEYS/Domain/Catalogue/HarvestReadinessGranularity.cs` | Assessment snapshot đúng granularity catalogue |
| `[Giữ]` | `SURVEYS/Domain/Catalogue/IHarvestReadinessCriterionRepository.cs` | Reuse repository contract Step 1 |
| `[Sửa]` | `SURVEYS/Domain/Results/HarvestReadinessAssessment.cs` | Original/corrected assessment, evidence, source và review behavior |
| `[Giữ]` | `SURVEYS/Infrastructure/Persistence/Configurations/HarvestReadinessCriterionConfiguration.cs` | Mapping đã hoàn thành ở Step 1 |
| `[Giữ]` | `SURVEYS/Infrastructure/Persistence/Repositories/HarvestReadinessCriterionRepository.cs` | Reuse implementation Step 1 |
| `[Tạo]` | `SURVEYS/Application/Abstractions/Queries/IHarvestReadinessResultQueries.cs` | Review queue, detail, published output, comparison |
| `[Tạo]` | `SURVEYS/Infrastructure/Queries/HarvestReadinessResultQueries.cs` | Scoped published/pending projections |

Ingestion/features:

| Folder/file | Files hoặc trách nhiệm |
|---|---|
| `SURVEYS/Infrastructure/Messaging/HarvestReadinessAssessmentsReadyV2Consumer.cs` | V2 transport adapter |
| `.../Features/Results/HarvestReadiness/ImportHarvestReadinessAssessments/` | command, validator, handler, response |
| `.../Features/Results/HarvestReadiness/GetHarvestReadinessReviewQueue/` | query, validator, handler, queue-item response |
| `.../Features/Results/HarvestReadiness/ReviewHarvestReadinessAssessment/` | command, validator, handler, response |
| `.../Features/Results/HarvestReadiness/ApproveHarvestReadinessSurveyResult/` | command, validator, handler, response |
| `.../Features/Results/HarvestReadiness/PublishHarvestReadinessSurveyResult/` | command, validator, handler, response |
| `.../Features/Results/HarvestReadiness/GetPublishedHarvestReadinessResult/` | query, validator, handler, response |
| `.../Features/Results/HarvestReadiness/GetHarvestReadinessComparison/` | query, validator, handler, response |

API request files đặt tại `API/Contracts/Surveys/Results/`: `ReviewHarvestReadinessAssessmentRequest.cs` và `PublishHarvestReadinessResultRequest.cs`. Các action dùng chung `SystemManagerSurveyResultsController.cs`; không tạo controller chứa cả admin catalogue/payment logic.

#### File manifest Step 9

Published-only query files:

| Folder | Files |
|---|---|
| `SURVEYS/Application/Features/Queries/GetTenantOwnerFarms/` | `GetTenantOwnerFarmsQuery.cs`, `GetTenantOwnerFarmsQueryHandler.cs`, `TenantOwnerFarmListItemResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetFarmSurveyOrders/` | `GetFarmSurveyOrdersQuery.cs`, validator, handler, `FarmSurveyOrderListItemResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetPublishedFarmMap/` | `GetPublishedFarmMapQuery.cs`, validator, handler, `PublishedFarmMapResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetFarmPlants/` | `GetFarmPlantsQuery.cs`, validator, handler, `FarmPlantListItemResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetDigitalPlantProfile/` | `GetDigitalPlantProfileQuery.cs`, validator, handler, `DigitalPlantProfileResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetDigitalPlantTimeline/` | `GetDigitalPlantTimelineQuery.cs`, validator, handler, `DigitalPlantTimelineItemResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetPublishedSurveyResult/` | query, validator, handler, `PublishedSurveyResultResponse.cs` |
| `SURVEYS/Application/Features/Queries/GetSurveyResultComparison/` | query, validator, handler, `SurveyResultComparisonResponse.cs` |

Query abstraction/implementation:

- `[Tạo] SURVEYS/Application/Abstractions/Queries/ITenantOwnerSurveyQueries.cs`
- `[Tạo] SURVEYS/Infrastructure/Queries/TenantOwnerSurveyQueries.cs`
- `[Tạo] PLANTS/Application/Abstractions/Queries/IDigitalPlantProfileQueries.cs`
- `[Tạo] PLANTS/Infrastructure/Queries/DigitalPlantProfileQueries.cs`

Plant-change feature folders trong `PLANTS/Application/Features/`: `ReportPlantRemoved`, `ReportPlantReplaced`, `ReportNewPlant`, `ReviewPlantInventoryChangeReport`, `ApplyVerifiedPlantChange`, `RelocatePlantFromVerifiedEvidence`, `PublishBaseMapAmendment`. Mỗi folder có command, validator, handler và response mang đúng tên use case. Không expose direct owner `AddPlant/RetirePlant/ReplacePlant`. Domain behavior đặt trong `PlantInventoryChangeReport.cs` và `Plant.cs`; amendment entity/policy đặt trong `FARMS/Domain/Maps/`.

API files:

- `[Tạo] API/Controllers/TenantOwnerSurveyPortalController.cs`
- `[Tạo] API/Contracts/Surveys/Queries/GetFarmPlantsRequest.cs`
- `[Tạo] API/Contracts/Surveys/Queries/GetPlantTimelineRequest.cs`
- `[Tạo] API/Contracts/Surveys/Queries/GetSurveyHistoryRequest.cs`
- `[Tạo] API/Contracts/Surveys/Plants/SubmitPlantInventoryChangeReportRequest.cs`
- `[Tạo] API/Contracts/Surveys/Plants/ReviewPlantInventoryChangeReportRequest.cs`
- `[Tạo] API/Controllers/TenantOwnerPlantInventoryChangesController.cs`
- `[Tạo] API/Controllers/SystemManagerPlantInventoryChangesController.cs`

#### File manifest Step 10

| Folder/file | Files hoặc trách nhiệm |
|---|---|
| `SURVEYS/Application/Features/WorkQueues/GetSystemAdminSurveyWorkQueue/` | query, validator, handler, item response |
| `SURVEYS/Application/Features/WorkQueues/GetSystemManagerSurveyWorkQueue/` | query, validator, handler, item response |
| `SURVEYS/Application/Features/WorkQueues/GetTenantOwnerSurveyWorkQueue/` | query, validator, handler, item response |
| `SURVEYS/Application/Abstractions/Queries/ISurveyWorkQueueQueries.cs` | Ba actor-scoped query methods |
| `SURVEYS/Infrastructure/Queries/SurveyWorkQueueQueries.cs` | SQL/projection/paging; security predicate trong query |
| `SURVEYS/Infrastructure/Messaging/SurveyNotificationEventConsumer.cs` | Inbox/dedup và dispatch notification |
| `SURVEYS/Infrastructure/Messaging/SurveyNotificationDeduplication.cs` | Stable dedup-key builder; không gửi email |
| `API/Controllers/SystemSurveyWorkQueueController.cs` | Admin queue route |
| `API/Controllers/SystemManagerSurveyWorkQueueController.cs` | Manager queue route |
| `API/Controllers/TenantOwnerSurveyWorkQueueController.cs` | Owner queue route |
| `docs/operations/be1-survey-operations-runbook.md` | Redrive/reconciliation/recovery procedures |

Không tạo `SurveyAuditService.cs` chứa logic của mọi use case. Dùng audit port hiện hữu; mỗi handler tự cung cấp action/resource/reason/before-after phù hợp nghiệp vụ của nó.

#### File manifest Step 11

Step 11 không tạo thêm aggregate/handler nghiệp vụ. Trong Phase 11A–11B, các file mục tiêu là migration và tài liệu; test files chỉ được tạo ở Final Test Phase mục 27:

| Thao tác | File |
|---|---|
| `[Tạo/Cập nhật]` | `docs/openapi/be1-surveys.openapi.json` |
| `[Tạo/Cập nhật]` | `docs/operations/be1-survey-operations-runbook.md` |
| `[Tạo]` | `docs/operations/be1-core-release-report.md` |
| `[Tạo khi có schema change]` | `DB/Migrations/<timestamp>_<descriptive-name>.cs` và `.Designer.cs` |

Tên file và cấu trúc E2E tests được quản lý tại mục 27.4.

---

## 9. Step 1 — Runtime Surveys module và UC03 Catalogue/Master Data

### 9.0. Chia phase và cách triển khai Step 1

Không làm toàn bộ Step 1 trong một lần. Thực hiện lần lượt theo năm phase sau:

| Phase | Mục tiêu | Kết quả phải nhìn thấy |
|---|---|---|
| 1A — Runtime foundation | Làm cho module Surveys được API khởi tạo và truy cập database đúng boundary | Module được đăng ký DI, migration model hợp lệ, health check chạy |
| 1B — Public catalogue | Cho khách hàng đọc hai dịch vụ đang nhận yêu cầu | Public query chỉ trả service hợp lệ và giá hiện hành |
| 1C — SystemAdmin catalogue | Cho SystemAdmin xem lịch sử và điều khiển lifecycle service | Có query admin, activate/experimental/retire và audit |
| 1D — Versioned per-pole pricing | Quản lý PricePerPole theo thời gian, không sửa lịch sử | Tạo price version mới, ngăn overlap, resolve đúng giá tại một thời điểm |
| 1E — Business master data | Khóa disease/severity/recommendation/criteria version phục vụ result | Result tham chiếu đúng version bất biến, kể cả version đã retire |

#### Phase 1A — Runtime foundation: cần viết gì

Tạo hoặc hoàn thiện các nhóm file sau:

- `AgriDrone.Modules.Surveys/DependencyInjection.cs`: chỉ đăng ký dependency của module; không chứa truy vấn hay nghiệp vụ.
- `Application/Abstractions/Persistence/`: repository và unit-of-work interface mà Application cần dùng.
- `Infrastructure/Persistence/Repositories/`: mỗi aggregate repository một file.
- `Infrastructure/Queries/`: mỗi read model/query service một file; không trả EF entity ra Application/API.
- `SurveysDbContext`: khai báo DbSet, áp dụng configuration và transaction behavior của riêng Surveys.
- `AgriDrone.Api/Program.cs`: gọi một hàm đăng ký module duy nhất.

Thứ tự thực hiện:

1. Liệt kê tất cả dependency mà handler Step 1 cần: DbContext, repository, query service, time provider, authorization, audit và outbox.
2. Tạo interface ở Application trước; Application không được tham chiếu class Infrastructure.
3. Viết implementation ở Infrastructure và đăng ký scoped lifetime.
4. Đăng ký MediatR handler và FluentValidation validator từ assembly Surveys.
5. Đăng ký module tại API và thêm health check chỉ kiểm tra khả năng kết nối/phụ thuộc cần thiết.
6. Ghi requirement controller không inject `SurveysDbContext` hoặc repository trực tiếp vào architecture-test backlog mục 27.

Không chuyển sang Phase 1B nếu module chỉ compile nhưng chưa resolve được dependency khi khởi động API.

#### Phase 1B — Public catalogue: luồng nghiệp vụ cần hoàn thành

Luồng đọc catalogue phải chạy như sau:

1. Nhận thời điểm hiện tại từ `TimeProvider`, không lấy thời gian do client gửi.
2. Đọc service có trạng thái được phép nhận yêu cầu công khai.
3. Với mỗi service, resolve đúng price version có `EffectiveFrom <= now` và `EffectiveTo > now` hoặc chưa có `EffectiveTo`.
4. Loại service đã retire, service chưa active hoặc service không có giá hiện hành theo policy.
5. Map sang public response; không trả internal status, audit field, reviewer hoặc dữ liệu cấu hình AI.
6. Sắp xếp ổn định để OpenAPI/consumer không nhận thứ tự ngẫu nhiên.

Tạo một feature folder riêng, ví dụ `Application/Features/Catalogue/GetPublicSurveyServices/`, gồm query, handler và response. API contract và controller đặt ở `AgriDrone.Api`, không đặt DTO HTTP trong Domain.

#### Phase 1C — SystemAdmin catalogue: nghiệp vụ cần viết

Mỗi action là một use case/folder riêng:

- Xem danh sách và lịch sử service/price, bao gồm experimental và retired.
- Kích hoạt service chỉ khi metadata bắt buộc và policy giá đã thỏa mãn.
- Đưa service về experimental khi capability chưa đủ điều kiện công bố chính thức.
- Retire service để chặn request mới nhưng vẫn đọc được order/result lịch sử.
- Cập nhật tên/mô tả chỉ khi policy cho phép; không làm thay đổi snapshot đã lưu ở order/result.

Mỗi mutation phải có command, validator, handler, domain method, application error và audit record. Unit test tương ứng được viết ở Final Test Phase. Không viết một `SurveyServiceCommands.cs` chứa nhiều command/handler.

#### Phase 1D — Versioned pricing: nghiệp vụ cần viết

Không xây API “sửa giá hiện tại”. Nghiệp vụ đúng là tạo một phiên bản giá mới:

1. SystemAdmin gửi service, giá, currency và thời điểm hiệu lực.
2. Validator kiểm tra cú pháp; Domain/Application kiểm tra giá dương, currency được hỗ trợ và cửa sổ thời gian hợp lệ.
3. Handler khóa/đọc các price version liên quan trong transaction.
4. Kiểm tra không overlap với bất kỳ version nào của cùng service.
5. Nếu thay thế giá hiện hành, đóng `EffectiveTo` của version cũ theo policy rồi thêm version mới; không sửa amount của version cũ.
6. Ghi audit trước/sau và commit một lần.
7. Order về sau chỉ lưu `PricePerPole` snapshot từ version được resolve sau khi có confirmed pole count; order cũ không bị cập nhật theo catalogue.

Feature nên tách thành `CreateSurveyServicePrice`, `GetSurveyServicePriceHistory` và query resolve giá nội bộ. Repository phải có truy vấn kiểm tra overlap tại database; scenario hai request đồng thời được kiểm chứng ở Final Test Phase.

#### Phase 1E — Business master data: phạm vi cần viết

- Tách catalogue bệnh/tổn thương, health level, expert-validated treatment recommendation và harvest-readiness criteria khỏi cấu hình model AI.
- Recommendation mapping theo disease + severity, có version/lifecycle/effective window và advisory disclaimer; không nhận free-form treatment do AI sinh.
- Mỗi definition có code ổn định, version, lifecycle status, effective window và metadata hiển thị.
- Order/job/result lưu ID hoặc version snapshot đã dùng, không chỉ lưu label hiện hành.
- Retire definition chỉ chặn sử dụng mới; history vẫn resolve được.
- Chưa đủ bằng chứng cho Harvest Readiness thì giữ `Experimental`; không trả wording khiến người dùng hiểu là đã validated.

Đầu ra Step 1 phải trả lời được ba câu hỏi bằng query: “dịch vụ nào đang bán?”, “giá nào áp dụng tại thời điểm X?” và “một kết quả cũ đã dùng criteria version nào?”. Test xác nhận được viết ở Final Test Phase.

### 9.1. Runtime wiring

- [x] Thêm project references tối thiểu cần thiết cho Application/Infrastructure; không tạo circular dependency.
- [x] Tạo `DependencyInjection.cs` đăng ký `SurveysDbContext`, repositories, query services, validators và handlers.
- [x] Đăng ký module trong API `Program.cs`.
- [x] Thêm database health/readiness check phù hợp.
- Yêu cầu architecture test tương ứng đã chuyển xuống mục 27.2; không thực hiện trong Step 1.

### 9.2. Survey Service domain/application

- [x] Thêm factory/domain methods cho create, activate, mark experimental, retire và update descriptive metadata theo policy.
- [x] Thêm price-version command với `PricePerPole > 0`, currency `VND`, effective window và non-overlap validation.
- [x] Không sửa amount/currency hoặc delete price version đã được order tham chiếu; chỉ cho phép đóng `EffectiveTo` một lần khi tạo replacement version.
- [x] Public query resolve đúng service/price đang nhận request tại thời điểm query.
- [x] SystemAdmin query vẫn đọc được retired/history version.
- [x] Chỉ seed `PLANT_HEALTH`, `HARVEST_READINESS`; không seed Follow-up/Baseline Mapping.

### 9.3. Business master data

- [x] Giữ fixed health levels và PlantCondition lifecycle hiện hữu.
- [x] Chốt disease/non-disease catalogue version cho selected diseases, sunburn và mechanical damage.
- [x] Thêm expert-validated treatment recommendation catalogue và mapping disease + severity có version/provenance.
- [x] Quản lý AI model/threshold reference dùng cho audit/result provenance mà không đưa inference runtime vào BE1.
- [x] Thêm/hoàn thiện metadata cho Harvest Readiness criteria version, assessment granularity và `Experimental/Validated` status.
- [x] Tách business criteria khỏi BE2 AI model/threshold configuration.
- [x] Result/job snapshot reference đúng version; retired definition vẫn resolve cho history.

Checkpoint Step 1: **CodeComplete — AwaitingFinalTest** ngày 2026-10-09. Evidence triển khai tại `docs/operations/be1-step1e-business-master-data-report.md`; test xác nhận vẫn thuộc Final Test Phase.

### 9.4. API đề xuất

```text
GET  /api/public/survey-services

GET  /api/system-admin/survey-services
POST /api/system-admin/survey-services/{serviceId}/prices
POST /api/system-admin/survey-services/{serviceId}/activate
POST /api/system-admin/survey-services/{serviceId}/retire
```

Các mutation catalogue phải dùng SystemAdmin policy; public endpoint chỉ trả trường an toàn và indicative current price.

### Tests Step 1

Chưa viết/chạy trong Step 1. Toàn bộ backlog kiểm thử của Step 1 đã chuyển xuống mục 27.2.

### Definition of Done Step 1

- Surveys module chạy runtime và sẵn sàng cho DI/application/persistence test ở Final Test Phase.
- Public catalogue ổn định cho Step 2.
- Master-data version đủ để snapshot vào Order/Job/Result.

---

## 10. Step 2 — UC04 Survey Request intake và review (Phase 7)

### 10.0. Chia phase và cách triển khai Step 2

| Phase | Nghiệp vụ | Không được làm trong phase này |
|---|---|---|
| 2A — Request foundation | Repository, number generation, idempotency, response chung | Chưa tạo Tenant/Farm/Order |
| 2B — NewCustomer intake | Public gửi yêu cầu cho khách hàng mới | Không tự tạo account hoặc Tenant |
| 2C — ExistingTenantNewFarm intake | TenantOwner yêu cầu thêm Farm | Không tin TenantId trong payload |
| 2D — ExistingFarmSurvey intake | TenantOwner yêu cầu khảo sát Farm hiện hữu | Không tin Farm/Tenant ownership từ client |
| 2E — Admin review | Inbox, detail, start review, reject | Chưa expose approve |
| 2F — Contract/security hardening | API contracts, PII và OpenAPI | Không bỏ qua idempotency/concurrency trong implementation |

#### Phase 2A — Request foundation: cần viết gì

- `ISurveyRequestRepository`: lấy theo ID, lấy theo caller scope + idempotency key, thêm request và truy vấn phục vụ transition.
- Read query riêng cho SystemAdmin inbox/detail và TenantOwner history.
- Service sinh `RequestNumber` ở server với unique constraint; client không được cấp request number.
- Response chung chỉ chứa ID, request number, kind, status, created time và thông tin cần thiết cho actor đó.
- Mỗi use case có folder riêng dưới `Application/Features/Requests/`; không gom ba loại submit vào một handler có nhiều nhánh khó kiểm soát.

Quy tắc idempotency phải được triển khai trước ba flow:

1. Xác định caller scope từ actor: public fingerprint/session policy hoặc authenticated actor/tenant.
2. Normalize idempotency key.
3. Tìm request đã tồn tại theo cặp caller scope + key.
4. Nếu payload tương đương, trả lại kết quả cũ; nếu cùng key nhưng payload khác, trả conflict ổn định.
5. Dùng unique constraint để xử lý race condition; không chỉ kiểm tra bằng code.

Checkpoint 2026-10-09: Phase 2A đạt `CodeComplete — AwaitingFinalTest`.
`RequestIdempotency` được chốt tối đa 100 ký tự đồng bộ với schema; repository,
read-query, request-number generator, acknowledgement response, payload
fingerprint/idempotency resolver và DI đã hoàn tất. Ba submit flow bắt đầu từ
Phase 2B; test evidence vẫn được tập trung ở Final Test Phase theo kế hoạch.

#### Phase 2B — NewCustomer intake: luồng nghiệp vụ

1. Public actor chọn một service đang nhận request.
2. Nhập applicant/contact, thông tin Farm dự kiến, approximate area, map location/coordinates, estimated pole count nếu biết, khoảng thời gian mong muốn và ghi chú.
3. Handler chuẩn hóa email/phone/text, tạo value objects cần thiết và gọi factory `CreateNewCustomer`.
4. Factory bắt buộc applicant/farm fields và cấm TenantId, FarmId, RequestedByUserId.
5. Lưu duy nhất SurveyRequest + audit/outbox acknowledgement.
6. Trả acknowledgement; không provision Tenant, User, Farm, assignment, invitation hoặc SurveyOrder.

File cần có: command, validator, handler, response và API request contract riêng cho `SubmitNewCustomerSurveyRequest`.

Checkpoint 2026-10-09: Phase 2B đạt `CodeComplete — AwaitingFinalTest`.
Factory NewCustomer, validation/normalization, idempotent handler, API input
contract và acknowledgement email outbox đã hoàn tất. Request, system audit và
outbox được commit cùng transaction; flow không tạo Tenant, User, Farm,
assignment, invitation hoặc SurveyOrder. Public controller/caller-fingerprint
policy vẫn thuộc contract/security hardening Phase 2F.

#### Phase 2C — ExistingTenantNewFarm intake: luồng nghiệp vụ

1. Yêu cầu đăng nhập TenantOwner.
2. Lấy UserId/TenantId từ execution context và membership query ở server.
3. Xác nhận owner/membership còn active tại thời điểm xử lý.
4. Payload chỉ chứa dữ liệu Farm mới và service; bỏ qua hoặc từ chối TenantId do client tự gửi.
5. Factory lưu TenantId và RequestedByUserId, bắt buộc FarmId là null.
6. Lưu request; chưa tạo Farm.

Tạo feature `SubmitNewFarmSurveyRequest`. Ghi hai scenario owner tenant A gửi tenant B và membership vừa bị vô hiệu hóa vào Final Test Backlog.

Checkpoint 2026-10-09: Phase 2C đạt `CodeComplete — AwaitingFinalTest`.
TenantId/UserId và applicant contact được lấy từ execution context cùng Identity
read port phía server; command/API input không nhận các giá trị định danh đó từ
client. Handler kiểm tra lại active TenantOwner trước cả idempotent replay, lưu
SurveyRequest, user audit và tenant-scoped acknowledgement outbox trong cùng
transaction, đồng thời không tạo Farm hoặc SurveyOrder. Public controller và
header mapping vẫn thuộc contract/security hardening Phase 2F.

#### Phase 2D — ExistingFarmSurvey intake: luồng nghiệp vụ

1. Route nhận FarmId.
2. Query Farm reference ở server để tìm TenantId và trạng thái Farm.
3. Xác nhận actor là active TenantOwner của Tenant đó.
4. Xác nhận Farm active và service có thể áp dụng.
5. Factory lưu TenantId, FarmId và RequestedByUserId; không nhận các giá trị này từ body.
6. Chưa chọn previous compatible order tại intake; Step 3 sẽ resolve lại trong transaction approval.

Tạo feature `SubmitExistingFarmSurveyRequest`; contract test chống ID enumeration/cross-tenant được viết ở Final Test Phase.

Checkpoint 2026-10-10: Phase 2D đạt `CodeComplete — AwaitingFinalTest`.
FarmId được lấy từ route; Farms read port phía server resolve TenantId, trạng thái
và snapshot Farm. Handler đối chiếu tenant context, kiểm tra lại active
TenantOwner, active Farm và service/current per-pole price trước khi tạo request.
TenantId, FarmId, RequestedByUserId và applicant/Farm snapshot không được lấy từ
body. SurveyRequest, farm-scoped user audit và acknowledgement outbox được commit
cùng transaction; intake không resolve previous compatible order và không tạo
Farm hoặc SurveyOrder. Controller/header mapping vẫn thuộc Phase 2F.

#### Phase 2E — Admin review: luồng nghiệp vụ

Tách thành các use case `GetSurveyRequestInbox`, `GetSurveyRequestDetail`, `StartSurveyRequestReview` và `RejectSurveyRequest`.

- Inbox có filter status/kind/service/date, paging và sort ổn định.
- Detail trả request snapshot, map/estimated-count inputs, checklist version và dữ liệu tham chiếu cần review; không trả secret và không coi applicant geometry là approved FarmBoundary.
- Start review dùng expected version và chỉ cho phép `Submitted -> UnderReview`.
- Reject bắt buộc reason + checklist snapshot, append một review record và transition domain.
- Audit và notification outbox nằm cùng transaction với transition.
- Approve không xuất hiện ở controller/OpenAPI Step 2.

Checkpoint 2026-10-10: Phase 2E đạt `CodeComplete — AwaitingFinalTest`.
SystemAdmin inbox/detail có filter, paging/sort ổn định, request snapshot, candidate
location được đánh dấu không phải approved FarmBoundary và checklist definition
`survey-request-review.v1`. Start-review/reject dùng expected version; reject lưu
append-only checklist snapshot + reason, audit đã redacted và rejection email
outbox trong cùng transaction. Application/API input contracts đã sẵn sàng;
controller, role-policy attachment và OpenAPI vẫn thuộc Phase 2F. Approve không
được expose hoặc orchestration trong Step 2.

#### Phase 2F — Điều kiện đóng Step 2

Có thể chuyển sang code Step 3 khi ba submit flow đã hoàn chỉnh, idempotency xử lý concurrent request, admin reject lưu immutable review history và không tạo Tenant/Farm/Order trước approval. Evidence bằng test được xác nhận tập trung ở Final Test Phase.

Checkpoint 2026-10-10: Phase 2F và toàn bộ Step 2 đạt
`CodeComplete — AwaitingFinalTest`. Bảy HTTP route mục tiêu đã được đăng ký với
`AllowAnonymous`, `TenantOwner` và `SystemAdmin` policy tương ứng; hai nhóm submit
bắt buộc header `Idempotency-Key`. Public caller scope được sinh phía server bằng
anonymous session cookie đã bảo vệ và không dựa vào IP/User-Agent hay giá trị scope
do client gửi. API response dùng enum string ổn định, candidate location được đánh
dấu không phải approved FarmBoundary, OpenAPI snapshot đã sinh từ runtime và không
có route `approve`. Evidence triển khai tại
`docs/operations/be1-step2f-contract-security-report.md`; test xác nhận vẫn thuộc
Final Test Phase.

### 10.1. Domain behavior

- [x] Tạo `SurveyRequest.CreateNewCustomer`.
- [x] Tạo `SurveyRequest.CreateExistingTenantNewFarm`.
- [x] Tạo `SurveyRequest.CreateExistingFarmSurvey`.
- [x] Domain validate field bắt buộc theo kind, approximate area dương, optional estimated pole count dương, location SRID 4326, preferred-time range và active service.
- [x] `StartReview`, `Reject`, `Withdraw`; approve domain transition chỉ được gọi bởi Step 3 orchestration.
- [x] Review snapshot append-only, không update/delete quyết định cũ.
- [x] Normalize caller scope + idempotency key bằng primitive hiện có.

### 10.2. Application/repository

- [x] `ISurveyRequestRepository` và query service cho inbox/detail/history.
- [x] Public submit handler không tạo Tenant/Farm/Order.
- [x] TenantOwner new-farm handler derive TenantId/UserId từ execution context.
- [x] Existing-farm handler resolve Farm → Tenant ở server, không tin request TenantId.
- [x] SystemAdmin start-review/reject handlers với expected version.
- [x] Acknowledgement/rejection outbox và audit cùng transaction.
- [x] PII redaction cho logs, metrics và error detail.

### 10.3. Review checklist

- [x] Contact/applicant validity.
- [x] Farm là vườn thanh long.
- [x] Supported service area.
- [x] Farm location/candidate boundary đủ để thẩm định sơ bộ; approval không tự biến nó thành approved FarmBoundary.
- [x] Preliminary legal/flight feasibility.
- [x] Active + available + flight-qualified SystemManager tồn tại.
- [x] Checklist version/snapshot được lưu cùng review.
- [x] Reject bắt buộc reason; approve validation được chuẩn bị nhưng chưa expose trước Step 3.

### 10.4. API mục tiêu

```text
POST /api/public/survey-requests

POST /api/tenant-owner/survey-requests/new-farm
POST /api/tenant-owner/farms/{farmId}/survey-requests

GET  /api/system-admin/survey-requests
GET  /api/system-admin/survey-requests/{requestId}
POST /api/system-admin/survey-requests/{requestId}/start-review
POST /api/system-admin/survey-requests/{requestId}/reject
```

`approve` chưa được đăng ký trong Step 2 vì side effects bắt buộc thuộc atomic orchestration Step 3.

### Tests Step 2

Chưa viết/chạy trong Step 2. Toàn bộ backlog kiểm thử của Step 2 đã chuyển xuống mục 27.2.

### Definition of Done Step 2

- Public/TenantOwner có entry point đúng nghiệp vụ.
- SystemAdmin có inbox/start-review/reject hoàn chỉnh.
- Không có đường provision resource trước approval.
- Implementation gate Phase 7 và OpenAPI snapshot hoàn tất; test evidence bổ sung ở Final Test Phase.

---

## 11. Step 3 — UC05 Atomic approval, onboarding và SurveyOrder (Phase 8)

### 11.0. Chia phase và cách triển khai Step 3

| Phase | Nội dung | Gate để đi tiếp |
|---|---|---|
| 3A — Approval prerequisites | Port/query kiểm tra request, service, manager, owner và previous order | Tất cả dependency có contract rõ ràng |
| 3B — Atomic persistence boundary | Implement `ISurveyApprovalUnitOfWork`/specialized DbContext | Một transaction rõ ràng, có failure seams |
| 3C — Approve từng request kind | NewCustomer, ExistingTenantNewFarm, ExistingFarmSurvey | Mỗi kind có orchestration độc lập |
| 3D — Retry/concurrency | Idempotency, unique constraints, stale version | Concurrent approve không nhân đôi dữ liệu |
| 3E — API và evidence | Endpoint, response, audit, outbox, OpenAPI | Sẵn sàng cho E2E ở Final Test Phase |

#### Phase 3A — Chuẩn bị dependency trước handler

Application cần các port/query rõ trách nhiệm:

- Đọc request để approve và khóa/version-check request.
- Kiểm tra service vẫn hợp lệ.
- Kiểm tra SystemManager profile, availability, qualification và assignment conflict.
- Provision Tenant, Farm và Owner invitation qua internal port; không gọi HTTP controller nội bộ.
- Resolve current published Farm base map.
- Resolve previous compatible order theo Farm + service.
- Ghi audit và outbox trong transaction approval.

Mỗi port cần mô tả input/output và lỗi ổn định. Không bắt đầu handler approve khi một port vẫn trả EF entity của module khác hoặc chưa có contract rõ ràng.

Checkpoint 2026-10-10: Phase 3A đạt `CodeComplete — AwaitingFinalTest`.
Đã khóa approval-only request repository, reference snapshots/query semantics,
staged provisioning/outbox ports, SurveyOrder repository, stable prerequisite
errors và resolver theo ba request kind. `ExistingFarmSurvey` không reassign
manager trong approve; manager ID nếu có chỉ là expected-current guard. Resolver
chưa được đăng ký runtime cho đến khi Phase 3B cung cấp toàn bộ implementation
trên cùng `SurveyApprovalDbContext`. Evidence tại
`docs/operations/be1-step3a-approval-prerequisites-report.md`.

#### Phase 3B — Viết atomic persistence boundary

Specialized approval DbContext/UoW phải map đúng các bảng cần commit cùng nhau: request/review, resource được provision theo kind, assignment, order, invitation, audit và outbox.

Thứ tự xây dựng:

1. Tạo implementation của `ISurveyApprovalUnitOfWork` trong project database/infrastructure phù hợp.
2. Chỉ expose một `ExecuteInTransactionAsync` cho orchestration; handler không tự gọi nhiều `SaveChanges`.
3. Thêm unique constraint cho Request → approved Order và các business key cần chống retry.
4. Tạo failure seam để Final Test Phase có thể chèn lỗi lần lượt sau Tenant, Farm, assignment, invitation và outbox.
5. Bảo đảm thiết kế cho phép kiểm tra tất cả bảng không có side effect dở dang và request chưa Approved.

Checkpoint 2026-10-10: Phase 3B đạt `CodeComplete — AwaitingFinalTest`.
`SurveyApprovalDbContext` map đúng approval graph và dùng một Npgsql
`RepeatableRead` transaction; request được `FOR UPDATE`, các reference mutable
được `FOR SHARE`. Provisioning chỉ flush trên context này, UoW facade từ chối
`SaveChangesAsync` từ application và chỉ commit qua `ExecuteInTransactionAsync`.
`Result.Failure` trả ra từ orchestration cũng kích hoạt rollback trước khi UoW
trả lỗi về caller.
Failure injector chạy sau database flush tại Tenant, Farm, primary assignment,
Owner invitation và Outbox nhưng trước commit. Các unique constraint mục tiêu đã
tồn tại nên Phase 3B không tạo migration mới. Runtime DI của prerequisite
resolver và toàn bộ approval adapter đã được nối; approve handler/HTTP action vẫn
chưa được tạo. Evidence tại
`docs/operations/be1-step3b-atomic-persistence-report.md`.

#### Phase 3C — Viết ba orchestration riêng theo request kind

Handler chung có thể điều phối, nhưng logic mỗi kind phải được tách thành strategy/method có tên nghiệp vụ rõ ràng; test riêng được viết ở Final Test Phase.

`NewCustomer`:

1. Revalidate request/service/manager.
2. Provision Tenant.
3. Provision Farm thuộc Tenant vừa tạo.
4. Tạo SurveyOrder và primary assignment.
5. Tạo TenantOwner invitation, không tạo password hay active account.
6. Append approved review, transition request, ghi audit/outbox rồi commit.

`ExistingTenantNewFarm`:

1. Revalidate requester vẫn là active owner của Tenant.
2. Không tạo Tenant/User/membership/invitation.
3. Provision Farm trong đúng Tenant.
4. Tạo order và primary assignment.
5. Transition/audit/outbox rồi commit.

`ExistingFarmSurvey`:

1. Revalidate Farm active và ownership.
2. Kiểm tra hoặc chọn primary manager theo policy; không âm thầm thay assignment.
3. Không tạo Tenant/Farm/invitation.
4. Resolve previous compatible published/completed order cùng service.
5. Tạo order rồi transition/audit/outbox và commit.

#### Phase 3D — Khởi tạo SurveyOrder và chống retry

- `RequiresBaselineMapping` lấy từ current published Farm base map + confirmed active-pole inventory, không lấy từ client hay lịch sử mission.
- Order bắt đầu ở `PendingBoundaryVerification`; chưa có confirmed pole count hoặc price snapshot.
- OrderNumber sinh ở server và unique.
- Approval command mang expected request version và idempotency key.
- Retry cùng command trả cùng kết quả; payload khác với cùng key trả conflict.
- Hai admin approve đồng thời chỉ một transaction thắng; transaction thua không để lại resource phụ.

#### Phase 3E — Nhóm file/use case cần có

- `Application/Features/Approvals/ApproveSurveyRequest/`: command, validator, handler, response.
- Các policy/resolver nhỏ cho manager eligibility, previous compatible order và baseline requirement; đặt theo business concept, không tạo file helper tổng hợp.
- Infrastructure implementation của approval UoW.
- API request/response contract và SystemAdmin controller action.
- Danh sách unit/PostgreSQL/HTTP test tương ứng nằm tại mục 27; không viết/chạy trong Step 3.

Step 3 chỉ đạt `CodeComplete — AwaitingFinalTest` trước phase cuối. Atomic rollback trên PostgreSQL thật là release gate bắt buộc tại mục 27.

### 11.1. Approval command

- [ ] Tạo `ApproveSurveyRequestCommand` gồm RequestId, selected SystemManagerId, checklist/reason, ExpectedVersion và idempotency key.
- [ ] Chỉ SystemAdmin được gọi.
- [ ] Re-read request/service/manager state trong transaction; không tin dữ liệu đã xem trước đó.
- [ ] Manager phải active, available, flight-qualified và qualification còn hiệu lực.
- [ ] Claim approval idempotency/unique approved-order constraint trước side effects.

### 11.2. NewCustomer orchestration

- [ ] Tạo Tenant bằng `ITenantProvisioningPort`/cross-module persistence đã khóa.
- [ ] Tạo Farm từ request snapshot bằng `IFarmProvisioningPort`; request geometry/location chỉ là candidate, chưa là approved FarmBoundary.
- [ ] Tạo SurveyOrder.
- [ ] Tạo primary FarmManagerAssignment.
- [ ] Tạo TenantOwner invitation; không tạo password/active account thay khách hàng.
- [ ] Ghi request review/Approved, audit và invitation/approval outbox trong cùng commit.

### 11.3. ExistingTenantNewFarm orchestration

- [ ] Re-check requester vẫn là active TenantOwner.
- [ ] Không tạo Tenant/User/Owner membership/invitation.
- [ ] Tạo Farm trong đúng Tenant, SurveyOrder và primary assignment.
- [ ] Cross-tenant request/Farm reference bị reject.

### 11.4. ExistingFarmSurvey orchestration

- [ ] Re-check Farm thuộc TenantOwner, active và có primary manager hợp lệ; nếu cần reassign phải dùng explicit audited admin flow.
- [ ] Không tạo Tenant/Farm/invitation.
- [ ] Tạo SurveyOrder.
- [ ] Resolve previous compatible order theo cùng Farm + cùng Service + completed/published gần nhất.
- [ ] Service khác không được dùng làm previous order.

### 11.5. Order initialization

- [ ] Snapshot `RequiresBaselineMapping` từ current published Farm base map + confirmed inventory; không suy ra từ Mission gần nhất.
- [ ] Lưu Request/Tenant/Farm/Service và nullable previous compatible order.
- [ ] Initial status `PendingBoundaryVerification`.
- [ ] Unique Request→ApprovedOrder và unique OrderNumber.
- [ ] Không snapshot price ở approval; price chỉ được snapshot sau approved boundary/scope và authoritative confirmed pole count ở Step 4.

### 11.6. Transaction implementation

- [ ] Implement specialized approval DbContext/UoW đã chốt ở Step 0.
- [ ] Một PostgreSQL transaction/một commit bao phủ tất cả side effects bắt buộc.
- [ ] Failure ở bất kỳ resource nào rollback toàn bộ.
- [ ] Nếu chọn orchestration nhiều transaction thay vì atomic DbContext, phải có persisted recovery state, compensation và retry proof được ADR chấp nhận trước; mặc định không chọn cách này.

### 11.7. API mục tiêu

```text
POST /api/system-admin/survey-requests/{requestId}/approve
```

Response trả Request/Order/Farm/Tenant IDs theo kind, không trả invitation token hoặc secret.

### Tests Step 3

Chưa viết/chạy trong Step 3. Toàn bộ backlog kiểm thử của Step 3 đã chuyển xuống mục 27.2.

### Definition of Done Step 3

- Ba request kinds approve end-to-end và atomic.
- Mọi Farm/Order mới truy nguyên được request/reviewer/assignment.
- Invitation accept/login cũ vẫn hoạt động.
- Direct public Tenant/Farm/owner provisioning route không còn trong MVP OpenAPI.

---

## 12. Step 4 — UC06 Boundary/scope, baseline count, pricing, appointment, payment và readiness (Phase 9)

### 12.0. Chia phase và cách triển khai Step 4

| Phase | Nghiệp vụ | Trạng thái/order outcome |
|---|---|---|
| 4A — Verify boundary/scope | Manager xác minh FarmBoundary, Zone và scope | Có approved boundary/scope version |
| 4B — Baseline appointment/readiness | Với Farm chưa mapped, manager đề xuất lịch baseline | Baseline có thể ready mà chưa payment |
| 4C — Confirm pole count/pricing | Publish baseline hoặc kiểm chứng inventory hiện hữu rồi snapshot giá/trụ | Có count + PricePerPole + FinalPrice bất biến |
| 4D — Paid appointment/payment | Owner xác nhận lịch paid service và thanh toán | Payment chỉ Confirmed từ trusted evidence |
| 4E — Adjustment/purpose-aware readiness | Xử lý adjustment/reconciliation và tổng hợp gate cho BE2 | Paid service chỉ ready khi đủ commercial gate |

#### Phase 4A — Verify FarmBoundary và scope

Tạo feature `VerifySurveyOrderBoundaryAndScope` với command, validator, handler và response riêng.

Luồng xử lý:

1. Lấy actor từ execution context và kiểm tra đang là primary assigned SystemManager của Farm.
2. Đọc order cùng service/Farm và kiểm tra expected version/status.
3. Validate SRID 4326, polygon validity, supported area, Zone containment/non-overlap và notes/evidence.
4. Lưu approved FarmBoundary/scope version với reviewer/time/reason; không dùng applicant polygon làm approved data ngầm.
5. Xác định authoritative `RequiresBaselineMapping` từ current published map + confirmed inventory.
6. Transition order sang Baseline appointment hoặc pole-count confirmation, ghi audit/outbox và commit một lần.

#### Phase 4B — Baseline Mapping appointment và readiness

Với Farm chưa có approved base map/confirmed inventory:

- Tạo appointment có `Purpose = BaselineMapping`; manager đề xuất, owner confirm/reschedule và mọi version được giữ.
- Readiness policy cho Baseline Mapping kiểm tra boundary/scope, appointment, active qualified assignment, drone/schedule/pre-flight và safety, nhưng không kiểm tra payment.
- BE2 prepare/start phải truyền MissionPurpose và re-check decision ngay trước flight.
- Sau flight, Step 6 publication trả confirmed active-pole count về Order; không cho client tự gửi count như authoritative result.

Farm đã có current approved base map/inventory bỏ qua Phase 4B; handler phải chứng minh inventory source/version thay vì tạo Baseline Mapping giả.

#### Phase 4C — Confirmed pole count và per-pole price snapshot

1. Nhận confirmed count từ atomic map publication (initial Farm) hoặc authoritative current inventory (mapped Farm).
2. Assigned manager xác nhận count áp dụng trong approved survey scope cùng evidence/version.
3. Resolve effective `PricePerPole` ở server time.
4. Tính `FinalPrice = ConfirmedSurveyPoleCount × PricePerPoleSnapshot` bằng money policy duy nhất.
5. Lưu CountSource/BaseMapVersionId, PriceVersionId, count, PricePerPole, currency và final price; audit/outbox trong cùng boundary.
6. Khoá snapshot; catalogue/inventory thay đổi sau đó không silent-recalculate order.

Không dùng approximate area để tính giá và không diễn giải estimated pole count của applicant là confirmed count.

#### Phase 4D — Paid-service appointment và payment callback

Tách các use case appointment theo purpose: `ProposeSurveyAppointment`, `ConfirmSurveyAppointment`, `RequestSurveyAppointmentReschedule`, và nếu policy cho phép thì `CancelSurveyAppointment`/`ReproposeSurveyAppointment`.

- Manager chỉ đề xuất paid-service appointment sau confirmed count/price.
- Owner authorization derive qua order → Farm → Tenant; không tin TenantId body.
- Mỗi lần reschedule tạo history/version hoặc entity mới; không mất cửa sổ cũ.
- Chỉ một current appointment cho mỗi order + purpose; database constraint bảo vệ race.
- Confirm lưu actor/time; reschedule/cancel lưu reason; notification phát qua outbox sau commit.

Tách adapter provider khỏi nghiệp vụ Surveys:

- Application định nghĩa port tạo payment session và xác thực provider event cần thiết.
- Infrastructure/Payments chứa provider adapter, options và signature verification.
- Feature `InitiateSurveyPayment` chỉ lấy amount/currency từ order snapshot; owner không gửi số tiền authoritative.
- Callback endpoint nhận raw provider identifiers, xác thực signature/timestamp trước khi gọi application handler.
- `ProcessPaymentEvent` claim dedup theo provider + reference + event ID, lưu immutable `PaymentEvent`, rồi mới transition payment.
- ACK callback chỉ sau commit; invalid signature không được ghi nhận như successful event.

Không bao giờ có endpoint TenantOwner `confirm payment`; Baseline Mapping không tạo payment riêng.

#### Phase 4E — Adjustment, reconciliation và readiness authoritative service

Tạo use case riêng cho request/approve/reject/apply price adjustment và admin reconciliation.

- Mọi thay đổi confirmed pole count/price sau confirmed payment là append-only adjustment.
- Pending adjustment chặn readiness.
- Approval/reconciliation yêu cầu SystemAdmin, reason, evidence/reference và expected version.
- Amount/currency mismatch không được ép thành Confirmed.
- Refund/chargeback là provider event có transition rõ; nếu flight chưa bắt đầu thì readiness phải đóng lại.
- Không sửa hoặc xóa PaymentEvent cũ để “làm sạch” lịch sử.

Tạo một application query/service duy nhất xây `SurveyOrderReadinessSnapshot` từ `MissionPurpose` và trạng thái hiện hành:

1. Mọi purpose: approved boundary/scope, purpose appointment confirmed/còn hiệu lực, active qualified assignment, drone/schedule/pre-flight/safety hợp lệ.
2. `BaselineMapping`: Order thật sự requires baseline và chưa có initial map trái policy; không yêu cầu count/price/payment.
3. `PlantHealth`/`HarvestReadiness`: current base map + confirmed pole count/price snapshot + payment đúng amount/currency và Confirmed.
4. Không có pending adjustment, refund/chargeback hoặc trạng thái order cấm purpose tương ứng.

Service gọi domain policy để nhận decision/failure codes, sau đó mới transition order nếu đủ điều kiện. Expose decision qua integration port cho BE2; BE2 không tự copy logic này.

Danh sách scenario phải được bổ sung vào Final Test Backlog trong lúc code; chỉ viết test sau khi các phase implementation kết thúc. Final Test Phase phải có ít nhất một test cho mỗi failure code và một test state thay đổi sau khi readiness vừa được đọc.

### 12.1. Boundary/scope và baseline decision

- [ ] Assigned SystemManager query Order/Farm/FarmBoundary/Zone scope.
- [ ] `VerifyBoundaryAndScope` nhận geometry/version, expected version và evidence/notes cần thiết.
- [ ] FarmBoundary dùng SRID 4326, valid polygon; Zone nằm trong boundary và không overlap trái policy.
- [ ] Derive `RequiresBaselineMapping` từ current published map + confirmed inventory, không từ client.
- [ ] Detection/candidate ngoài hoặc nghi ngờ gần biên dùng `OutOfBoundary/NeedsReview`, không tự assign Farm.

### 12.2. Baseline appointment và confirmed pole-count price snapshot

- [ ] Tạo/confirm/reschedule appointment có purpose `BaselineMapping` khi Order requires baseline.
- [ ] Baseline readiness không yêu cầu Payment nhưng vẫn yêu cầu boundary/scope, manager, drone/schedule/pre-flight/safety.
- [ ] Initial map publication hoặc authoritative mapped inventory cung cấp `ConfirmedSurveyPoleCount` + source version.
- [ ] Resolve effective service price và lưu PriceVersionId, PricePerPoleSnapshot, ConfirmedSurveyPoleCount, Currency, FinalPrice.
- [ ] Dùng duy nhất per-pole money policy; approximate area không tham gia công thức.
- [ ] Count/price snapshot bị khóa; catalogue/inventory thay đổi sau đó không ảnh hưởng Order nếu không có adjustment.

### 12.3. Paid-service appointment lifecycle

- [ ] SystemManager `ProposeAppointment` cho selected service với start < end sau confirmed count/price.
- [ ] TenantOwner `ConfirmAppointment` hoặc `RequestReschedule`.
- [ ] Reschedule/cancel tạo history/version; không overwrite mất appointment cũ.
- [ ] Chỉ appointment current + Confirmed + còn hiệu lực đúng MissionPurpose mới mở gate.
- [ ] SystemAdmin support action nếu có phải có reason/audit và không thay actor trong normal flow.

### 12.4. Payment lifecycle

- [ ] TenantOwner initiate payment theo exact current amount/currency.
- [ ] Tạo Payment `Pending/Processing`, provider reference và redirect/client token an toàn.
- [ ] Payment callback endpoint xác thực signature/timestamp/source.
- [ ] Deduplicate theo `(Provider, ProviderReference, ProviderEventId)` dùng `PaymentEventIdentity`.
- [ ] Reject/out-of-order transition không lùi state.
- [ ] Amount/currency mismatch → `AdjustmentRequired`/manual review, không Confirmed.
- [ ] SystemAdmin reconciliation chỉ khi có provider reference, evidence và reason.
- [ ] TenantOwner không có command mark-confirmed.
- [ ] Không tạo/đòi payment riêng cho Baseline Mapping.

### 12.5. Price adjustment

- [ ] Trước payment confirmed, policy đổi boundary/scope/count/price phải explicit và giữ history.
- [ ] Sau payment confirmed, mọi đổi confirmed pole count/price qua PriceAdjustment append-only.
- [ ] Pending adjustment đóng readiness gate.
- [ ] Approve/reject/apply adjustment có separate authorization, expected version và audit.
- [ ] Refund/chargeback đóng gate cho flight chưa bắt đầu.

### 12.6. Purpose-aware readiness service/contract

- [ ] Tạo application service build `SurveyOrderReadinessSnapshot` từ MissionPurpose + database state hiện hành.
- [ ] Không persist `ReadyForOperations` từ payload; nếu lưu OrderStatus thì chỉ transition từ authoritative policy decision.
- [ ] Expose internal query/port cho BE2 prepare/start flight.
- [ ] Decision trả stable failures: boundary, scope, appointment/purpose, count-price, payment, manager, adjustment, eligibility và safety.
- [ ] Re-check manager assignment/qualification thời điểm query.
- Scenario missing payment/Baseline Mapping/paid-service đã chuyển xuống mục 27.2.

### 12.7. API mục tiêu

```text
POST /api/system-manager/survey-orders/{orderId}/boundary-scope/verify
POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm
POST /api/system-manager/survey-orders/{orderId}/appointments

POST /api/tenant-owner/survey-orders/{orderId}/appointments/{appointmentId}/confirm
POST /api/tenant-owner/survey-orders/{orderId}/appointments/{appointmentId}/request-reschedule
POST /api/tenant-owner/survey-orders/{orderId}/payments

POST /api/integrations/payments/{provider}/callbacks
POST /api/system-admin/survey-payments/{paymentId}/reconcile
POST /api/system-admin/survey-orders/{orderId}/price-adjustments/{adjustmentId}/approve
```

### Tests Step 4

Chưa viết/chạy trong Step 4. Toàn bộ backlog kiểm thử của Step 4 đã chuyển xuống mục 27.2.

### Definition of Done Step 4

- Flow chạy từ boundary verification → Baseline (nếu cần) → confirmed pole count/pricing → paid appointment/payment → ready for paid service.
- BE2 nhận authoritative readiness decision và failure reasons.
- Không actor/client nào tự set payment/order ready.
- Exit gate Phase 9 đạt trên PostgreSQL thực.

---

## 13. Step 5 — UC07 Farm/Zone operational verification và BE2 boundary

### 13.0. Chia phase và cách triển khai Step 5

| Phase | Mục tiêu | Đầu ra |
|---|---|---|
| 5A — Farm access | Khóa quyền owner/assigned manager | Không đọc/mutate chéo assignment hoặc tenant |
| 5B — FarmBoundary/Zone verification | Chuẩn hóa và xác minh vùng khảo sát | Approved FarmBoundary/Zone hợp lệ trên PostGIS |
| 5C — Operational context | Cấp context tối thiểu cho BE2 | Contract không lộ EF entity/secret |
| 5D — Archive and history safety | Chặn archive khi còn dependency | Không mất map/result/provenance |

#### Phase 5A — Farm access và operational detail

- Giữ việc tạo Farm sau approval qua internal provisioning port; không mở lại public create Farm.
- Tạo query cho assigned manager lấy Farm/Zone/order context, dùng `ISystemManagerAccessService` hoặc policy tương đương.
- Mỗi mutation operational detail phải kiểm tra assignment ngay lúc xử lý, không chỉ lúc phát token.
- TenantOwner chỉ đọc và gửi change request nếu use case yêu cầu; không được dùng manager command.
- Trả `404` cho resource ngoài visibility và `403` khi resource visible nhưng actor thiếu action permission theo convention Step 0.

#### Phase 5B — FarmBoundary, geometry và Zone verification

Tách từng use case theo hành động: verify/version FarmBoundary, update Farm operational detail, create Zone, update Zone, verify Zone và archive Zone/Farm.

Mỗi handler cần:

1. Parse/normalize geometry tại boundary.
2. Ép SRID 4326 và kiểm tra geometry type.
3. Kiểm tra polygon valid, không rỗng và diện tích hợp lý.
4. Kiểm tra approved FarmBoundary thuộc đúng Farm; Zone nằm trong boundary và không overlap trái policy bằng PostGIS query.
5. Gọi domain method, ghi reviewer/evidence/audit/reason và commit.
6. Cung cấp point-in-polygon/near-boundary policy để candidate thành `OutOfBoundary/NeedsReview` thay vì tự assign Farm.

Geometry phải được thiết kế để kiểm chứng trên PostgreSQL/PostGIS; test tương ứng chỉ viết/chạy ở Final Test Phase.

#### Phase 5C — Contract operational context cho BE2

Implement port đã khóa ở Step 0 với read model tối thiểu: OrderId, FarmId, service type, approved FarmBoundary/scope version, confirmed pole count/price state, purpose-aware readiness decision, baseline requirement, current base-map version và assignment summary.

- Contract trả ID/value snapshot, không trả navigation property hoặc EF entity.
- Plant/base-map references có version và stable ordering.
- Không trả PII, payment provider payload, invitation token hoặc internal audit.
- BE2 phải gọi readiness port tại prepare/start; không cache decision vô thời hạn.

Chốt schema/semantics ở cả producer và consumer side trước khi cho BE2 tích hợp; contract test được viết/chạy ở Final Test Phase.

#### Phase 5D — Archive safety

Archive query phải tổng hợp dependency từ active order, mission, current/published map, published result và history cần retention. Nếu có dependency chặn, trả danh sách reason ổn định. Archive chỉ đổi lifecycle state; không hard-delete Farm, Zone, Plant hay history.

### 13.1. Farm/Zone application features

- [ ] Giữ Farm creation chỉ qua approval internal port.
- [ ] Assigned SystemManager get/update Farm operational details theo policy.
- [ ] Assigned SystemManager verify/version FarmBoundary với evidence/reason/audit.
- [ ] Assigned SystemManager create/update/verify Zone và survey scope nếu workflow yêu cầu.
- [ ] Geometry luôn SRID 4326, valid polygon, Zone within approved FarmBoundary và non-overlap.
- [ ] Boundary exception query/classification hỗ trợ `OutOfBoundary/NeedsReview`; không cross-Farm auto-assignment.
- [ ] TenantOwner chỉ đọc/request change; không có direct mutation tạo Farm/Mission.
- [ ] Archive check active Order + Mission + published map/result/history.

### 13.2. Work context cho BE2

- [ ] Query Order operational context gồm OrderId, FarmId, service type, MissionPurpose, RequiresBaselineMapping, current base-map reference, approved boundary/scope, confirmed pole count/price state và manager assignment summary.
- [ ] Contract không trả secrets/payment payload hoặc EF entity.
- [ ] BE2 không được tự suy readiness từ copy trạng thái; phải dùng Step 4 decision.
- [ ] Plant reference/base-map query có version và stable ordering.

### Tests Step 5

Chưa viết/chạy trong Step 5. Toàn bộ backlog kiểm thử của Step 5 đã chuyển xuống mục 27.2.

### Definition of Done Step 5

- SystemManager có đủ verified Farm/Zone/scope data để BE2 chuẩn bị Mission.
- TenantOwner không có operational mutation path.
- Phase 10 của BE2 có thể tích hợp mà không đọc DbContext BE1.

---

## 14. Step 6 — UC08 Farm base map và persistent Plant identity (Phase 11)

### 14.0. Chia phase và cách triển khai Step 6

| Phase | Mục tiêu | Gate |
|---|---|---|
| 6A — V3 ingestion | Nhận và deduplicate pending mapping candidates | Message invalid không tạo business data |
| 6B — Draft, manager review and batch validation | Tạo draft, review và kiểm tra toàn Farm | Không publish partial/unreviewed Zone |
| 6C — Atomic publication | Commit map, plants, history, audit, outbox | Transaction/failure seams hoàn chỉnh |
| 6D — Persistent Plant identity | Giữ/reuse/thay Plant ID đúng rule | Repeat survey không nhân đôi Plant |
| 6E — V1 drain | Chạy song song và retire an toàn | Có metric/retention evidence |

#### Phase 6A — V3 consumer và Inbox

Tạo consumer adapter trong `Infrastructure/Messaging` và application handler riêng cho `BaselineMappingCandidatesReadyV3`. `...ApprovedV2` chỉ chạy compatibility/drain nếu đã có message lịch sử.

Thứ tự bắt buộc:

1. Validate envelope, event type và schema version.
2. Validate required identifiers/correlation/causation.
3. Claim Inbox theo MessageId; duplicate đã hoàn thành thì ACK không xử lý lại.
4. Query Order/Farm/Mission authoritative state.
5. Kiểm tra mission đúng purpose, order đúng Farm, `RequiresBaselineMapping = true` và chưa có published initial map trái policy.
6. Chỉ sau các bước trên mới tạo draft/import pending candidate; chưa tạo Plant hoặc publish map.

Phân loại lỗi rõ: payload/schema sai là permanent/DLQ; database/network là transient/retry; duplicate là success-idempotent. Không ACK trước commit.

#### Phase 6B — Draft, SystemManager review và validate toàn batch

Tạo application feature/import service chịu trách nhiệm xây draft `FarmBaseMapVersion` và các Zone slice. Validation phải chạy trên toàn bộ candidate set:

- Candidate ID/source reference không trùng.
- Geometry đúng SRID và được kiểm tra với approved FarmBoundary/Zone; outside hoặc near-boundary candidate thành exception, không tự assign.
- Row/column hoặc spatial ordering hợp lệ theo policy.
- Một candidate chỉ resolve thành tối đa một Plant decision.
- Matched Plant phải thuộc đúng Farm và đúng current map context.
- Tất cả Zone của event cùng Farm/header/version.
- Batch thiếu/invalid phải fail toàn bộ, không bỏ qua âm thầm vài Zone.

Assigned SystemManager review/correct/approve/reject từng candidate hoặc batch và boundary exception với optimistic concurrency. Lưu original/corrected decision/reason/evidence; chỉ batch đã review đủ mới được publication. TenantOwner không được đọc draft/candidate.

#### Phase 6C — Atomic publication

Implement `IMappingPublicationUnitOfWork` đã định nghĩa ở Step 0. Một transaction phải bao gồm:

- Inbox state.
- FarmBaseMapVersion và ZoneMapVersions.
- Plant create/link/update decision.
- PlantChangeEvents.
- Current published map switch/version check.
- Audit.
- `FarmBaseMapPublishedV3` outbox event; V2 không bị sửa payload.
- Confirmed active-pole inventory/count handoff để SurveyOrder chuyển sang per-pole pricing.

Publication flow: khóa current version → kiểm tra expected version/boundary → áp dụng toàn bộ Plant decisions → tính/xác nhận active pole count → đánh dấu map Published/current → ghi audit/outbox → commit. Failure ở Plant thứ N hoặc khi cập nhật count handoff phải rollback cả Inbox claim để message có thể retry an toàn theo cơ chế đã chọn.

#### Phase 6D — Plant identity rules cần hiện thực

Mỗi decision phải có tên nghiệp vụ riêng; test tương ứng được viết ở Final Test Phase:

- `MatchExisting`: giữ nguyên Plant ID.
- `Relocate`: giữ Plant ID, thêm location/history mới.
- `CreateNew`: tạo Plant ID mới sau approval.
- `Replace`: tạo ID mới và liên kết Plant cũ theo replacement relation.
- `Unmatched/Ambiguous`: không tự tạo Plant chính thức.
- `MissingObservation`: không tự retire Plant.

Repeat Plant Health/Harvest Readiness chỉ tham chiếu current map và Plant IDs; không tạo FarmBaseMapVersion mới. Sửa map sau initial publication đi qua BaseMapAmendment có reason/reviewer/audit.

#### Phase 6E — File/evidence cần có

- Consumer + handler V2 tách file.
- Batch validator/domain policy tách theo mapping concept.
- UoW implementation và repository/query cần thiết.
- Các golden JSON, PostgreSQL/PostGIS, retry/DLQ và atomic rollback tests tương ứng được liệt kê ở mục 27.
- Dashboard/metric cho V1/V2 received, completed, retry và DLQ trước khi xóa V1.

Không chuyển Step 7 nếu BE2 chưa đọc lại được published base-map/Plant reference snapshot đúng version.

### 14.1. V3 consumer entry point

- [ ] Consumer riêng nhận `BaselineMappingCandidatesReadyV3`; V2 cũ chỉ compatibility/drain.
- [ ] Validate envelope/schema trước payload.
- [ ] Inbox check/claim theo MessageId.
- [ ] Validate Order/Farm/Mission/purpose/assigned reviewer/expected base-map state.
- [ ] Chỉ order `RequiresBaselineMapping = true` và BaselineMapping Mission hợp lệ được initial publish.
- [ ] ACK transport chỉ sau transaction commit; transient retry/permanent DLQ theo policy.
- [ ] Assigned SystemManager review/correct/approve/reject pending candidates/boundary exceptions trước publication.

### 14.2. Farm-level publication

- [ ] Tạo Draft `FarmBaseMapVersion` với SourceSurveyOrderId.
- [ ] Tạo `ZoneMapVersion` slices thuộc cùng Farm header.
- [ ] Validate all candidates theo batch: IDs, geometry, row/column, duplicate, resolved Plant và Farm/Zone consistency.
- [ ] Classify `OutOfBoundary/NeedsReview`; không tạo/link Plant cho exception chưa có manager decision.
- [ ] Tạo/link Plants và PlantChangeEvents theo reviewed decisions.
- [ ] Publish toàn Farm atomically; không lộ partial Zone state.
- [ ] Enforce tối đa một current Published base map/Farm.
- [ ] Phát `FarmBaseMapPublishedV3` qua Outbox; giữ V2 compatibility/drain theo metric.
- [ ] Ghi/phát `ConfirmedSurveyPoleCount` cùng source map version để Step 4C khóa per-pole price; duplicate event không tính giá hai lần.

### 14.3. Plant rules

- [ ] Candidate/observation chưa publish không tạo Plant chính thức.
- [ ] Matched Plant giữ ID; relocation giữ ID.
- [ ] New/replacement Plant tạo ID mới theo explicit decision.
- [ ] Missing observation không retire Plant.
- [ ] Owner-reported `NewlyAdded` không được `CreateNew` nếu thiếu later-survey candidate + manager approval liên kết với report.
- [ ] Repeat service survey chỉ lưu observation/match result, không tạo base-map version.
- [ ] Map correction sau initial publish đi qua `BaseMapAmendment` có reason/review/audit.

### 14.4. V1 migration

- [ ] V3 producer/consumer có routing/queue riêng; không rewrite V2.
- [ ] V1 consumer chỉ tồn tại để drain/replay hợp lệ.
- [ ] Theo dõi V1 receive/outbox/retry/DLQ counts.
- [ ] Không xóa V1 trước retention window và Phase 14 gate.

### Tests Step 6

Chưa viết/chạy trong Step 6. Toàn bộ backlog kiểm thử của Step 6 đã chuyển xuống mục 27.2.

### Definition of Done Step 6

- Farm có current approved base map rõ ràng.
- Plant chính thức chỉ xuất hiện sau Farm-level publish.
- BE2 có thể reuse base map/Plant IDs cho survey sau.
- Exit gate Phase 11 đạt.

---

## 15. Step 7 — UC09 Plant Health review và official result (Phase 12)

### 15.0. Chia phase và cách triển khai Step 7

| Phase | Nghiệp vụ | Dữ liệu được phép thay đổi |
|---|---|---|
| 7A — Health/zone ingestion | Import AI observations và proposed Disease Zones | Chỉ pending/provenance |
| 7B — Human review | Manager correct/approve/reject finding/zone/recommendation | Review history, chưa đổi current health |
| 7C — Result approval | Khóa một official candidate result | SurveyResult/review state |
| 7D — Atomic publication | Publish result và cập nhật current health | Official result + derived current state |
| 7E — Completion/output | Thông báo BE2 và owner published view | Outbox/read model |

#### Phase 7A — Import health observations

Tạo V2 consumer và application feature riêng. Handler phải:

1. Validate event/service là Plant Health.
2. Resolve Order, Mission, Farm và base-map version.
3. Kiểm tra source observation/job version và provenance.
4. Claim Inbox/dedup theo message và source observation version.
5. Import resolved items thành immutable pending findings.
6. Import proposed Disease Zones, memberships/geometry và recommendation selection dưới dạng pending với model/threshold/catalogue provenance.
7. Giữ ambiguous/unmatched/boundary-exception items ở trạng thái cần xử lý; không gán bừa vào Plant/Farm.
8. Không cập nhật current Plant health và không publish zone/recommendation trong ingestion.

Nếu reprocess cùng source với model/output version mới, tạo version mới và liên kết supersedes; không overwrite AI output cũ.

#### Phase 7B — Human review

Tách use case đọc queue, review một finding và review batch. Authorization luôn kiểm tra current primary assignment + qualification.

Mỗi quyết định review phải lưu:

- AI original label/severity/evidence/model version.
- Corrected value nếu có.
- Decision approve/reject.
- Reviewer, reviewed time, reason và expected version.
- Reference tới Order/Farm/Plant/source observation.
- Disease Zone original/corrected geometry + member findings, và recommendation catalogue/version được chọn theo disease/severity.

Domain validation phải phân biệt Healthy, disease, sunburn, mechanical damage và severity allowed combinations. Recommendation phải đến từ expert-validated catalogue; không lưu free-form AI treatment advice làm official. Hai reviewer cùng sửa finding/zone phải nhận conflict, không last-write-wins.

#### Phase 7C — Approve result candidate

Feature `ApprovePlantHealthSurveyResult` kiểm tra mọi required finding, Disease Zone, boundary exception và recommendation selection đã có final review decision, không còn unresolved blocking item và order đúng state. Handler tạo/transition `SurveyResult` sang Approved nhưng vẫn chưa lộ cho TenantOwner và chưa cập nhật current health.

#### Phase 7D — Atomic publication

Implement `ISurveyResultPublicationUnitOfWork` cho Plant Health. Một commit bao gồm:

- SurveyResult Published và publication metadata.
- Official finding/review references.
- Approved Disease Zone geometry/membership và treatment-recommendation catalogue provenance.
- Derived current health/condition của Plant theo ordering rule.
- SurveyOrder transition.
- Audit.
- Official outcome + notification outbox.

Khi áp dụng current health, so sánh observed/effective time và publication/version; result cũ đến muộn không được ghi đè state mới. Tạo failure seam cho rollback scenario giữa quá trình cập nhật nhiều Plant; test được viết ở Final Test Phase.

#### Phase 7E — API và output cần viết

- SystemManager pending findings/Disease Zones query, review/correction actions, recommendation selection, approve result và publish result.
- TenantOwner result query chỉ đọc Published.
- Event cho BE2 biết official review/publication outcome để complete Mission theo contract.
- Notification event cho owner sau commit.
- Previous comparison resolver chỉ chọn published Plant Health order cùng Farm/service.

Đóng Step 7 khi có thể truy từ official result về reviewer, AI source, Mission, Order, Farm, Plant và base-map version mà không đọc raw table tùy tiện.

### 15.1. Plant Health V3 ingestion

- [ ] Consumer nhận `PlantHealthAnalysisReadyV3`; `HealthObservationsReadyV2` chỉ compatibility/drain.
- [ ] Validate service là Plant Health, Order/Mission/Farm/base-map version và source provenance.
- [ ] Inbox/idempotency theo message + source observation/version.
- [ ] Import resolved observations thành pending findings; không đổi Plant current health.
- [ ] Import proposed Disease Zones/recommendation selection/boundary exceptions dưới dạng pending, giữ full provenance.
- [ ] Ambiguous/unmatched item không được biến thành resolved finding.
- [ ] Reprocess tạo output/finding version mới, không overwrite provenance.

### 15.2. SystemManager review

- [ ] Assigned SystemManager query pending queue theo Order/Farm.
- [ ] Review/correct/approve/reject item hoặc batch theo policy.
- [ ] Review/correct/approve/reject Disease Zone geometry/memberships; unresolved boundary exception chặn publication.
- [ ] Chọn/xác nhận recommendation từ expert-validated disease + severity catalogue; không nhận free-form advice.
- [ ] Validate condition/Healthy/severity và sunburn/mechanical damage semantics.
- [ ] Mọi correction giữ AI original value, corrected value, reviewer, reason và evidence.
- [ ] Optimistic concurrency ngăn hai reviewer commit đè nhau.

### 15.3. Publication

- [ ] Tạo SurveyResult `PendingReview` cho Order.
- [ ] Chỉ approve khi required findings đã resolved.
- [ ] Publish atomically official findings, approved Disease Zones, recommendation provenance, review history, derived current health, Order status, audit và Outbox.
- [ ] Current health update theo observed/effective ordering rule; older result không được silently ghi đè newer official state.
- [ ] Phát official result/review state V2 cho BE2 và notification event.
- [ ] Follow-up comparison dùng previous published Plant Health order.

### 15.4. API mục tiêu

```text
GET  /api/system-manager/survey-orders/{orderId}/health-findings
POST /api/system-manager/survey-orders/{orderId}/health-findings/{findingId}/review
GET  /api/system-manager/survey-orders/{orderId}/disease-zones
POST /api/system-manager/survey-orders/{orderId}/disease-zones/{zoneId}/review
POST /api/system-manager/survey-orders/{orderId}/health-results/approve
POST /api/system-manager/survey-orders/{orderId}/health-results/publish
```

### Tests Step 7

Chưa viết/chạy trong Step 7. Toàn bộ backlog kiểm thử của Step 7 đã chuyển xuống mục 27.2.

### Definition of Done Step 7

- Plant Health đi từ V2 raw handoff tới Official Result đã publish.
- BE2 chỉ complete Mission sau publication outcome phù hợp.
- TenantOwner chỉ thấy published findings, approved Disease Zones và catalogue-based advisory recommendations.

---

## 16. Step 8 — UC10 Harvest Readiness review và official result (Phase 12)

### 16.0. Chia phase và cách triển khai Step 8

| Phase | Mục tiêu | Gate |
|---|---|---|
| 8A — Domain evidence gate | Chốt criteria/granularity/claim | Chưa chốt thì chỉ được Experimental |
| 8B — V2 ingestion | Import assessment/evidence bất biến | Không dùng Harvest legacy |
| 8C — Human review | Correct/approve/reject với provenance | Pending không lộ owner |
| 8D — Publication | Publish official readiness result | Atomic result/order/audit/outbox |
| 8E — Comparison and wording | Hiển thị đúng confidence/capability status | Không gắn validated claim sai |

#### Phase 8A — Viết domain specification trước code

Tạo một tài liệu/versioned definition nêu rõ:

- Criteria code/version và lifecycle `Experimental/Validated/Retired`.
- Observable indicators nào được dùng và đơn vị/allowed values.
- Granularity là Farm, Zone hay Plant; không tự giả định.
- Cách diễn giải AI assessment/confidence.
- Evidence tối thiểu để reviewer ra quyết định.
- Ground-truth/evaluation protocol và điều kiện promote sang Validated.

Application phải snapshot criteria version vào assessment/result. Nếu product/domain owner chưa chốt các mục trên, chỉ xây ingestion/review framework và trả wording Experimental.

#### Phase 8B — Ingestion

Consumer V2 thực hiện envelope/Inbox validation giống Step 7, sau đó kiểm tra Order service là Harvest Readiness, Farm/Mission đúng context, granularity phù hợp criteria version và evidence đủ định dạng.

Lưu AI assessment, confidence, visible indicators, evidence, model/criteria version và source provenance dưới dạng immutable pending assessment. Không tham chiếu hoặc tái tạo `HarvestBatch`, `PlantHarvestRecord`, quality grade, yield hay quantity.

#### Phase 8C — Human review

Tạo queue/detail/review use cases riêng. Manager có thể giữ nguyên, correct hoặc reject assessment theo policy. Luôn lưu original + corrected values, reason, reviewer, time và version. Nếu granularity không phải Plant thì API/read model không được ép PlantId giả.

#### Phase 8D — Approve và publish

Tách approve khỏi publish như Plant Health. Approve kiểm tra assessment coverage/resolution; publish dùng result publication UoW để commit SurveyResult, official assessment references, Order status, audit và outbox. Harvest Readiness không cập nhật Plant current-health field của Plant Health.

#### Phase 8E — Comparison và API

- Previous compatible resolver chỉ chọn published Harvest Readiness result cùng Farm và cùng criteria-compatible policy.
- Public/owner response luôn kèm criteria version, granularity và Experimental/Validated status.
- Plant Health order không được dùng làm comparison; contract/E2E verification nằm ở mục 27.
- Production code không phụ thuộc Harvests legacy; architecture verification nằm ở mục 27.
- Pending/rejected assessment không xuất hiện trong owner query.

### 16.1. Pre-implementation domain gate

- [ ] Chốt criteria version.
- [ ] Chốt observable indicators.
- [ ] Chốt assessment granularity; không mặc định per-Plant nếu domain study chưa xác nhận.
- [ ] Chốt ground-truth/dataset/evaluation protocol và evidence requirements.
- [ ] Chốt điều kiện chuyển `Experimental → Validated`.

Nếu các mục trên chưa chốt, chỉ triển khai framework/pending model; không công bố capability đã validated.

### 16.2. Ingestion/review

- [ ] Consumer/handler nhận pending Readiness handoff V2.
- [ ] Validate Order service, Farm, Mission, criteria/model version và granularity.
- [ ] Import immutable AI assessment/evidence vào SurveyResult pending.
- [ ] Assigned SystemManager correct/approve/reject; giữ original/corrected values.
- [ ] Không tham chiếu HarvestBatch, PlantHarvestRecord, HarvestQualityGrade, quantity hoặc yield.

### 16.3. Publication/follow-up

- [ ] Publish official Harvest Readiness result qua result publication UoW.
- [ ] Response/public metadata phản ánh đúng `Experimental/Validated`.
- [ ] Previous compatible chỉ là published Harvest Readiness order cùng Farm.
- [ ] Phát official outcome cho BE2 và notification.

### Tests Step 8

Chưa viết/chạy trong Step 8. Toàn bộ backlog kiểm thử của Step 8 đã chuyển xuống mục 27.2.

### Definition of Done Step 8

- Harvest Readiness chạy end-to-end như một Survey Service riêng.
- Không tái tạo Harvest Management.
- Official result có criteria/model/evidence/reviewer provenance.

---

## 17. Step 9 — UC11 Digital Plant Profile, history và follow-up (Phase 13)

### 17.0. Chia phase và cách triển khai Step 9

| Phase | Mục tiêu | Đầu ra |
|---|---|---|
| 9A — Owner farm/order views | Danh sách Farm và survey history | Published/owned data only |
| 9B — Map/plant views | Current map, plant count/list/detail | Stable paging và map version |
| 9C — Unified timeline | Hồ sơ dọc theo thời gian | Provenance + stable ordering |
| 9D — Owner plant-change workflow | Owner report, manager verify, later-survey evidence | Không mutate profile trực tiếp/hard-delete history |
| 9E — Performance/cache | Index, benchmark và invalidation | Không N+1, cache không mở rộng quyền |

#### Phase 9A — Owner Farm và survey history

Tạo read-model query riêng thay vì dùng repository aggregate cho màn hình:

- `GetTenantOwnerFarms`: derive TenantId từ execution context.
- `GetFarmSurveyOrders`: filter đúng Farm/Tenant, paging và sort ổn định.
- `GetPublishedSurveyResult`: chỉ Published và đúng owner.
- `GetSurveyComparison`: dùng stored `PreviousCompatibleOrderId`, không tự chọn order khác service.

Mọi query phải áp ownership ngay trong database predicate. Không tải resource trước rồi mới che bằng application code.

#### Phase 9B — Current map và Plant queries

- Query current Published FarmBaseMapVersion; draft/superseded không lộ trừ history được policy cho phép.
- Trả plant count nhất quán với cùng map version.
- Plant list có paging, Zone/map filters và stable ordering.
- Plant detail trả identity/lifecycle/current official summaries, không trả raw AI observation.
- Response luôn mang map/result version cần thiết để client nhận biết snapshot.

#### Phase 9C — Unified Digital Plant Profile timeline

Xây read model hợp nhất từ PlantChangeEvent, official Plant Health, official Harvest Readiness và published media references. Mỗi timeline item có event type, effective/observed time, published time, source Order/Mission/result và version.

Định nghĩa ordering/tie-breaker rõ ràng, ví dụ effective time rồi published time rồi stable ID. Query không được tạo N+1 khi lấy nhiều media/result summary.

#### Phase 9D — Owner-reported plant inventory change và amendment

Tách `ReportPlantRemoved`, `ReportPlantReplaced`, `ReportNewPlant`, `ReviewPlantInventoryChangeReport` và `ApplyVerifiedPlantChange` thành các feature riêng:

- TenantOwner chỉ submit report trong Farm mình sở hữu; report chứa type, target Plant nếu có, approximate location/evidence/notes và idempotency key.
- Submit chỉ tạo pending report; không retire/replace/create Plant và không sửa published map.
- Assigned/qualified manager review, correct hoặc reject với reason/evidence/expected version.
- Removal/replacement đã verify đổi lifecycle state nhưng giữ profile/history; replacement tạo ID mới + relation chỉ khi evidence/policy cho phép.
- `NewlyAdded` cần candidate quan sát ở later survey và manager approval trước khi tạo Digital Plant Profile mới.
- Relocation do verified survey evidence giữ Plant ID; mọi thay đổi geometry/published map đi qua BaseMapAmendment.
- Concurrent/replayed report/review/apply phải idempotent và audit append-only.

#### Phase 9E — Performance và cache

Đo query plan/dataset gần thực tế trước khi thêm index. Cache key phải chứa Tenant/Farm/Plant và publication/version context; authorization vẫn kiểm tra trước khi đọc/trả cache. Invalidate sau map publication, result publication và amendment. Khi cache lỗi phải fallback database mà không nới visibility.

### 17.1. Published-only read models

- [ ] TenantOwner Farm list/order status.
- [ ] Current published Farm map và plant count.
- [ ] Plant list theo Farm/Zone/map với pagination/spatial filters cần thiết.
- [ ] Digital Plant Profile detail.
- [ ] Timeline hợp nhất PlantChangeEvent, official health, readiness và published media references.
- [ ] Survey history theo Farm/service.
- [ ] Follow-up comparison từ stored `PreviousCompatibleOrderId`.

### 17.2. Plant lifecycle/amendment

- [ ] TenantOwner submit/query pending removed/replaced/new-plant report trong own Farm.
- [ ] Assigned SystemManager review/correct/approve/reject report theo assignment và expected version.
- [ ] Report không mutate official Plant; `NewlyAdded` chỉ tạo profile sau later-survey candidate + manager approval.
- [ ] Relocate giữ ID; replace tạo ID mới + link cây cũ.
- [ ] Mọi mutation có reason/audit và BaseMapAmendment khi ảnh hưởng published map.
- [ ] Không hard-delete Plant/profile/history.

### 17.3. Cache/index/performance

- [ ] Database là source of truth; cache key bao gồm Tenant/Farm/publication/version context.
- [ ] Invalidate cache sau map/result/amendment publication.
- [ ] Index/benchmark current map, Zone plants, profile timeline, survey history và previous compatible order.
- [ ] Không N+1 khi tải profile/timeline/media summaries.

### API mục tiêu

```text
GET /api/tenant-owner/farms
GET /api/tenant-owner/farms/{farmId}/survey-orders
GET /api/tenant-owner/farms/{farmId}/map
GET /api/tenant-owner/farms/{farmId}/plants
GET /api/tenant-owner/plants/{plantId}
GET /api/tenant-owner/plants/{plantId}/timeline
GET /api/tenant-owner/survey-orders/{orderId}/result
GET /api/tenant-owner/survey-orders/{orderId}/comparison
POST /api/tenant-owner/farms/{farmId}/plant-inventory-change-reports
GET  /api/tenant-owner/farms/{farmId}/plant-inventory-change-reports
GET  /api/system-manager/farms/{farmId}/plant-inventory-change-reports
POST /api/system-manager/plant-inventory-change-reports/{reportId}/review
```

### Tests Step 9

Chưa viết/chạy trong Step 9. Toàn bộ backlog kiểm thử của Step 9 đã chuyển xuống mục 27.2.

### Definition of Done Step 9

- TenantOwner xem được toàn bộ giá trị published của sản phẩm.
- Persistent Digital Plant identity và longitudinal history được chứng minh end-to-end.
- Exit gate Phase 13 đạt.

---

## 18. Step 10 — UC12 Work queues, notifications và audit completion

### 18.0. Chia phase và cách triển khai Step 10

| Phase | Nội dung | Kết quả |
|---|---|---|
| 10A — Queue inventory | Liệt kê action pending của từng actor | Không cần query database thủ công |
| 10B — Queue read models | Paging/filter/sort và scope | Không lộ cross-tenant/assignment |
| 10C — Notifications | Event, template, dedup, retry | Business commit không phụ thuộc email |
| 10D — Audit completeness | Mutation matrix và append-only audit | Truy được ai/làm gì/khi nào/vì sao |
| 10E — Observability/runbook | Metrics, traces, redrive/recovery | Vận hành được failure/replay |

#### Phase 10A — Lập inventory trước khi viết query

Tạo bảng actor → queue item → source state → action route:

- SystemAdmin: submitted/under-review requests, manager eligibility exceptions, payment mismatch/reconciliation, failed publication/integration.
- SystemManager: assigned orders chờ boundary/scope, Baseline/paid appointment, baseline-count review, boundary exceptions, findings/Disease Zones, plant-change reports và publication actions.
- TenantOwner: requests, Baseline/paid appointment confirmation/reschedule, payment action, plant-change report status và newly published results.

Mỗi queue item phải chỉ ra action tiếp theo và resource version; không trả deep link mà endpoint đích không re-authorize.

#### Phase 10B — Queue read models

Mỗi actor có query service/read model riêng. Filter authorization trong SQL/query layer, dùng cursor hoặc paging ổn định, không trả toàn bộ dataset rồi lọc memory. Queue không sở hữu business state mới; nó chỉ tổng hợp state của các aggregate hiện có.

#### Phase 10C — Notification pipeline

Cho mỗi business event, định nghĩa event type, dedup key, recipient resolver, template key, required variables và deep link. Business handler chỉ ghi outbox; consumer gửi email/push sau commit. Consumer có inbox/dedup, retry policy và DLQ. Delivery failure không rollback request/order/result.

Không đưa PII không cần thiết, invitation token hoặc provider secret vào log/event. Deep link luôn quay lại endpoint có authorization.

#### Phase 10D — Audit completeness

Lập mutation matrix cho Steps 1–9. Mỗi mutation trọng yếu phải ghi actor, action, resource IDs, occurred time, reason, correlation, expected/new version và before/after tối thiểu. Audit append-only và nằm cùng transaction business; không dùng application log thay audit record.

#### Phase 10E — Observability và vận hành

Thêm metric/trace theo RequestId, OrderId, FarmId, MissionId, message ID và correlation khi phù hợp. Viết runbook cho notification redrive, outbox stuck, inbox poison message, payment reconciliation và publication retry. Replay/dedup test được viết ở Final Test Phase.

Step này được thực hiện tăng dần trong Steps 1–9, sau đó có gate hoàn thiện riêng.

### 18.1. Work queues

- [ ] SystemAdmin: request inbox, manager availability, payment reconciliation và operational exceptions.
- [ ] SystemManager: assigned Orders, boundary/scope pending, purpose appointments, count confirmation, processing/Disease Zone/change-report review và publication actions.
- [ ] TenantOwner: requests, purpose appointments, payment/order/change-report status và published results.
- [ ] Pagination/filter/sort ổn định và tenant/assignment scoped từ query layer.

### 18.2. Notification events

- [ ] Request acknowledgement/rejection/approval.
- [ ] TenantOwner invitation.
- [ ] Baseline/paid-service appointment proposed/reschedule/confirmed.
- [ ] Payment action/state/reconciliation.
- [ ] Review required.
- [ ] Boundary exception, Disease Zone/recommendation và plant-change decision.
- [ ] Official result published.
- [ ] Stable dedup key, template variables tối thiểu và deep link phải authorize lại.

### 18.3. Audit/observability

- [ ] Approval/rejection.
- [ ] Assignment/reassignment.
- [ ] FarmBoundary/scope, confirmed-pole-count/PricePerPole snapshot và PriceAdjustment.
- [ ] Appointment/payment/reconciliation.
- [ ] AI correction, Disease Zone/recommendation review và publication.
- [ ] Plant lifecycle/base-map amendment.
- [ ] Log/metric có RequestId, OrderId, TenantId, FarmId, MissionId, correlation và actor khi phù hợp.
- [ ] Không log password, invitation token, provider secret hoặc raw PII không cần thiết.

### Tests Step 10

Chưa viết/chạy trong Step 10. Toàn bộ backlog kiểm thử của Step 10 đã chuyển xuống mục 27.2.

### Definition of Done Step 10

- Mỗi actor có work queue đủ để vận hành luồng mà không truy cập database/manual API tùy tiện.
- Business event, delivery và audit truy vết được end-to-end.

---

## 19. Step 11 — Residual cleanup, integration và release gate (Phase 14–15)

### 19.0. Chia phase và cách triển khai Step 11

| Phase | Mục tiêu | Bằng chứng bắt buộc |
|---|---|---|
| 11A — Dependency inventory | Biết chính xác legacy nào còn dùng | `rg`, project graph, route/message metrics |
| 11B — Safe cleanup | Gỡ route/consumer/code đã được thay thế | Code/build sẵn sàng cho final verification |
| 11C — Viết test tập trung | Viết toàn bộ unit/architecture/contract/integration/E2E còn thiếu | Test suite hoàn chỉnh, chưa chạy full suite |
| 11D — Chạy test cuối cùng | Chạy một lần theo tầng và sửa lỗi | Fresh + upgrade + HTTP/DB/message/threat/perf evidence |
| 11E — Operational handoff | Runbook, OpenAPI, completion report | Có owner và recovery procedure |

#### Phase 11A — Inventory trước khi xóa

Lập danh sách từng legacy route, project reference, table, consumer, queue và scheduled job. Với mỗi mục ghi replacement, producer/consumer còn lại, usage metric, retention requirement và quyết định keep/remove. Search source chỉ là một bằng chứng; message queue/database/runtime metric cũng phải được kiểm tra.

#### Phase 11B — Cleanup theo thứ tự an toàn

1. Ngừng producer/route cũ sau khi replacement đã hoàn chỉnh và usage metric cho phép; contract verification nằm ở Final Test Phase.
2. Giữ consumer tương thích trong drain window.
3. Theo dõi retry/DLQ/outbox cho đến khi đạt gate.
4. Gỡ DI registration và project reference trước.
5. Gỡ production code/API sau đó.
6. Chỉ chuẩn bị contract/drop migration khi có inventory + retention approval; chỉ áp dụng release sau khi upgrade test cuối cùng pass.

Không xóa migration cũ, audit, Inbox/Outbox hoặc historical published evidence.

#### Phase 11C — Viết test tập trung

Sau khi Step 0R–10 đã `CodeComplete — AwaitingFinalTest`, viết toàn bộ test còn thiếu theo backlog mục 27. Không chạy full suite trong lúc đang tạo từng nhóm test; chỉ dùng build/compile để phát hiện lỗi cú pháp và reference.

#### Phase 11D — Release verification

Chỉ sau khi viết xong toàn bộ test backlog mới chạy theo thứ tự ở mục 27.5. Mỗi lỗi phải được sửa ở step sở hữu nghiệp vụ; sau khi sửa, tiếp tục từ tầng bị lỗi và cuối cùng chạy lại full release suite một lần.

Security checklist tối thiểu: tenant isolation, assignment revocation, stale token/context, ID enumeration, webhook signature/replay và secret/PII logging. Performance checklist tối thiểu: queue paging, current map, plant list, profile timeline, comparison và large-batch publication.

#### Phase 11E — Hồ sơ bàn giao

Cập nhật OpenAPI, ADR liên quan, migration report, contract/version matrix, operational runbook và completion report. Mọi mục deferred phải ghi rõ lý do, owner, dependency và gate; không đánh dấu Done khi mới có skeleton/interface.

### 19.1. Residual cleanup

- [ ] Xóa direct public System tenant/Farm/owner provisioning routes sau khi replacement Step 3 hoàn chỉnh và usage metric bằng 0; contract verification thực hiện ở Final Test Phase.
- [ ] Xóa old Mission mutation/Mapping V1 compatibility code chỉ sau BE2 replacement và queue drain.
- [ ] Xác nhận không còn production reference đến FieldTasks/Harvests/FarmMembership legacy ngoài migration/archival docs.
- [ ] Không drop archival tables khi chưa có production-like inventory/retention approval.
- [ ] Không xóa migration, audit, inbox/outbox, Plant/Mission/map/result history hoặc published evidence.

### 19.2. E2E scenarios

Toàn bộ E2E scenario chưa viết đã chuyển xuống mục 27.4 để thực hiện sau cùng.

### 19.3. Release quality gates

Các lệnh chạy test và release gates chưa hoàn thành đã chuyển xuống mục 27.5. OpenAPI/runbook vẫn hoàn thiện trong Step 11E trước khi bắt đầu lần chạy cuối.

---

## 20. API actor matrix

| Hành động | Public | SystemAdmin | SystemManager | TenantOwner |
|---|---:|---:|---:|---:|
| Xem public service/indicative price | Có | Có | Có | Có |
| Submit NewCustomer request | Có | Không | Không | Không cần |
| Submit New Farm/Existing Farm request | Không | Không | Không | Có, đúng tenant/farm |
| Review/approve/reject request | Không | Có | Không | Không |
| Quản lý manager/assignment | Không | Có | Không | Không |
| Verify FarmBoundary/scope | Không | Support only | Có, assigned | Xem |
| Confirm active pole count/price input | Không | Support only | Có, assigned | Xem |
| Propose Baseline/paid appointment | Không | Support only | Có, assigned | Không |
| Confirm/request reschedule | Không | Support only | Không | Có |
| Initiate payment | Không | Support only | Không | Có |
| Confirm/reconcile payment | Không | Reconcile có evidence | Không | Không |
| Review/publish AI result | Không | Oversight only | Có, assigned | Không |
| Review Disease Zone/recommendation | Không | Oversight only | Có, assigned | Không |
| Submit plant inventory change report | Không | Support | Không | Có, own Farm |
| Verify/apply plant inventory change | Không | Oversight | Có, assigned | Không |
| Xem raw/draft/pending AI | Không | Theo operations policy | Có, assigned | Không |
| Xem published map/result/profile | Không | Support | Có, assigned | Có, own tenant |

Architecture/API tests khóa ma trận này được viết ở Final Test Phase; implementation không được chỉ dựa vào UI ẩn nút.

## 21. Migration strategy theo step

1. Step 0R: expand/backfill/contract migration cho per-pole pricing, FarmBoundary, Disease Zone/recommendation và PlantInventoryChangeReport; không sửa migration Phase 5.
2. Step 1–2: chỉ migration nhỏ cho field/constraint thực sự thiếu; không gộp commercial/result changes.
3. Step 3: migration/index cho approval idempotency, unique Request→Order và orchestration recovery nếu cần.
4. Step 4: migration riêng cho purpose appointment/payment/count-price adjustment/history còn thiếu.
5. Step 6: V2 base-map/Plant/count association contract migration sau backfill/validation.
6. Step 7–8: result/review/Disease Zone/recommendation provenance association migration theo từng service.
7. Step 9: plant-change workflow và index/read-model optimization migration có benchmark chứng minh.
8. Step 11: contract/drop migration chỉ sau inventory, retention approval và upgrade test.

Mỗi migration phải có:

- [ ] Preflight read-only query.
- [ ] Roll-forward recovery instruction; không sửa migration cũ để rollback giả.

Các nhiệm vụ fresh database, upgrade snapshot và row-count/checksum verification chưa làm đã chuyển xuống mục 27.3; chỉ chạy trong Final Test Phase.

## 22. Test strategy bắt buộc

Chiến lược và toàn bộ backlog test chưa viết đã được chuyển xuống mục 27 ở cuối tài liệu. Các test `[x]` đã tồn tại trong baseline/Step 0R được giữ nguyên và không viết lại.

## 23. Thứ tự PR/commit đề xuất

1. `surveys: add runtime module wiring and repositories`
2. `surveys: rebaseline per-pole pricing boundary and purpose readiness`
3. `surveys: expose service catalogue recommendation master data and versioned pricing`
4. `surveys: add public and tenant-owner request intake`
5. `surveys: add system-admin request review and rejection`
6. `surveys: add atomic request approval orchestration`
7. `farms: verify approved boundary and spatial exceptions`
8. `surveys: add baseline appointment count and immutable per-pole price snapshot`
9. `surveys: add paid appointment payment reconciliation and adjustments`
10. `surveys: expose purpose-aware order readiness contract`
11. `mapping: add farm base-map v2 count publication`
12. `surveys: add plant-health disease-zone recommendation review and publication`
13. `surveys: add harvest-readiness ingestion review and publication`
14. `plants: add owner inventory-change workflow profile timeline and follow-up queries`
15. `surveys: complete actor work queues notifications and audit`
16. `cleanup: retire replaced routes and drained v1 consumers`
17. `tests: add deferred unit architecture contract integration and e2e suites`
18. `release: run final migration security performance and e2e gates`

Không gộp approval, payment, mapping và result publication vào một PR lớn. Mỗi PR code phải ghi rõ migration/rollback scope và test scenarios vào mục 27; test implementation được gom ở PR 17 và chỉ chạy full suite ở PR 18.

## 24. Phân chia sprint đề xuất

### Sprint A — Phase 7 entry point

1. Step 0R per-pole/boundary/readiness/Disease Zone/recommendation/change-report delta.
2. Surveys runtime wiring.
3. Catalogue/PricePerPole/recommendation master data.
4. Survey Request submit/start-review/reject.

### Sprint B — Phase 8 onboarding

5. Approval specialized UoW.
6. NewCustomer flow.
7. ExistingTenantNewFarm flow.
8. ExistingFarmSurvey + previous compatible order.

### Sprint C — Phase 9 commercial gate

9. FarmBoundary/scope và Baseline appointment/readiness.
10. Map/count handoff và PricePerPole snapshot.
11. Paid appointment/payment/callback/reconciliation/adjustment.
12. Purpose-aware readiness contract và BE2 integration boundary.

### Sprint D — Phase 11 mapping V3

13. V2 mapping contracts/consumer.
14. Farm-level atomic publication.
15. Persistent Plant identity/amendment rules.
16. V1 drain metrics.

### Sprint E — Phase 12 results

17. Plant Health/Disease Zone/recommendation ingestion/review/publication.
18. Harvest Readiness criteria gate/ingestion/review/publication.
19. BE2 Mission completion coordination.

### Sprint F — Phase 13–15 product completion

20. Owner published-only portal queries.
21. Owner plant-change workflow, profile/history/follow-up.
22. Work queues/notification/audit completion.
23. Residual cleanup và release runbook.

### Sprint G — Final Test Phase

24. Viết toàn bộ unit/architecture/contract/integration/migration/E2E tests còn thiếu theo mục 27.
25. Chạy test một lần theo thứ tự release; sửa lỗi và chạy lại full release suite cuối cùng.
26. Chốt security/performance evidence và completion report.

## 25. Công việc nên bắt đầu ngay

Step 0 đã hoàn thành trên .NET; Phase 1A runtime wiring chưa hoàn thành và proposal mới tạo delta bắt buộc. Thứ tự tiếp theo:

1. Hoàn thành Step 0R inventory/ADR và expand migration cho per-pole, FarmBoundary, purpose-aware readiness, Disease Zone/recommendation và PlantInventoryChangeReport.
2. Phát hành operational-context V3; giữ V2 bất biến và triển khai đúng semantics Baseline Mapping không cần payment nhưng paid mission bắt buộc payment. Golden tests làm ở Final Test Phase.
3. Hoàn thành Phase 1A runtime wiring, sau đó Phase 1B–1E trên .NET: public catalogue, PricePerPole và master data/recommendation versioning.
4. Triển khai Step 2 request intake/review rồi Step 3 approval orchestration.
5. Không bắt đầu paid-service mission/result flow trước khi boundary → baseline → count → price → appointment/payment dependency đã hoàn chỉnh về code và contract; integration test xác nhận ở Final Test Phase.

Không bắt đầu approval handler Step 3 trước khi Step 1/2 có SurveyRequest state machine, idempotency, internal provisioning contracts, manager eligibility, transaction boundary, unique Request→Order và failure seams cho rollback. Các rollback tests được viết ở Final Test Phase.

## 26. Definition of Done toàn kế hoạch

Chỉ đánh dấu kế hoạch `Done` khi:

- [ ] UC00–UC12 trong `BE1-Use-Case-Specification.md` đạt DoD tương ứng.
- [ ] Ba request kinds và flow chung chạy end-to-end.
- [ ] Không có public/tenant path tạo trực tiếp Tenant/Farm/Mission/Drone.
- [ ] Purpose-aware gate không thể bypass: Baseline cần boundary/appointment/manager/safety; paid service cần thêm count-price/payment.
- [ ] Initial Farm bắt buộc có Baseline Mapping; repeat survey không rebuild map.
- [ ] Persistent Plant IDs và same-service follow-up được chứng minh.
- [ ] Plant Health, Disease Zones/recommendations và Harvest Readiness đều human-reviewed/published, giữ provenance và không dùng chung sai domain model.
- [ ] Owner plant-change report không mutate profile; removal/replacement giữ history và new plant cần later-survey evidence.
- [ ] TenantOwner chỉ xem published map/profile/result/history thuộc Tenant mình.
- [ ] Replay/concurrency/cross-tenant/failure paths pass.
- [ ] Fresh/upgrade EF Core migrations, pending-model check và schema ownership/drift gates pass cho solution .NET.
- [ ] V1 đã drain đúng policy hoặc còn được giữ có lý do/metric rõ ràng.
- [ ] OpenAPI, operational reports, runbooks và completion evidence được cập nhật.
- [ ] Không còn task deferred bị ghi nhầm là hoàn thành; external retention/manual data gates vẫn được nêu rõ nếu chưa có authority/evidence.

## 27. Final Test Phase — viết test tập trung và chạy sau cùng

### 27.1. Quy tắc thực hiện

1. Step 0R–10 ưu tiên hoàn tất domain/application/infrastructure/API/migration/documentation và chỉ ghi thêm scenario vào mục này.
2. Không tạo test mới và không chạy `dotnet test`, test filter, migration test, contract test hay E2E trong các phase code còn lại.
3. Các test đã có và đã được đánh dấu `[x]` được giữ nguyên, không viết lại. Chúng chỉ được chạy lại trong lần chạy cuối.
4. Khi toàn bộ phase code đạt `CodeComplete — AwaitingFinalTest`, viết tất cả test còn thiếu theo thứ tự: unit → architecture → contract → integration/migration → E2E/security/performance.
5. Trong lúc viết test chỉ build/compile để sửa lỗi cú pháp và reference. Chỉ bắt đầu chạy test sau khi toàn bộ test backlog bên dưới đã được tạo.
6. Nếu lần chạy cuối phát hiện lỗi, sửa code tại step sở hữu nghiệp vụ, chạy lại tầng test bị lỗi, rồi chạy lại toàn bộ release suite một lần cuối.
7. Chỉ sau lần chạy cuối xanh mới đổi các phase liên quan từ `CodeComplete — AwaitingFinalTest` sang `Done`.

Trade-off đã chấp nhận: cách này giảm số lần chạy test lặp lại trong quá trình code, nhưng lỗi integration có thể được phát hiện muộn hơn và thời gian sửa cuối phase có thể tăng.

### 27.2. Backlog test theo Step

#### Step 0R — chỉ phần chưa có test

- [ ] PlantInventoryChangeReport state machine: owner report → manager verification → later-survey evidence → applied/rejected.
- [ ] Mapping/result publication boundary cho confirmed active-pole count, Disease Zone/recommendation provenance và plant-change decision.
- [ ] V3 golden JSON cho boundary exception, proposed Disease Zone, recommendation selection, confirmed pole count và plant-change evidence; V2 vẫn bất biến.
- [ ] Stable errors/OpenAPI assumptions không còn price-per-hectare hoặc payment-before-every-flight trong target path.
- [ ] Architecture test pending/raw artifacts không thể đi vào TenantOwner read model.

Không viết lại các unit/migration tests 0R-1 đến 0R-4 đã có cho per-pole, purpose readiness, FarmBoundary, DiseaseZone và TreatmentRecommendation.

#### Step 1 — Catalogue/Master Data

- [ ] Chỉ hai service MVP xuất hiện trong public catalogue.
- [ ] Retired/không có active price không nhận request mới theo policy.
- [ ] Effective price windows không overlap, kể cả hai request đồng thời.
- [ ] Historical price/criteria/label không đổi sau retire/version mới.
- [ ] Published finding chỉ resolve recommendation đúng disease/severity/version; free-form recommendation bị từ chối.
- [ ] Money precision/rounding trên PostgreSQL khớp ADR-0003.
- [ ] Authorization SystemAdmin allow, actor khác deny.
- [ ] API/controller chỉ gọi application handler, không inject `SurveysDbContext` hoặc repository trực tiếp.
- [ ] Runtime DI/application/persistence resolve được khi khởi động API.

#### Step 2 — Survey Request

- [ ] Public submit không tạo Tenant/Farm/Order/assignment/invitation.
- [ ] TenantOwner chỉ submit trong Tenant/Farm của mình; tenant A gửi tenant B và membership vừa vô hiệu hóa đều bị chặn.
- [ ] Ba request kinds validate đúng required/forbidden fields.
- [ ] Duplicate idempotency key cùng caller scope trả cùng request; khác caller scope không collision.
- [ ] Concurrent start-review chỉ một transition thắng.
- [ ] Reject lưu reason/checklist/audit và notification outbox.
- [ ] ExistingFarmSurvey chống ID enumeration/cross-tenant.
- [ ] PII không xuất hiện trong structured logs snapshot.

#### Step 3 — Atomic approval

- [ ] NewCustomer tạo đúng năm nhóm resource, không tạo account/password active.
- [ ] ExistingTenantNewFarm không tạo Tenant/User/Owner mới.
- [ ] ExistingFarmSurvey chỉ tạo Order.
- [ ] Previous compatible order cùng service/farm và đúng thứ tự thời gian.
- [ ] Retry/concurrent approve không nhân đôi bất kỳ resource nào.
- [ ] Chèn lỗi sau Tenant/Farm/assignment/invitation/outbox phải rollback request Approved và mọi resource trước đó.
- [ ] Manager invalid/unavailable/expired qualification bị reject.
- [ ] `RequiresBaselineMapping` đúng với current published Farm base map.
- [ ] HTTP authorization/response đúng cho approval endpoint.

#### Step 4 — Boundary, pricing, payment và readiness

- [ ] Assigned/unassigned/expired manager verify boundary/scope và confirm count allow-deny.
- [ ] Invalid/cross-Farm boundary và OutOfBoundary candidate không được official assignment.
- [ ] Decimal precision/rounding chính xác; FinalPrice bằng `ConfirmedSurveyPoleCount × PricePerPoleSnapshot`.
- [ ] Catalogue price change không đổi snapshot.
- [ ] Wrong TenantOwner không confirm appointment/initiate payment.
- [ ] Duplicate callback idempotent; invalid signature reject; amount/currency mismatch không mở gate.
- [ ] Appointment cancel/reschedule, refund/chargeback và pending adjustment đóng gate.
- [ ] Direct readiness/handler invocation không bypass rule.
- [ ] Baseline không payment vẫn pass; Plant Health/Harvest Readiness thiếu payment phải fail.
- [ ] Mỗi readiness failure code có ít nhất một test; state đổi sau khi snapshot được đọc phải được re-check.
- [ ] Concurrent boundary/count/appointment/payment mutations trả conflict đúng.

#### Step 5 — Farm/Zone operational verification

- [ ] SystemManager A không đọc/mutate Farm của B.
- [ ] Wrong TenantOwner nhận response theo cross-tenant security convention.
- [ ] Geometry rules chạy bằng PostgreSQL/PostGIS integration tests, không thay bằng unit test hình học.
- [ ] Outside/near-boundary points không tự gán Plant/Farm; correction cần manager decision và audit.
- [ ] Archive không làm mất history/provenance.
- [ ] Contract tests producer/consumer với BE2 prepare/start Mission pass.

#### Step 6 — Farm base map và persistent Plant identity

- [ ] First multi-zone publication atomic; duplicate event/approval idempotent.
- [ ] Unassigned manager hoặc batch/boundary exception chưa review đủ không thể publish.
- [ ] Stale expected base-map version/concurrent publish chỉ một commit thắng.
- [ ] Failure Plant thứ N rollback Inbox/map/Plant/history/audit/outbox.
- [ ] Repeat survey không tăng base-map version; missing observation không retire Plant.
- [ ] Outside/near-boundary candidate không cross-Farm assignment.
- [ ] Map publication/count handoff failure rollback; retry không tạo duplicate snapshot.
- [ ] TenantOwner không đọc Draft/candidate.
- [ ] Contract V3 BE1/BE2 pass; V2 compatibility còn xanh trong drain window.
- [ ] Golden JSON, retry/DLQ và PostGIS coverage pass.

#### Step 7 — Plant Health result

- [ ] Pending/rejected raw finding không đổi current health và không lọt owner query.
- [ ] Pending/rejected Disease Zone/recommendation không lọt owner query.
- [ ] Disease Zone correction giữ original geometry/membership; outside-boundary zone không publish.
- [ ] Recommendation sai disease/severity/version hoặc free-form advice bị từ chối.
- [ ] Wrong/unassigned manager deny; duplicate ingestion không tạo finding trùng.
- [ ] Concurrent review/publication chỉ một commit thắng.
- [ ] Failure giữa quá trình cập nhật nhiều Plant rollback result/current health/audit/outbox.
- [ ] Result cũ đến muộn không ghi đè current health mới hơn.
- [ ] Full provenance query được; follow-up không dùng Readiness order.

#### Step 8 — Harvest Readiness result

- [ ] Granularity contract đúng criteria version.
- [ ] Plant Health order không được dùng làm previous comparison.
- [ ] Experimental result không mang validated claim.
- [ ] Duplicate/reprocessed assessment giữ version/provenance.
- [ ] Architecture/source test không có production dependency sang Harvests legacy.
- [ ] TenantOwner không đọc pending assessment.

#### Step 9 — Digital Plant Profile

- [ ] Wrong TenantOwner không suy ra được resource tồn tại.
- [ ] Draft map/pending/rejected result không xuất hiện.
- [ ] Cùng Plant ID xuất hiện qua nhiều survey; relocate/replace semantics và timeline order đúng.
- [ ] Owner report không trực tiếp tạo/retire/replace Plant; retry không tạo report trùng.
- [ ] New-plant report thiếu later-survey evidence không thể tạo Digital Plant Profile.
- [ ] Removed/replaced profile cũ vẫn truy được trong history với lifecycle state đúng.
- [ ] Comparison chỉ dùng same Farm + same service + published/completed result.
- [ ] Cache failure fallback database và không mở rộng visibility.
- [ ] Performance target đạt với dataset gần quy mô thực tế, không N+1.

#### Step 10 — Work queues, notifications và audit

- [ ] Notification failure không rollback business commit.
- [ ] Replay event không tạo notification/audit business trùng ngoài policy.
- [ ] Deep link re-authorize.
- [ ] Work queue không lộ cross-tenant/unassigned data.
- [ ] Audit append-only và before/after/reason đầy đủ cho mutation trọng yếu.

### 27.3. Backlog test kỹ thuật, contract và migration

Các file mapping test cần tạo:

- [ ] `backend/tests/AgriDrone.UnitTests/Mapping/BaselineMappingBatchValidatorTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/Mapping/FarmBaseMapPublicationIntegrationTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/Mapping/FarmBaseMapPublicationRollbackTests.cs`.
- [ ] `backend/tests/AgriDrone.UnitTests/Integration/MappingV2ContractTests.cs`.

Architecture tests:

- [ ] API không dùng DbContext/repository trực tiếp.
- [ ] BE2/module khác không truy cập repository/entity nội bộ của Surveys/Farms/Plants.
- [ ] Không còn production dependency FieldTasks/Harvests/FarmMembership legacy.
- [ ] TenantOwner không có Mission/Drone/raw-AI mutation routes.
- [ ] API actor matrix ở mục 20 được khóa bằng authorization tests.

Contract tests:

- [ ] Golden JSON V1/V2/V3 theo drain policy.
- [ ] BE1↔BE2 purpose-aware readiness, base map/count, boundary exception, health/Disease Zone, readiness-result và completion events.
- [ ] Payment provider callback signature/dedup contract.

Migration/integration tests trên PostgreSQL/PostGIS thật:

- [ ] Unique/concurrency/composite FK/PostGIS constraints.
- [ ] Approval/map/result atomic rollback.
- [ ] Callback/event Inbox/Outbox idempotency.
- [ ] Fresh database migration từ đầu.
- [ ] Upgrade từ từng snapshot được hỗ trợ.
- [ ] Row count/checksum/backfill verification khi đụng dữ liệu cũ.
- [ ] Published-only và cross-tenant query behavior.
- [ ] EF pending-model check bằng 0.

### 27.4. E2E và non-functional backlog

Mỗi E2E scenario phải có Arrange, actor actions, expected state sau từng action, database assertions, emitted/consumed events và negative assertions; không chỉ kiểm tra HTTP 200 cuối flow.

Các file E2E cần tạo/cập nhật:

- [ ] `backend/tests/AgriDrone.IntegrationTests/E2E/NewCustomerSurveyFlowTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/E2E/ExistingTenantNewFarmSurveyFlowTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/E2E/RepeatPlantHealthSurveyFlowTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/E2E/RepeatHarvestReadinessSurveyFlowTests.cs`.
- [ ] `backend/tests/AgriDrone.IntegrationTests/E2E/SurveyFailureReplaySecurityTests.cs`.
- [ ] `backend/tests/AgriDrone.ArchitectureTests/LegacyEndpointSafetyTests.cs`.

Không gom các flow vào một file `SurveyTests.cs`.

- [ ] E2E-01 NewCustomer: request → approval atomic → invitation → boundary/Baseline → map/Plant/count → per-pole pricing/payment → selected service → result/Disease Zone/recommendation → owner portal/change report.
- [ ] E2E-02 ExistingTenantNewFarm: không tạo Tenant/User/Owner dư, tạo đúng Farm/Order/assignment rồi chạy flow chuẩn.
- [ ] E2E-03 Repeat Plant Health: không rebuild base map, reuse Plant IDs, previous comparison là published Plant Health gần nhất.
- [ ] E2E-04 Repeat Harvest Readiness: không dùng Plant Health làm previous, không tạo Harvest legacy, wording Experimental/Validated đúng.
- [ ] E2E-05 Failure/replay/security: approval/payment/event retry, concurrency, cross-tenant/unassigned/unqualified actor, revoked readiness dependencies và published-only visibility.
- [ ] E2E plant inventory change: owner report không mutate profile; new plant cần later-survey evidence; lifecycle/history được giữ.
- [ ] Security: tenant isolation, assignment revocation, stale token/context, ID enumeration, webhook signature/replay và secret/PII logging.
- [ ] Performance: queue paging, current map, Plant list, profile timeline, comparison, media summary và large-batch publication.

Không mock domain authorization/readiness/publication trong E2E happy/failure gates.

### 27.5. Thứ tự chạy test cuối cùng

Chỉ bắt đầu danh sách này sau khi 27.2–27.4 đã viết xong:

1. [ ] Clean full-solution build theo warning policy.
2. [ ] Unit tests, bao gồm toàn bộ test đã có từ baseline và 0R-1–0R-4.
3. [ ] Architecture tests.
4. [ ] Contract/golden JSON tests.
5. [ ] PostgreSQL/PostGIS integration tests.
6. [ ] Fresh migration và upgrade snapshot tests.
7. [ ] EF pending-model check.
8. [ ] E2E tests.
9. [ ] Security tests.
10. [ ] Performance/benchmark checks.
11. [ ] Chạy lại toàn bộ release suite sau khi sửa lỗi cuối cùng.
12. [ ] Cập nhật migration report, security/performance evidence, OpenAPI, runbook và completion report; chuyển phase sang `Done`.
