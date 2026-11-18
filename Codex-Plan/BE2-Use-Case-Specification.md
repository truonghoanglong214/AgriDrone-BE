# BE2 — Drone, Processing và Delivery Use Cases theo v3

Specification approved **2026-11-10**. Đây là đặc tả mục tiêu; đối chiếu [implementation plan](BE1-Core-Business-Implementation-Plan.md) và checklist từng phase để biết trạng thái runtime. Sources: [Business Rules](Business-Rules-v3.md), [ADR 0018](../docs/adr/0018-v3-paid-map-combo-business-policy.md), [BE1 use cases](BE1-Use-Case-Specification.md).

## 1. Architecture và ownership

```text
Web / online Manager Mobile → ASP.NET Core modular monolith
    Surveys/Farms/Plants: BE1 transactional policy
    Missions/SePay adapters/pending consumers/portal/notifications: BE2 capability
        → authenticated versioned processing API → FastAPI imagery/AI workers
        ← immutable artifacts/candidates/job provenance
    PostgreSQL/PostGIS + object storage + RabbitMQ Outbox/Inbox
```

- .NET giữ business truth, authentication/authorization, payments/claims/refunds và official identities/publications. Không có Java/Spring Boot migration trong backlog.
- FastAPI không ghi trực tiếp tables nghiệp vụ hoặc tự complete Order/create Plant; .NET orchestration chịu trách nhiệm job receipt, reconciliation, persisted source context và review handoff.
- Internal .NET coordination qua typed ports, không HTTP loopback; cross-module DB transaction/read adapters có boundary rõ. FastAPI network boundary không nằm trong SQL transaction nghiệp vụ kéo dài.
- Gateway plumbing tái dùng existing Surveys + `AgriDrone.Integrations.SePay`; không giả định `AgriDrone.Modules.Payments` đã có hoặc bắt buộc thêm project mới.
- Contract proposal v3 không đổi nghĩa payload Mapping V3/Health V4 đang phát hành. First Combo context/service khác phải contract-review/version mới trước producer cutover.

## 2. Catalogue use cases

Giữ UC00–UC09 làm logical crosswalk, không coi số step của bản trước là trạng thái hoàn thành.

| ID | Capability | Policy dependency |
|---|---|---|
| BE2-UC00 | Drone registry, availability, maintenance | System-owned; authorized Admin |
| BE2-UC01 | Order-bound preparation, scheduling, preflight/start | Deposit + appointment + safe authorized capture |
| BE2-UC02 | Flight, GNSS/imagery, uploads và online notes/checklists | Assignment/context immutable, resumable retry |
| BE2-UC03 | Processing job orchestration/FastAPI adapters | .NET owns job identity/provenance |
| BE2-UC04 | First/Follow-up mapping candidate handoff | MAP và COMBO đều mapping; Baseline vs snapshot tách riêng |
| BE2-UC05 | GNSS+grid re-identification/change candidates | Stable IDs; uncertain/new/missing pending review |
| BE2-UC06 | Combo suspected disease/zone/guidance candidates | Combo only, no severity/diagnosis |
| BE2-UC07 | Harvest Readiness | Excluded, không MVP implementation |
| BE2-UC08 | Release proof → Mission outcome/portal/notification projections | UC13 BE1 owns release transaction |
| BE2-UC09 | Retry, DLQ, retention, observability | Preserve referenced evidence, audit redrive |
| BE2-UC10 | SePay checkout/webhook transport | BE1 owns allocation, invoice, due/refund policy |
| BE2-UC11 | Released Owner portal/timeline/comparison/media | Ownership + paid release + not withdrawn |
| BE2-UC12 | Notification, export và bank-proof storage adapters | Durable notice canonical, no async reset/window |

## 3. First và Follow-up flows

### First MAP

1. Order snapshot MAP price/shared deposit, Owner confirms appointment and pays deposit.
2. Server prepares required capture Missions, selects available drone, preflight và safety/authorization checks. Không yêu cầu customer-published Baseline hoặc final balance.
3. Capture imagery/GNSS; upload finalize chỉ hoàn tất upload, chưa review/publish/result unlock.
4. FastAPI photogrammetry/detection trả immutable outputs. Manager mark/review boundary, map/poles và confirmed billable count.
5. BE1 approves internal Baseline/profile result và invoice; balance settlement triggers atomic release. BE2 exposes result only after persisted release proof.

### First COMBO

Các bước capture/mapping như MAP; approved internal mapping cung cấp stable identity/context cho disease processing trên imagery của Order. Manager review suspected findings/zones/guidance trước final approval. Không yêu cầu bay một paid PlantHealth Mission sau khi đã trả đủ balance, không bắt customer-visible Baseline trước disease processing.

### Follow-up MAP hoặc COMBO

Order mới tham chiếu released Baseline/version và snapshot giá/cọc mới. Deposit vẫn bắt buộc. Mapping observations match profile cũ, giữ own survey map snapshot. Combo thêm disease pipeline; MAP trả NotAssessed cho disease, không clear lịch sử. Store riêng Baseline reference/prior mapping/prior compatible Combo basis. Follow-up completion không supersede Baseline.

### Quality Re-flight

Mission/capture attempt mới cùng Order, link predecessor/reason/approval, không checkout phí/cọc mới. Retain all originals/GNSS/logs. Reservation/preflight/safety/authorization vẫn recheck. Đội không bàn giao được và dừng đơn thì refund đủ theo BE1 policy.

## 4. Operational readiness và atomic start

Capture readiness gồm approved Order trong state cho phép, confirmed appointment/window, assigned active/available Manager, confirmed deposit, authorized/safe operation, available drone/reservation và completed preflight. Follow-up cần released Baseline; first capture không cần boundary/route artifact mà chính capture này mới sinh ra.

Balance, finalized postflight count và disease publication không là prerequisites cho first capture. Route planning vẫn áp dụng khi cần, nhưng không được đặt circular dependency “đã chụp orthophoto mới được bắt đầu chuyến chụp orthophoto”. Technical route policy và capture area phải dựa dữ liệu có sẵn, Manager safety decision và drone capabilities.

Start boundary khóa/rechecks Order/appointment/deposit/assignment/drone reservation/Mission expected version, commit Mission start/consumption receipt/audit/Outbox atomic. Không “read readiness rồi save start” có cửa sổ revoke. Cross-module coordinator dùng typed context và transaction adapter; không gọi domain repository nội bộ chéo module tùy ý.

Acceptance: deposit missing/appointment stale/wrong assignment/drone conflict block; unpaid balance không block capture; cancelled/rescheduled context cannot start; duplicate start/re-flight consume idempotent; no fake flight permission evidence backfill.

## 5. Media, GNSS và online field workflow

- Online-only mobile hiển thị checklist/notes, nhận versioned updates và tạo upload intents. Không offline task cache, deferred mutation sync hoặc generic FieldTask/Harvest runtime.
- Interrupted upload retry/resume bằng same upload/operation identity; checksum và finalized source manifest ngăn partial/corrupt input processing.
- Capture time/GNSS coordinate system, imagery metadata, source Mission/Order/Tenant/Farm và storage object references bất biến. Corrected geometry là review value khác original.
- Owner upload claim evidence và Manager raw drone imagery có visibility khác nhau; không public bucket/long-lived URL bypass release. Object access authorization kiểm tra trước presigned URL.
- Revoked assignment không cấp token mới; token TTL/withdrawal/revocation/cache policy phải có acceptance. Không tuyên bố revoke URL đã ký có hiệu lực tức thì nếu storage chưa hỗ trợ.

## 6. Processing orchestration và source contracts

1. .NET ghi job identity, immutable source manifest/config/model/dataset/annotation snapshots và queue intent.
2. Adapter gửi FastAPI request có request/job IDs, correlation và input checksum, authenticated service access. Long job chạy background; polling/callback authenticated có dedup, timeout và reconciliation.
3. Retry cùng job không tạo source revision giả; reprocess hợp lệ có new source run revision/supersedes.
4. .NET verify artifact hash/ownership/context trước pending handoff. FastAPI success không đồng nghĩa approved/paid/released.
5. Pending import transaction atomically ghi candidates/import receipt/Inbox/audit; không overwrite reviewed published result bằng late output.

Mapping chạy cho cả MAP/COMBO; disease chạy Combo only. Match suggestions không được biến trực tiếp thành official lifecycle. New/missing/uncertain/boundary candidates chờ Manager. First approved internal map và Follow-up Baseline/survey map references phải phân biệt trong target context, không nhồi vào legacy `PublishedFarmBaseMap` field bằng thông tin giả.

Acceptance: failed/duplicate/out-of-order job handoff, tampered artifact, cross-tenant media và stale source revision fail; crash/retry preserve provenance; no auto-create/retire/profile or Healthy fallback.

## 7. Gateway transport

SePay adapter tạo QR/reference từ server-side payment obligation và xác thực webhook theo cơ chế adapter thực tế; không mô tả mọi provider đều HMAC. Provider transaction/event dedup trước business credit; unauthenticated/mismatched input không mở capture/release.

BE1 resolve amount, allocations, overpayment reservation và financial policies. BE2 transport không tự suy `FullPaid` từ một webhook. Missing reference/currency/amount mismatches vào reconciliation; late callback từ stale balance checkout không bị bỏ mất receipts thật.

Refund MVP là Admin-recorded manual bank transfer, không giả định SePay auto-refund API. Proof storage/audit và reconciliation hỗ trợ BR-RFD-01–05; không tự retry chuyển tiền khi chưa biết transfer trước đã thành công hay chưa.

## 8. Release output, portal và notification

- BE1 commit atomic publication/unlock/notice/window/service value/Order completion; BE2 chỉ project từ persisted release receipt có exact revision/hash.
- Current runtime PlantHealth manual publish/Completed event không tự đáp ứng target policy; giữ compatibility consumers, thiết kế event revision mới nếu semantics khác.
- Mission capture/processing milestones được lưu độc lập với commercial Order completion. Không bắt giữ flight InFlight trong lúc khách trả balance; Mission terminal outcome nếu phụ thuộc publication phải có receipt rõ.
- Owner query áp ownership và release/withdrawal predicate trong database/read model. Timeline/profile của đơn còn locked không lộ qua “latest reviewed health”, per-pole/zone/disease aggregates, tile/report hoặc media endpoint; verified billable count/rate/Total/Due trong invoice là billing metadata được phép trước release.
- Old released history vẫn xem được khi Order mới chưa release. New mapping/disease observations chưa release không cập nhật Owner-visible latest projection.
- In-app notice đã durable tại release, async consumer chỉ delivery/project; notification retry không cấp claim window mới. Email/push thất bại không làm rollback payment hoặc đổi publication date.

## 9. Revenue và Excel adapters

BE1 owns BR-FIN formulas; BE2 implements shared queries, Admin-only export, cutoff UTC+7, formula version/audit và safe text cells. Không dùng flight date/current status hoặc total của corrected revision làm sale mới. Refund classes tách overpayment/cancellation cash returns khỏi service-value refunds.

Acceptance có cross-month deposit/balance/refund/publication, deposit held/unpriced/pending/retained groups, outstanding as-of history và Dashboard=Excel.

## 10. Retry, evidence retention và compatibility

- Outbox producer transaction, Inbox business transaction, ACK sau commit, publisher confirm, bounded retry/DLQ và business idempotency cùng message dedup.
- V1/V2/V3/V4 đã phát hành immutable. New policy không phát service enum/context khác qua old schema. Legacy drain/replay giữ wire facts; cutover không chạy competing shadow consumers trên production queue.
- Evidence retention bảo vệ media/GNSS/AI originals/report versions được claim/correction/Baseline/profile tham chiếu; no destructive cleanup dựa solely current map.
- Observability: deposit/capture gate failures, job/review/release lag, unlock leaks, duplicate release attempts, refund reservation/cap failures, reconciliation differences và export mismatches.

## 11. Thứ tự triển khai và DoD

1. V3 context/contracts/policy version và migration disposition thống nhất BE1.
2. Deposit-aware preparation/start + online media; tái dùng drone safety foundations.
3. FastAPI source job provenance + first/follow-up mapping, Combo processing/pending review.
4. Gateway settlement và release proof; portal/media gate + durable notice/delivery.
5. Correction/claim/refund proof adapters + financial query/export.
6. Meaningful security/concurrency/rollback/DB/broker/E2E/research evidence theo [acceptance matrix](../docs/testing/v3-business-acceptance.md).

Không tạo code/config/migration trong lần rebaseline tài liệu. Build/evidence phase cũ không chuyển thành v3 Done; mỗi capability cần acceptance tương ứng.
