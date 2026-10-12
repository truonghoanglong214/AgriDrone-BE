# BE2 — Drone & AI Integration Use Case Specification và .NET Implementation Plan

> Rebaseline nghiệp vụ và kiến trúc .NET: 2026-10-12
> Nguồn nghiệp vụ cao nhất: `Codex-Plan/Be-Plan.md`
> Boundary BE1 hiện hành: `Codex-Plan/BE1-Core-Business-Implementation-Plan.md`
> Quyết định khóa: ADR-0007 đến ADR-0011 và `docs/domain/be1-step0r-locked-contract.md`
> Runtime target: một ASP.NET Core/.NET modular monolith; không có BE1 Java và không có Python/FastAPI production service.

## 1. Trạng thái và mục đích tài liệu

Tài liệu này là đặc tả use case đồng thời là implementation backlog .NET duy nhất cho phần Drone, Mission, media/telemetry, AI processing và raw-result handoff. Nó thay thế bản BE2 ngày 2026-09-25 vốn còn các giả định sai:

- BE1 là Java/Spring Boot service độc lập;
- BE1–BE2 gọi nhau qua REST như hai backend khác ngôn ngữ;
- Mapping/Plant Health V2 là target mới;
- unmapped Farm có thể tạo sẵn cả Baseline và paid Mission hoặc bắt payment trước khi chuẩn bị Baseline;
- AI production runtime là Python/FastAPI;
- AI job hoàn thành đồng nghĩa Mission hoàn thành.

Những giả định trên không còn được dùng. Source hiện tại là solution `.NET` trong `backend/AgriDrone.sln`.

Trạng thái baseline khi rebaseline:

| Capability | Trạng thái | Ghi chú |
|---|---|---|
| System-owned Drone | `Done` baseline | Đã có registry, status và availability foundation |
| Mission/media/telemetry/AI job schema | `FoundationReady` | Có code cũ nhưng chưa đủ target order-bound/purpose-aware flow |
| BE1 purpose-aware readiness | `CodeComplete — AwaitingFinalTest` | BE2 chưa nối prepare/start vào port authoritative |
| Mapping/Health V3 contracts | `ContractReady` | Contract đã có; BE2 producer và completion consumer chưa hoàn tất |
| .NET AI execution adapter/worker | `NotStarted` | Project hiện chỉ là shell, chưa có production runner |
| Target BE2 flow Phase 10–12 | `NotStarted` | Không suy `Done` từ aggregate/schema legacy |

## 2. Nguồn chuẩn và quy tắc xử lý xung đột

Thứ tự ưu tiên:

1. Nghiệp vụ trong `Be-Plan.md` và quyết định .NET-only ngày 2026-10-12.
2. ADR Accepted 0007–0011 và locked contract Step 0R.
3. `BE1-Core-Business-Implementation-Plan.md`, đặc biệt Step 4–8.
4. Tài liệu này cho ownership, implementation order và exit gate BE2.
5. Source code hiện tại chỉ là baseline kỹ thuật; code legacy không được dùng để đảo ngược nghiệp vụ đã khóa.

Khi contract cũ xung đột với V3, giữ contract cũ bất biến để drain/replay, tạo consumer/producer V3 riêng và chuyển target flow sang V3. Không sửa payload V1/V2 tại chỗ.

## 3. Kiến trúc .NET-only bắt buộc

BE2 không phải một backend riêng. BE2 là tập capability trong cùng solution:

```text
AgriDrone.Api
├── AgriDrone.Modules.Missions
│   ├── Drone registry và availability
│   ├── order-bound Mission/pre-flight/flight
│   ├── media/telemetry
│   ├── AI job/attempt/raw observation/provenance
│   └── mapping/re-identification/result handoff
├── AgriDrone.Integrations.AI
│   └── .NET AI runner/adapters và background execution
├── AgriDrone.IntegrationContracts
│   └── synchronous snapshots + versioned async contracts
├── AgriDrone.Database
│   └── EF Core migrations và cross-module transaction/read adapters
└── PostgreSQL/PostGIS + object storage + RabbitMQ Outbox/Inbox
```

Quy tắc kiến trúc:

- Tất cả production backend code viết bằng C#/.NET trong `backend/AgriDrone.sln`.
- Không tạo Java/Spring Boot, Python/FastAPI, Node.js backend hoặc database riêng cho BE2.
- AI inference chạy qua .NET abstraction/worker. Model có thể được export thành artifact tương thích với .NET runtime; công cụ nghiên cứu ngoại tuyến không trở thành production service hoặc business source of truth.
- BE1–BE2 synchronous coordination dùng C# internal contract/port tại `AgriDrone.IntegrationContracts`, không gọi HTTP loopback.
- Async handoff vẫn dùng versioned integration event, Outbox/Inbox và RabbitMQ để bảo toàn retry, replay và module decoupling.
- `Missions` không reference domain/repository nội bộ của `Surveys`, `Farms` hoặc `Plants`. Cross-module read/transaction adapter nằm ở `AgriDrone.Database` hoặc implementation được composition root đăng ký.
- Mỗi module có runtime DbContext riêng; `AgriDroneSchemaDbContext` là canonical migration owner.
- API controller chỉ map HTTP contract sang command/query; domain decision nằm trong aggregate/policy/handler.

## 4. Ownership BE1 và BE2

| Capability | Owner |
|---|---|
| SurveyRequest, onboarding, SurveyOrder, appointment, count/price/payment/adjustment | BE1/Surveys |
| Approved FarmBoundary/scope, official base map, persistent Plant identity | BE1/Farms/Plants |
| Human review, Disease Zone, recommendation selection, official SurveyResult | BE1 |
| System-owned Drone, reservation, Mission, pre-flight, flight | BE2/Missions |
| Media, telemetry, AI job/attempt, raw observation, match candidate | BE2/Missions |
| Technical model artifact/profile và inference execution | BE2 + `.NET` AI integration |
| Business disease/severity/recommendation/Harvest criteria catalogue | BE1 |
| Customer-visible published history | BE1 |
| Notification delivery | Notifications module; BE2 chỉ phát source event |

BE2 không tạo official Plant, FarmBaseMap, DiseaseZone, treatment recommendation hoặc SurveyResult. BE2 chỉ tạo raw/pending candidate có provenance và gửi sang BE1 để assigned SystemManager review/công bố.

## 5. Actor và authorization boundary

| Actor | Quyền trong BE2 |
|---|---|
| SystemAdmin | Quản lý system-owned Drone và technical AI configuration; xem job/storage health; retry/redrive theo policy |
| SystemManager | Chỉ vận hành Mission thuộc Farm/Order đang primary assignment, active, available và flight-qualified |
| TenantOwner | Không có Mission/Drone/raw-AI mutation; chỉ xem status/evidence đã được BE1 cho phép |
| Background/System Actor | Chạy worker, Inbox/Outbox, retention với execution context riêng và least privilege |

Không tin `TenantId`, `FarmId`, `SurveyOrderId`, purpose, readiness hoặc payment status do client tự khẳng định. Mọi authorization phải resolve lại từ server-side Order → Farm → current assignment.

## 6. Canonical business invariants

1. Drone là tài sản cấp hệ thống, không có `TenantId` ownership và không hard-delete khi đã có lịch sử.
2. Target Mission bắt buộc có `SurveyOrderId` và `MissionPurpose` thuộc `BaselineMapping`, `PlantHealth`, `HarvestReadiness`.
3. `MissionType.Mapping/HealthInspection` chỉ là legacy storage/conversion seam; target logic không dùng nó để suy purpose.
4. Baseline Mapping là prerequisite miễn phí trong cùng SurveyOrder, không phải Survey Service hoặc SurveyOrder riêng.
5. Follow-up không phải purpose/service mới; đó là survey lặp lại cùng service và reuse current published map/Plant IDs.
6. Readiness do BE1 quyết định theo purpose. BE2 không copy rule payment/count/price vào một policy riêng.
7. `BaselineMapping` không yêu cầu payment; paid `PlantHealth`/`HarvestReadiness` bắt buộc published map, count/price snapshot, paid appointment, confirmed exact payment và không có open adjustment.
8. `StartFlight` phải re-check authoritative state ngay trước transition. Kết quả readiness đã đọc trước đó không được cache làm authorization.
9. Mission không hoàn thành khi flight/upload/AI job hoàn thành; phải nhận đúng BE1 publication outcome.
10. Raw candidate/observation/assessment không customer-visible và không mutate official state.
11. Outside/near-boundary item thành `OutOfBoundary/NeedsReview`; unresolved item không được auto-assign Farm/Plant, tính count hoặc tham gia official result.
12. Repeat survey re-identify vào current published map; không rebuild map, không tự retire Plant bị missing và không tự tạo Plant từ `Unmatched`.
13. AI chỉ đề xuất Disease Zone và recommendation candidate/reference. Official recommendation phải là version từ expert-validated catalogue do manager chọn/xác nhận ở BE1; không có free-form treatment advice.
14. Harvest Readiness không dùng Harvest batch/yield/quantity/quality-grade legacy và chỉ được ghi `Validated` khi criteria/evaluation gate của BE1 đã đạt.
15. Mọi mutation dùng expected version/idempotency; mọi output truy nguyên được Order → Mission → Job/Attempt → Model/Threshold/Criteria → Media/Frame/Telemetry.

## 7. Hai luồng nghiệp vụ chuẩn

### 7.1. First survey — Farm chưa có base map

```text
BE1 verify boundary/scope
→ confirm Baseline appointment
→ BE2 prepare duy nhất BaselineMapping Mission
→ complete pre-flight
→ atomic start re-check Baseline readiness (không payment)
→ flight/upload/.NET AI mapping
→ BE2 phát BaselineMappingCandidatesReadyV3
→ BE1 manager review + publish FarmBaseMapPublishedV3 + confirmed active-pole count
→ BE1 snapshot PricePerPole/FinalPrice
→ confirm PaidService appointment
→ confirm payment
→ BE2 prepare selected paid Mission
→ atomic start re-check paid readiness
→ flight/upload/.NET AI service analysis
→ BE2 phát PlantHealthAnalysisReadyV3 hoặc HarvestReadinessAssessmentsReadyV2
→ BE1 human review + official publication
→ BE2 nhận publication outcome và complete Mission
```

Không tạo/reserve paid Mission trước khi initial map/count/pricing/appointment/payment đã hoàn tất. Không yêu cầu payment để prepare/start Baseline Mission.

### 7.2. Farm đã có current published base map

```text
BE1 verify boundary/scope và current inventory
→ manager confirm count/price snapshot cho Order
→ confirm PaidService appointment
→ confirm payment
→ BE2 prepare duy nhất Mission đúng selected service
→ start re-check readiness
→ flight/upload/process/re-identify
→ BE1 review/publish
→ Mission complete sau publication outcome
```

## 8. State machines mục tiêu

### 8.1. Mission

```text
Draft
→ Scheduled
→ InFlight
→ FlightCompleted
→ Uploading
→ ReadyForProcessing
→ Processing
→ AwaitingReview
→ Completed
```

Failure branches giữ riêng `FlightFailed`, `UploadFailed`, `ProcessingFailed`. `Cancelled` chỉ áp dụng trước khi flight bắt đầu. Retry tạo attempt/history mới phù hợp, không viết lại lịch sử thất bại.

### 8.2. AI job

```text
Queued → Running → Completed
                 ↘ Failed
Queued/Running → Cancelled
```

Retry/reprocess tạo `Attempt` hoặc output version mới. Callback/result cùng identity phải idempotent; result cũ/out-of-order không lùi state.

### 8.3. Mission completion theo purpose

- Baseline chỉ complete khi nhận `FarmBaseMapPublishedV3` khớp Mission/Order/Farm/handoff/version.
- Plant Health chỉ complete khi nhận official published-result outcome khớp source `PlantHealthAnalysisReadyV3`.
- Harvest Readiness chỉ complete khi nhận official published-result outcome đúng criteria/granularity/version.
- Rejected/correction-required outcome giữ Mission ở `AwaitingReview` và mở work item/reprocess flow; không giả thành Completed.

## 9. Contract BE1–BE2 mục tiêu

### 9.1. Synchronous internal contracts

Tạo `SurveyOrderOperationalContextV3` và query port mới; không sửa `SurveyOrderOperationalContextV2` tại chỗ. V3 tối thiểu mang:

- Order/Tenant/Farm/service/purpose và Order version/status;
- current approved FarmBoundary/scope version;
- `RequiresBaselineMapping` và current published FarmBaseMap version;
- confirmed pole count và immutable price snapshot khi đã có;
- purpose-specific appointment;
- payment summary chỉ cho paid purpose;
- current primary SystemManager assignment summary;
- previous compatible published order;
- authoritative `IsReady`, stable failure codes và `EvaluatedAt`.

Preparation có thể đọc context để tạo Draft Mission nhưng không được coi `IsReady=false` do chưa có Mission/pre-flight là lỗi business cuối cùng. `StartFlight` luôn gọi mission-specific readiness bằng MissionId và bắt buộc `IsReady=true`.

### 9.2. Async target contracts

| Direction | Target contract | Ghi chú |
|---|---|---|
| BE2 → BE1 | `BaselineMappingCandidatesReadyV3` | Pending candidates, boundary classification, source provenance |
| BE1 → BE2 | `FarmBaseMapPublishedV3` | Published map, Plant mapping và confirmed active count |
| BE2 → BE1 | `PlantHealthAnalysisReadyV3` | Findings, boundary exceptions, proposed Disease Zones, recommendation candidates |
| BE2 → BE1 | `HarvestReadinessAssessmentsReadyV2` | Giữ V2 vì chưa có ADR khóa V3 cho capability này |
| BE1 → BE2 | `SurveyResultReviewStateChangedV2` hoặc successor đã khóa | Completion/review outcome; không dùng AI-job status thay thế |

V1/V2 Mapping/Health chỉ để compatibility/drain. Target code mới không phát `BaselineMappingCandidatesApprovedV2` hoặc `HealthObservationsReadyV2` thay cho V3.

### 9.3. Atomic start boundary

Một read-only readiness endpoint không đủ chống race. Tạo `ISurveyMissionStartCoordinator` trong Missions application abstraction và implementation cross-module tại `AgriDrone.Database`:

1. khóa Mission, SurveyOrder, current appointment, payment/adjustment, assignment, Drone và checklist cần thiết;
2. build snapshot bằng cùng readiness projection/policy của BE1, không copy rule;
3. kiểm tra actor, expected Mission/Order version, purpose và schedule;
4. transition `SurveyOrder` sang `BaselineInProgress` hoặc `InProgress` và Mission sang `InFlight`;
5. ghi audit/outbox;
6. commit một lần.

Nếu chưa thể dùng một transaction vật lý cho cả hai aggregate, phase implementation phải định nghĩa reservation/claim + compensation rõ ràng và chứng minh không có cửa sổ start sau khi gate bị revoke. Không chấp nhận “query rồi save Mission sau” không có concurrency guard.

## 10. Thứ tự triển khai .NET

Từ mục này, “Step” nghĩa là BE2 Step; số Step không thay thế số Step tương ứng trong BE1 plan.

| Step | Nội dung | Dependency | Trạng thái |
|---:|---|---|---|
| 0 | Rebaseline .NET boundary, V3 context, legacy inventory | BE1 Step 4 | `NotStarted` |
| 1 | Order-bound Mission preparation và atomic start | Step 0 + BE1 Step 5C | `NotStarted` |
| 2 | Pre-flight, flight, media, telemetry và offline sync | Step 1 | `FoundationReady` |
| 3 | .NET AI job runner, attempt và provenance | Step 2 | `FoundationReady` |
| 4 | Baseline mapping pipeline + V3 handoff | Step 3 | `NotStarted` |
| 5 | Plant re-identification + boundary guardrail | Step 4/current map | `FoundationReady` |
| 6 | Plant Health V3 pipeline | Step 5 | `NotStarted` |
| 7 | Harvest Readiness V2 pipeline | Step 5 + criteria gate | `NotStarted` |
| 8 | Publication outcome và Mission completion | Step 4/6/7 + BE1 Step 6–8 | `NotStarted` |
| 9 | Monitoring, retry, retention và compatibility drain | Step 3–8 | `NotStarted` |
| 10 | Final tests, E2E và release gate | Step 0–9 | `NotStarted` |

Không đánh dấu `Done` chỉ vì file/entity/table đã tồn tại hoặc solution build pass.

### 10.1. Use-case crosswalk sau rebaseline

| Use case cũ | Ownership/Step mới |
|---|---|
| BE2-UC00 Integration foundation | BE2 Step 0 |
| BE2-UC01 System-owned Drone | Giữ baseline `Done`; hardening ở Step 1/9 |
| BE2-UC02 Mission set planning | Tách thành prepare Baseline và prepare paid Mission ở Step 1 |
| BE2-UC03 Pre-flight/flight | Step 1–2, start qua atomic coordinator |
| BE2-UC04 Media/telemetry/field sync | Step 2 |
| BE2-UC05 AI jobs | Step 3, chạy bằng .NET worker |
| BE2-UC06 Baseline mapping | Step 4, phát candidates V3 |
| BE2-UC07 Re-identification | Step 5 |
| BE2-UC08 Mapping review | Review/publication chuyển đúng về BE1; BE2 chỉ handoff và completion coordination ở Step 4/8 |
| BE2-UC09 Plant Health | Step 6, phát V3; review/publication thuộc BE1 |
| BE2-UC10 Harvest Readiness | Step 7, phát V2 hiện hành; review/publication thuộc BE1 |
| BE2-UC11 Publication coordination | Step 8 |
| BE2-UC12 Notification delivery | Chuyển về Notifications module; BE2 chỉ phát source event |
| BE2-UC13 Monitoring/retry/retention | Step 9 |

## 11. Step 0 — Rebaseline boundary và foundation audit

### 11.1. Việc phải làm

- Thêm `SurveyOrderOperationalContextV3` và port mới trong `AgriDrone.IntegrationContracts/Surveys/`; giữ V2 immutable.
- Implement adapter BE1 context/readiness trong `AgriDrone.Database/Surveys/` và đăng ký DI.
- Chuyển target command từ `MissionType` sang `MissionPurpose`; legacy mapping chỉ phục vụ archive/drain.
- Audit mọi controller/handler cũ có thể tạo Mission không Order, tenant-owned Drone hoặc generic transition; đóng khỏi target API.
- Chốt stable mission/readiness/error codes và response semantics.
- Xác nhận V3 Mapping/Health descriptors/routing/validators; Harvest V2 vẫn là target hiện hành.
- Chốt `.NET` AI execution port; đổi tên `IAiJobClient` nếu semantics không còn là remote HTTP client.
- Thêm architecture tests để cấm Missions reference Surveys/Farms/Plants implementation và cấm production Python/Java backend.

### 11.2. Exit gate

- V3 context compile và được Missions consume qua port.
- Không còn tài liệu/code target nói BE1 Java hoặc Python/FastAPI production runtime.
- Legacy V1/V2 route/event có inventory và drain owner, không bị xóa tùy tiện.

## 12. Step 1 — Order-bound Mission preparation và atomic start

### 12.1. Features riêng

- `PrepareBaselineMappingMission`
- `PreparePaidServiceMission`
- `ScheduleSurveyMission`
- `CompleteMissionPreflight`
- `StartSurveyMissionFlight`
- `CancelPreparedMission`

Mỗi feature có command/query, validator, handler và response riêng; không dùng một generic `TransitionMission` public endpoint cho target flow.

### 12.2. Preparation rules

Baseline preparation:

- chỉ khi Order cần baseline, boundary/scope đã verified và Baseline appointment hiện hành đã confirmed;
- chỉ tạo `BaselineMapping` Mission;
- chưa yêu cầu count/price/payment;
- retry/concurrent prepare chỉ có một active Mission cùng Order/purpose.

Paid preparation:

- chỉ sau current map, count/price snapshot, PaidService appointment và payment đã đạt commercial gate;
- purpose derive từ selected Survey Service, client không gửi tùy ý;
- Plant Health và Harvest Readiness không dùng chung purpose/schema;
- unmapped Order chưa publish initial map không được prepare paid Mission.

### 12.3. Scheduling và pre-flight

- Drone query/reservation là global, chống double booking khác Farm/Tenant.
- Mission schedule phải nằm trong purpose-specific appointment window.
- Checklist lưu definition/version và immutable answers, unsuitable-flight/failsafe notes, actor/time.
- Checklist hoặc Drone/assignment/schedule thay đổi trọng yếu làm start re-check fail hoặc checklist stale theo policy.

### 12.4. Start rules

- handler không tin readiness từ UI;
- atomic coordinator re-check authoritative state và expected versions;
- Baseline start chuyển Order `BaselineReady → BaselineInProgress`;
- paid start chuyển `ReadyForPaidService → InProgress`;
- direct handler invocation, retry hoặc concurrent start không bypass gate và không tạo hai flight.

## 13. Step 2 — Flight, media, telemetry và field sync

- Giữ state machine flight rõ ràng; interrupted/failed không trở thành Completed.
- Upload session hỗ trợ multipart/resume, checksum, MIME/size và idempotency.
- Binary lớn đi trực tiếp object storage khi adapter hỗ trợ; API chỉ cấp session/finalize.
- Telemetry dùng UTC, WGS84, liên kết Mission/time range và giữ raw import provenance.
- Chỉ khi required media/telemetry integrity pass mới chuyển `ReadyForProcessing`.
- Offline support chỉ cho checklist, field notes và upload intents; không hồi sinh generic FieldTask/Harvest sync.
- Authorization derive Mission → Order → Farm → current assignment.

Exit gate gồm weak-network retry, corrupt/incomplete object, telemetry mismatch và cross-Farm access.

## 14. Step 3 — .NET AI processing runtime

### 14.1. Target components

Trong `AgriDrone.Integrations.AI` tạo tối thiểu:

- DI extension để composition root đăng ký integration;
- `IAiProcessingEngine`/runner implementation bằng .NET;
- background worker nhận queued job;
- model artifact loader/cache có checksum/version;
- task adapter riêng cho Baseline Mapping, Plant Health và Harvest Readiness;
- typed result serializer/validator;
- health check, timeout, cancellation và bounded concurrency options.

`AgriDrone.Api` reference/register project integration. Không chạy inference đồng bộ trong HTTP request.

### 14.2. Job rules

- Khi Mission `ReadyForProcessing`, tạo job/attempt và snapshot model, threshold, algorithm, business criteria reference, input object checksums.
- Technical model/threshold do BE2 quản lý; disease/recommendation/Harvest criteria do BE1 cung cấp dưới version/reference read-only.
- Worker claim job bằng lease/concurrency token; crash/timeout không tạo duplicate final output.
- Retry tạo attempt mới; reprocess tạo output version mới; raw result cũ bất biến.
- Chỉ typed/validated ingestion thành công mới đưa Mission sang `AwaitingReview`.
- Không log media secret, signed URL, raw PII hoặc model secret.

## 15. Step 4 — Baseline Mapping và V3 producer

### 15.1. Processing

- Parse pole detections/tracks với frame/GPS/telemetry references.
- De-duplicate physical pole qua overlapping frames trước khi tính count.
- Geolocate và derive row/column theo versioned algorithm/policy.
- Classify boundary bằng approved FarmBoundary version; lưu distance, threshold và policy version.
- `OutOfBoundary/NeedsReview` giữ pending; không tự sửa tọa độ hoặc gán Farm.
- Candidate không phải Plant và detected count không phải confirmed count.

### 15.2. Handoff

- Phát `BaselineMappingCandidatesReadyV3`, không phát tên “Approved” cho raw candidate.
- Payload chứa Order/Mission/Farm/boundary version, handoff/job/model/policy provenance, Zone batches và candidate evidence.
- Enforce giới hạn 10.000 item/4 MiB từ ADR; vượt giới hạn phải split scope/job trước khi publish.
- Outbox cùng transaction với immutable handoff record; retry giữ cùng identity.
- Sau handoff, Mission `AwaitingReview`; chỉ `FarmBaseMapPublishedV3` mới complete Baseline Mission.

BE1 mới là nơi manager review candidate, publish map/Plants và xác nhận count.

## 16. Step 5 — Plant re-identification

- Đọc current published base-map/Plant reference snapshot đúng version từ BE1.
- Matching dùng GPS distance + row/column/grid structure + boundary guardrail; không nearest-GPS-only.
- Lưu strategy/version/parameters, score components và ranked candidates.
- Kết quả là `Matched`, `Ambiguous`, `Unmatched` hoặc boundary exception.
- Một Plant không resolve cho nhiều observation trong cùng pass sau dedup.
- `Ambiguous/Unmatched` không được gắn Plant giả, tạo Plant mới hoặc retire Plant cũ.
- Manual resolution giữ actor/reason/evidence/original candidates và phục vụ RQ1 metrics.
- Wrong/stale map version fail/re-run rõ; không silently match vào map khác.

## 17. Step 6 — Plant Health V3

- Parse selected disease, Healthy, sunburn/mechanical damage, severity và confidence theo versioned schema.
- Re-identify observation vào current Plant reference; unresolved item giữ pending.
- Sinh proposed Disease Zones dạng Polygon SRID 4326 cùng membership/provenance; nhiều vùng rời nhau là nhiều zone.
- Sinh recommendation candidate/reference từ expert catalogue snapshot; không sinh official free-form treatment.
- Giữ evidence media/frame, observed time, model/threshold/algorithm và base-map/boundary version.
- Phát `PlantHealthAnalysisReadyV3` qua Outbox.
- BE1 import/review/correct/approve/publish; BE2 không cập nhật current Plant health.
- Reprocess tạo handoff/output version mới và supersedes relation, không overwrite.

## 18. Step 7 — Harvest Readiness V2

- Chỉ implement capability theo criteria version, indicators, granularity và status do BE1 cung cấp.
- Nếu criteria chưa đạt validation gate, output phải ghi `Experimental`; không tự claim `Validated`.
- Granularity có thể Farm/Zone/Plant; không ép `PlantId` nếu domain definition không yêu cầu.
- Lưu visible indicators, confidence, evidence và model/threshold/criteria provenance.
- Không reference HarvestBatch, PlantHarvestRecord, HarvestQualityGrade, yield hoặc quantity.
- Phát `HarvestReadinessAssessmentsReadyV2` idempotently và chờ BE1 human review/publication.
- Previous comparison chỉ là published Harvest Readiness result compatible, không dùng Plant Health.

## 19. Step 8 — Publication coordination và Mission completion

- Consumer `FarmBaseMapPublishedV3` validate envelope, Order/Farm/source Mission/handoff/version/count trước khi apply.
- Consumer official result state validate purpose và source handoff/output version.
- Inbox claim/commit trước ACK; duplicate là idempotent success, mismatch là permanent/audit, transient failure retry.
- Event đến sớm được giữ/retry, không bỏ qua.
- Correction/rejected state không complete Mission.
- Mission complete không tự quyết SurveyOrder complete; BE1 publication transaction là source of truth cho Order.
- Completion event có thể phát notification source event, nhưng delivery thuộc Notifications module.

## 20. Step 9 — Monitoring, retry, retention và drain

- Metrics: job queue/running/failed duration, attempt count, inference duration, handoff lag, review lag, Inbox/Outbox retry/DLQ, storage integrity.
- Alert: stuck job, lease expiry, repeated result failure, V3 consumer error, storage orphan và completion mismatch.
- Retry/redrive yêu cầu actor/reason/audit và không sửa raw history.
- Retention dry-run phải bảo vệ media/evidence đang được map/result/profile hoặc investigation tham chiếu.
- Cleanup object/database dùng recoverable state, không để một bên báo deleted khi bên kia còn active reference.
- V1/V2 Mapping/Health chỉ retire sau producer cutover, queue/DLQ drain, retention window, replay test và metric bằng zero theo policy.

## 21. Target .NET file manifest

Tên có thể điều chỉnh theo convention repo nhưng không đổi ownership:

| Step | File/folder mục tiêu |
|---|---|
| 0 | `IntegrationContracts/Surveys/SurveyOrderOperationalContextV3.cs` |
| 0 | `Database/Surveys/SurveyOrderOperationalContextV3Query.cs` |
| 1 | `Missions/Application/Features/Missions/PrepareBaselineMappingMission/` |
| 1 | `Missions/Application/Features/Missions/PreparePaidServiceMission/` |
| 1 | `Missions/Application/Features/Missions/CompleteMissionPreflight/` |
| 1 | `Missions/Application/Features/Missions/StartSurveyMissionFlight/` |
| 1 | `Missions/Application/Abstractions/Missions/ISurveyMissionStartCoordinator.cs` |
| 1 | `Database/Missions/SurveyMissionStartCoordinator.cs` |
| 2 | Giữ/mở rộng `Missions/Application/Features/Media/` và `Telemetry/` |
| 3 | `Integrations/AgriDrone.Integrations.AI/DependencyInjection.cs` |
| 3 | `Integrations/AgriDrone.Integrations.AI/Processing/DotNetAiProcessingEngine.cs` |
| 3 | `Integrations/AgriDrone.Integrations.AI/Processing/AiProcessingWorker.cs` |
| 4 | `Missions/Application/Features/Mapping/ProcessBaselineMappingOutput/` |
| 4 | `Missions/Infrastructure/Messaging/BaselineMappingCandidatesReadyV3OutboxFactory.cs` |
| 5 | `Missions/Application/Features/Observations/ResolvePlantObservations/` |
| 6 | `Missions/Application/Features/Health/ProcessPlantHealthOutput/` |
| 6 | `Missions/Infrastructure/Messaging/PlantHealthAnalysisReadyV3OutboxFactory.cs` |
| 7 | `Missions/Application/Features/HarvestReadiness/ProcessHarvestReadinessOutput/` |
| 8 | `Missions/Infrastructure/Integration/FarmBaseMapPublishedV3Processor.cs` |
| 8 | `Missions/Infrastructure/Integration/SurveyResultReviewStateChangedV2Processor.cs` |

Không tạo generic `Handler.cs`; dùng đầy đủ tên feature. Trước khi tạo file phải đối chiếu source để sửa/reuse foundation hiện có, tránh duplicate aggregate/repository/DbContext.

## 22. API intent

Target manager routes:

```text
POST /api/system-manager/survey-orders/{orderId}/missions/baseline-mapping/prepare
POST /api/system-manager/survey-orders/{orderId}/missions/paid-service/prepare
POST /api/system-manager/missions/{missionId}/schedule
POST /api/system-manager/missions/{missionId}/preflight/complete
POST /api/system-manager/missions/{missionId}/start-flight
POST /api/system-manager/missions/{missionId}/complete-flight
POST /api/system-manager/missions/{missionId}/fail-flight
POST /api/system-manager/missions/{missionId}/upload-sessions
POST /api/system-manager/missions/{missionId}/uploads/finalize
GET  /api/system-manager/missions/{missionId}/processing
```

SystemAdmin routes tách riêng cho Drone registry, technical AI configuration và authorized retry/redrive. Không có TenantOwner mission mutation route, generic public transition route hoặc client command mark-ready/complete.

## 23. Migration strategy

1. Inventory legacy nullable `SurveyOrderId`, `MissionType`, Zone-only scope và V1 completion fields.
2. Expand target columns/indexes trước; không sửa migration lịch sử.
3. Backfill chỉ khi mapping deterministic; ambiguous legacy row giữ archive/non-target state và report.
4. Thêm unique partial index cho một active Mission mỗi `(SurveyOrderId, MissionPurpose)` theo policy.
5. Thêm optimistic version/check constraints cho mission, job attempt, handoff và completion identity.
6. Contract phase chỉ sau khi target API/consumer đã cut over và replay/retention gate pass.
7. Fresh/upgrade/downgrade SQL và `has-pending-model-changes` là release evidence bắt buộc.

## 24. Final Test Backlog

Không đánh dấu `Done` trước khi có ít nhất:

- unit test state transitions Mission/job và purpose mapping;
- architecture test .NET-only, module dependency và no legacy public mutation;
- authorization test assigned/unassigned/expired qualification/wrong Farm/TenantOwner;
- PostgreSQL concurrency test duplicate prepare, Drone double booking và concurrent start;
- atomic start test gate bị revoke giữa prepare/start;
- Baseline readiness pass không payment và paid readiness fail khi thiếu/mismatch/refund/open adjustment;
- upload resume/checksum/corrupt object/telemetry mismatch tests;
- worker lease/timeout/cancellation/retry/reprocess/idempotency tests;
- Mapping V3 và Health V3 golden JSON/routing/replay/DLQ tests;
- boundary exception, duplicate track, stale map và ambiguous match tests;
- Disease Zone/recommendation provenance tests;
- Harvest criteria/granularity/Experimental tests và architecture test cấm Harvest legacy;
- completion out-of-order/duplicate/wrong-purpose/wrong-version tests;
- fresh/upgrade migration tests và retention dry-run;
- E2E first unmapped Farm, mapped Farm, repeat Plant Health và repeat Harvest Readiness.

## 25. E2E acceptance bắt buộc

### E2E-01 — First unmapped Farm

Baseline Mission chạy trước payment, V3 candidates được BE1 review/publish, confirmed count tạo pricing snapshot, payment sau đó mới mở paid Mission, official result publication mới complete flow.

### E2E-02 — Existing mapped Farm

Không tạo Baseline Mission; reuse current map/Plant IDs, xác nhận count/price cho Order rồi mới paid appointment/payment/Mission.

### E2E-03 — Repeat Plant Health

Không rebuild map; re-identification đúng version, proposed Disease Zones/recommendations vẫn pending cho tới BE1 publication.

### E2E-04 — Repeat Harvest Readiness

Chỉ dùng compatible Harvest criteria/result; không đọc Plant Health hoặc Harvest legacy.

### E2E-05 — Revocation/replay/security

Appointment/payment/assignment/Drone/checklist thay đổi trước start đóng gate; duplicate command/event không nhân đôi; wrong order/farm/version bị reject; raw AI/media nội bộ không lộ TenantOwner.

## 26. Definition of Done BE2

BE2 chỉ đạt `Done` khi:

1. Toàn bộ target production runtime nằm trong solution .NET và composition root đăng ký đầy đủ.
2. First/unmapped flow thực sự chạy Baseline trước payment rồi paid service sau payment.
3. Start flight dùng authoritative BE1 readiness và atomic/concurrency-safe transition, không có TOCTOU bypass.
4. Mapping/Health target dùng V3; V1/V2 compatibility có drain evidence.
5. Raw AI không tạo official state; human review/publication của BE1 là gate.
6. Mission completion phụ thuộc đúng publication outcome theo purpose/version.
7. Fresh/upgrade PostgreSQL migration, OpenAPI, golden contract và toàn bộ test backlog xanh.
8. Metrics, audit, retry/DLQ, retention và recovery runbook đủ để vận hành.

## 27. Công việc nên bắt đầu ngay

1. Step 0: tạo `SurveyOrderOperationalContextV3`/adapter và loại giả định Java/REST/V2 target khỏi runtime path.
2. Step 1: thay public generic Create/Transition flow bằng `PrepareBaselineMappingMission`, `PreparePaidServiceMission` và atomic `StartSurveyMissionFlight`.
3. Step 3: biến `AgriDrone.Integrations.AI` từ project shell thành .NET worker/adapter có DI, health check và typed runner.
4. Sau đó mới nối Baseline Mapping V3 producer; không bắt đầu Plant Health/Harvest pipeline trước khi current map/count handoff chạy đúng.
