# V3-03 — Appointment, deposit, CaptureAttempt, Re-flight và cancellation

Plan date: **2026-11-13**. Status: **03-00 Accepted, 03A-1 Implemented (domain + 52 unit tests passed; P1/P2 audit remediated) — next 03A-2**. [Execution baseline](../operations/v3-03-execution-baseline.md) và [ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md) đã chốt decisions/file manifest. Chỉ thay tài liệu; không sửa code/config/migration, không chạy payment hoặc bật activation.

Nguồn nghiệp vụ: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [BE1 master plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [V3-01 locked design](../domain/v3-01-locked-design.md). Contracts: [business coordination](../contracts/survey-business-v3.md), [HTTP intent](../openapi/survey-business-v3.openapi-intent.md). Work tracking: [V3-03 execution checklist](v3-03-execution-checklist.md). Acceptance gốc: [V3-T05–08, T14, T17, T21, T26, T28, T33–34](../testing/v3-business-acceptance.md).

## 1. Outcome, phạm vi và dependency thực tế

Sau phase này, một V3 Order có thể có appointment được xác nhận, nghĩa vụ cọc từ snapshot, confirmed funding, một hoặc nhiều capture attempts có execution Mission riêng, start an toàn trong transaction chung, upload theo attempt và approved input manifest. Quality Re-flight giữ nguyên giá/cọc/hóa đơn; cancellation lưu đúng quyết định, retention và nghĩa vụ hoàn tiền.

**Đầu ra V3-03 kết thúc ở source capture/approved input và durable cancellation obligations.** V3-04/05 xử lý mapping/Combo và chốt billable count/invoice; V3-06 settlement/release/notice; V3-08 vận hành refund transfer, complaint và post-release correction. Không dựng lại các subsystem đó trong phase này.

V3-02 hiện có implementation nhưng 02A-2 DB acceptance, 02D-3 và 02F-1 còn pending; xem [evidence 2026-11-12](v3-02-execution-checklist.md#9-release-proof-remediation--2026-11-12). Build/unit/component evidence của đợt trước không chứng minh PostgreSQL locking/upgrade/rollback. V3-03 có thể triển khai domain/contracts trên isolated fixtures, nhưng không được đóng integration acceptance hoặc go-live nếu prerequisite gates chưa đạt.

`UnavailableV3BaselineDeliveryProofReader` hiện từ chối Follow-up production. V3-03 giữ gate đó; không lấy Completed/Published làm proof, không bật fake adapter. Test Follow-up dùng released-baseline fixture được ghi rõ là test data. First Order có thể được kiểm thử capture độc lập mà không yêu cầu first map đã có từ trước.

## 2. Source audit — foundation có sẵn và chỗ phải thay

Các đường dẫn dưới đây tương đối với `backend/src`; đây là đọc source, không phải live DB audit. Chưa thấy `CaptureAttempt`, `CaptureInputApproval` hoặc capture V5 implementation; tên mới ở các phần sau là **target**, chưa tồn tại.

| Source đã đọc | Hành vi hiện tại / tác động V3-03 |
|---|---|
| `Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs` | `CreateV3` pin giá/cọc và Pending, nhưng vẫn dùng status `AwaitingBaselineAppointment`; các phương thức legacy còn dùng `RequiresBaselineMapping` để skip/credit deposit. Tách V3 lifecycle/readiness khỏi các switch legacy; không đổi tiền lịch sử. |
| `Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderReadinessPolicy.cs` | Gate BaselineMapping bằng deposit, PlantHealth bằng commercial flow cũ. Tạo V3 policy chung cho MAP/COMBO, First/Follow-up; balance không chặn capture. |
| `Modules/AgriDrone.Modules.Surveys/Application/Features/Appointments/{ProposeSurveyAppointment,ConfirmSurveyAppointment,RequestSurveyAppointmentReschedule}/` | Có owner confirmation/version/audit/UoW; confirm phân nhánh BaselineMapping/PaidService. V3 cần capture appointment semantics và invalidation/reservation đồng bộ. |
| `Modules/AgriDrone.Modules.Surveys/Application/Features/Payments/{InitiateSurveyPayment,ProcessSurveyPaymentEvent,ReconcileSurveyPayment}/` | Checkout deposit đang theo `BaselineDeposit` + AwaitingBaselineDeposit; callback cập nhật readiness theo payment purpose. Giữ verified transport/dedup nhưng dispatch theo persisted Order scope; không cho V3 chạy nhánh final-balance-before-flight. |
| `AgriDrone.Api/Controllers/SurveyPaymentCallbacksController.cs` | SePay verification rồi gửi `ProcessSurveyPaymentEventCommand`. Giữ callback URL và verifier; server resolve Payment→Order→scope, không tin scope trong payload. |
| `BuildingBlocks/AgriDrone.Database/Surveys/SurveyOrderOperationalContextV4Query.cs` | Legacy purpose, payment/mapping requirements; có fallback bán kính/geometry. Không dùng adapter này làm V3 safety truth hoặc copy fallback vào V5. |
| `Modules/AgriDrone.Modules.Missions/{Infrastructure/Queries/SurveyOrderMissionPlanningQuery.cs,Application/Features/Missions/PrepareMissionSet/}` | Planning dựa context V3 cũ, `RequiresBaselineMapping` và legacy selected service. V3 capture cần đường riêng, một execution Mission/attempt; COMBO không tự tách thành hai chuyến trả phí. |
| `Modules/AgriDrone.Modules.Missions/Application/Features/Missions/TransitionMission/TransitionMissionCommandHandler.cs` | Readiness đọc trước SaveChanges, phát Baseline/PaidMissionStartedV1. Không đủ bằng chứng atomic cross-module start; V3 coordinator phải recheck/commit Order+Attempt+Mission+reservation cùng transaction. |
| `Modules/AgriDrone.Modules.Missions/Domain/Missions/DroneMission.cs`, `.../Features/Missions/RescheduleOrderMission/` | Có `RecoverFailedFlight`, xóa StartedAt/EndedAt khi recover. V3 phải dùng new attempt/new Mission; bảo vệ cả old route/generic transition khỏi bypass. |
| `Modules/AgriDrone.Modules.Missions/Application/Features/Media/{CreateUploadSession,FinalizeMissionUpload,ResetMissionUpload}/` | Upload/finalize theo Mission; reset có xóa media/telemetry. V3 cần attempt binding, frozen manifest và deny destructive reset khi có V3/evidence. |
| `Modules/AgriDrone.Modules.Surveys/Domain/{Policies/CancellationPolicy.cs,Orders/SurveyOrderCommercialSnapshot.cs,Orders/OrderStateHistory.cs}` | Policy có no-show/failure/postponement flags; commercial snapshot copy một phần và pin policy ID. Đọc đủ terms bằng **pinned ID/version**, không current policy; reuse append-only history. |
| `BuildingBlocks/AgriDrone.Database/Surveys/{SurveyApprovalDbContext.cs,SurveyAppointmentSchedulingDbContext.cs,SurveyPaymentProcessingDbContext.cs}` | Mẫu specialized UoW/row locks đã có. Capture/start/cancel cần graph và transaction riêng, không ghép nhiều DbContext commits. |

## 3. Quyết định triển khai để không sửa từng handler rời rạc

### 3.1 Scope, trạng thái và compatibility

- Server đọc persisted `ContractScope` của Order trước khi chọn Legacy/V3 strategy. Tất cả old endpoints, callbacks, workers và generic Mission/media mutations phải theo cùng nguyên tắc. Giữ enums/JSON/golden fixtures released; không đổi nghĩa `PLANT_HEALTH` thành COMBO.
- V3 dùng semantics AwaitingAppointment → AwaitingDeposit/ReadyForCapture → Capturing → AwaitingInputReview/Processing; payment, attempt, review và release là các trục riêng. `Cancelled` đóng nghĩa vụ còn phải thu. Không gán `Completed` khi mới bay/upload xong.
- 03-00 đã chốt ADR 0020: mở rộng additive SurveyOrderStatus cho V3 và dùng cùng Order.Status/history, không shadow current-status field. Không rename/renumber released labels hoặc tự chuyển active legacy Orders. Những V3 rows hiện mang tên status legacy phải được inventory; migration chỉ đổi khi có quy tắc chứng minh được, không đoán từ tên.
- `OrderStateHistory` và `CaptureAttemptHistory` append cùng transaction mỗi transition, gồm predecessor, actor hoặc verified system source, reason/evidence, expected version, UTC time/correlation/operation receipt. Audit generic không thay thế business histories.

### 3.2 Funding và appointment

- Mọi **new paid Order**, gồm MAP/COMBO và First/Follow-up, có một deposit obligation lấy từ commercial snapshot. Không `RequiresBaselineMapping ? Paid : ...`, không lấy current catalogue, không default money.
- Manager đề xuất capture appointment; Owner xác nhận. Chưa xác nhận không mở deposit checkout. V3 dùng capture appointment, không ép khách đi qua baseline appointment rồi paid-service appointment cho cùng lần capture.
- Một obligation có thể có nhiều payment attempts/verified receipts, nhưng retry/reschedule/Re-flight không tạo deposit obligation thứ hai. Amount/currency/reference/purpose do server xác định; client không gửi Paid/amount truth.
- Cọc đủ phải dựa confirmed credit còn hợp lệ, có allocation/receipt provenance; pending/failed/wrong currency/unmatched references không tính. Partial/overpayment được ghi nhận để reconciliation, không tự sửa snapshot. Refund reservation/cancel pending ngăn dùng đồng thời phần tiền đã được dành để hoàn.
- Capture **không cần** finalized pole count, final invoice hoặc full balance. First capture dùng reviewed capture area/safety context có thật; không bắt first orthophoto/internal map/boundary sinh từ chính capture tương lai.
- Payment webhook sau reschedule hoặc hết hạn checkout vẫn lưu actual receipt. Payment sau cancel không mở lại Order; cập nhật reconciliation/return obligation, không xóa tiền vì không còn active checkout.

### 3.3 Attempt và evidence

- Order 1–N `CaptureAttempt`, unique `(OrderId, AttemptIndex)`; mỗi attempt có một execution Mission, MissionId unique. Allocate index và kiểm tra active attempt trong Order lock; same operation replay trả attempt cũ.
- State business theo locked design và refinement ADR 0020: Planned → PreflightReady → InFlight → AwaitingUpload → Uploaded → Processing → Accepted; nhánh FlightFailed/ReflightRequired ở thời điểm hợp lệ, Cancelled chỉ cho attempt chưa execution. Flight end/Uploading facts vẫn nằm trong Mission/flight evidence; terminal parent không reset.
- Quality Re-flight phải có approval/reason, cùng Order, `SupersededAttemptId` trỏ đúng attempt của Order đó. Đóng/freeze attempt cũ rồi tạo attempt/Mission mới atomically. Không duplicate active successor do đổi Idempotency-Key; không tạo charge/invoice/deposit mới.
- Đổi lịch trước execution không mặc định là Re-flight: appointment/reservation/history có revision mới, old schedule/preflight evidence vẫn giữ. Bất kỳ lần execution mới sau flight failure đều new attempt/new Mission.
- Media upload sessions, object keys, GNSS/import/field notes, preflight revision và flight logs bind Tenant/Farm/Order/Attempt/Mission. Derive Attempt từ Mission phía server; cross-attempt ID hoặc conflicting source/hash phải bị từ chối.
- `CaptureInputManifest` là immutable exact media IDs/object versions/checksums + required GNSS/log refs. `CaptureInputApproval` duyệt quality của manifest sau upload verification; không có quyền publish result hoặc tự đánh dấu billable count.
- Upload complete ≠ quality approved ≠ Attempt.Accepted. V3-03 tạo approved-input handoff; Processing/Accepted được nối từ đúng processing/review receipts ở V3-04/05. Không bắt pipeline chờ Accepted để bắt đầu pipeline.

### 3.4 Cancellation và tiền

Đọc `CancellationPolicyId`/version của original commercial snapshot, kể cả version đó đã hết hiệu lực cho Order mới. Các flags chưa được copy vào snapshot phải resolve từ immutable pinned policy row; không rewrite snapshot/fingerprint cũ để lấy current terms.

| Tình huống | Quyết định / durable facts |
|---|---|
| Khách hủy trước capture | Due đóng; toàn bộ confirmed receipts còn giữ phải trả, trừ tiền đã hoàn thực tế, tính đến active reservations để không lập trùng nghĩa vụ. |
| Weather/safety/authorization chưa đạt | Postpone, giữ cọc; khách dừng trước capture thì full return. Không fee vận hành. |
| No-show | Dùng pinned allowance; approved MVP là một free reschedule. Lần tiếp theo Admin xử lý, không tự tạo fee; hủy trước capture vẫn full return. |
| Khách hủy sau acceptable capture | Admin xác nhận requester/reason và quality evidence. Retention không vượt deposit snapshot hoặc actual unrefunded funds; phần dư là return liability, không thu remaining balance. |
| Team không thể bàn giao, hai bên dừng | Full return phần receipts còn lại, không giữ cọc chỉ vì đã bay. |
| Có Mission đang InFlight | Ghi cancellation request; không terminal-cancel/giải phóng drone reservation khi drone chưa kết thúc an toàn. Serialize start vs cancel; quyết định cuối dựa flight/quality evidence thật. |

Funding basis phải được đọc dưới cùng Order/funding lock. Gọi `R` là confirmed receipts, `F` là completed refunds, `A` là active refund reservations và `D` là deposit snapshot. Khi `R >= F + A`, phần chưa cam kết là `U = R - F - A`. Before-capture/service-failure: retention=0, chỉ tạo mới return obligation cho `U`; giữ các obligations/reservations cũ để tổng không bị lập trùng. Customer cancellation sau acceptable capture: retention tối đa `min(D, U)`, new unreserved return=`U-retention`; các active reservations vẫn phải được thực hiện/đối soát đúng nghĩa vụ. Final cancellation receipt lưu basis, classification và IDs liên quan. Nếu `F+A>R` hoặc có transfer uncertain thì reconciliation hold, không tự sửa evidence để cân số. Actual receipt đến sau cancellation không làm tăng retention đã chốt hoặc mở balance; lập thêm return/reconciliation fact idempotently.

“Acceptable capture” dùng technical capture-quality decision có actor/evidence và manifest; **không** đòi terminal Accepted của processing để quyết định, cũng không tin Owner/client boolean hoặc chỉ Mission.EndedAt.

V3-03 phải persist cancellation decision + exact funding basis + `CancellationReturn`/`ServiceFailure` obligation và `CancellationRetention` classification cùng cancel/history/Outbox. Chỉ giữ seam trong memory/event là chưa đạt. Chọn một canonical obligation identity để V3-08 bổ sung reservation/transfer, không tạo bảng nghĩa vụ cạnh tranh. Không ghi completed refund/cash out khi chưa có chứng từ; không ghi retention thành service revenue. V3-08 chịu trách nhiệm transfer workflow/cap execution, V3-09 reporting.

Chỉ pre-release cancellation và quality Re-flight được đóng acceptance trong V3-03. Post-release correction/Re-flight/ComplaintRefund theo BR-COR/BR-PAY-06 nối V3-06/08 với historical paid proof; không mở production hoặc thu thêm deposit trong phase này.

## 4. Kiến trúc transaction và contract

### 4.1 Authoritative V5 capture context

Tạo target `SurveyOrderCaptureContextV5` và query port mới dưới `AgriDrone.IntegrationContracts/Surveys`; adapter thuộc Database. Giữ V3/V4 legacy interfaces nguyên nghĩa. Typed V5 snapshot tối thiểu:

- Tenant/Farm/Order/scope, policy/snapshot/service revision/capabilities, first/follow-up và expected versions.
- Appointment revision/window/confirmation, current assignment/Manager eligibility, authorization và operational safety decision refs.
- Pinned deposit obligation, confirmed allocations/receipts/refunds/reservations; không boolean Paid từ caller.
- First capture area source hoặc Follow-up pinned released baseline/boundary refs; không fabricated geometry/default radius.
- Attempt/index/parent/Re-flight approval, Mission/drone/reservation/preflight refs, input manifest/approval refs khi đã có.
- Ready/rejected reason codes cho display; snapshot không phải start authorization receipt.

Operational/checklist/route/safety parameters lấy từ versioned approved configuration khi plan/preflight/job; thiếu configuration thì NotReady. Spatial/AI calibration thuộc V3-04/05; V3-03 chỉ pin/reference seam, không bịa tolerance, timeout, radius hoặc drone capacity để demo chạy.

### 4.2 Atomic start coordinator

Target specialized `SurveyCaptureExecutionDbContext`/UoW trong Database, map đúng shared physical tables. Missions feature gọi internal start coordinator; không gọi HTTP nội bộ hoặc giữ transaction trong external I/O.

Transaction:

1. Authorize caller, claim operation identity; lock Order rồi current appointment/assignment/funding facts, Attempt/Mission, drone/reservation và preflight/safety facts theo lock order cố định.
2. Re-read current states/versions/server time. Verify active scope, not cancelled/cancel pending, valid appointment window, assignment/authorization, funding, available drone, reservation, current preflight/checklist/config and safety.
3. Consume/reserve resource bằng DB constraints; không chỉ kiểm tra query rồi ghi sau. Transition Attempt + execution Mission + V3 Order status/history và durable start receipt trong cùng transaction.
4. Audit + Outbox + idempotent response receipt cùng commit. Inject failure ở từng checkpoint phải rollback cả start/claim/reservation/history. Retry cùng payload trả kết quả cũ; changed payload conflict; revoked actor vẫn bị deny.

Mọi writer ảnh hưởng start (cancel/reschedule/revoke assignment/funding reversal/drone maintenance/reservation/preflight invalidation) phải dùng cùng lock/constraint protocol. Lock order được ghi bằng ADR/implementation note ở 03-00; không chỉ khóa trong reader rồi bỏ qua writers. Start thắng race thì cancellation/reschedule thấy flight đã start và xử lý phù hợp; cancel/revoke thắng thì start thất bại.

Existing async Baseline/PaidMissionStartedV1 consumers không được mutate lại V3 Order theo legacy logic sau synchronous commit. V3 có event family/validator/consumer mới hoặc explicit scope isolation; never publish new meaning vào old envelope rồi đánh Done do Inbox dedup.

### 4.3 Endpoints và events — target, chưa deployed

Route family theo `/api/v3/tenant-owner/survey-orders/{id}`, `/api/v3/system-manager/...`, `/api/v3/system-admin/...`; không đổi SePay callback URL. DTO cụ thể phải chốt ở từng slice cùng auth/version/error/idempotency tests:

| Capability | Actor / yêu cầu |
|---|---|
| Propose/confirm/reschedule capture appointment | Assigned Manager propose; owning Owner confirm/request reschedule; server assignment/window/version checks |
| Deposit checkout/history | Owning Owner, server amount/purpose; privileged Admin reconciliation có proof; receipt metadata không lộ bank secrets |
| Prepare attempt/Mission, readiness, preflight/start/end/failure | Assigned eligible Manager; start coordinator authoritative, reason/evidence/version bắt buộc |
| Upload/resume/finalize input manifest và input-quality approval | Assigned Manager; checksum/object proof/attempt ownership, append-only approval revision |
| Approve/create quality Re-flight | Authorized assigned Manager cho technical quality; Admin xử lý dispute/cancellation; có parent/reason/evidence và operation receipt |
| Request/cancel/no-show/postpone decisions | Owner request theo ownership; Manager ghi operational evidence; Admin xác nhận retention/service-failure decision |

Target Outbox handoffs phải có Tenant/Farm/Order/Attempt/Mission/manifest/policy revisions và fingerprint, không raw secrets/signed download URLs. Unknown scope/attempt/stale output từ worker không được advance new attempt. New consumers deploy trước producer; payload fixtures cũ giữ nguyên.

## 5. Các lát triển khai A–H

| Slice | Đầu ra bắt buộc | Exit gate |
|---|---|---|
| 03-00 | Source inventory, trạng thái V3-02 gates, exact writer/caller map, V3 status/lock/compatibility ADR và migration sequence | Không còn chỗ “sẽ audit sau” cho callback/start/reset/reschedule/cancel ảnh hưởng cùng graph |
| 03A | Domain CaptureAttempt/history, manifest/input approval, V3 appointment/funding/cancel semantics và invariants | Domain checks; 03A-1 chưa sửa EF/schema/handlers |
| 03B | Additive persistence/constraints và canonical deposit/cancellation obligations, expand migration | SQL review + fresh/populated fixtures; historical migrations và receipts không bị rewrite |
| 03C | Capture appointment + shared deposit checkout/verified callback/reconciliation | First/Follow-up MAP/COMBO đủ cọc, balance không chặn; cancelled/late/duplicate callbacks đúng |
| 03D | V5 read snapshot, attempt preparation + atomic start coordinator/writer protocol | PostgreSQL reservation/start/revoke/cancel races và checkpoint rollback thật |
| 03E | Attempt-scoped upload/GNSS/checklist, frozen manifest/input approval, future processing seam | Cross-attempt deny, resume/idempotent hash checks, no Accepted-before-processing dependency |
| 03F | Quality Re-flight mới, destructive legacy-route guards | New Mission/index, không đổi charge; old timestamps/media/GNSS/checklist bảo toàn |
| 03G | Cancellation/no-show/weather decisions + durable retention/return obligations | Due=0 khi terminal cancel, obligations không mất; payment/start/cancel races, cap/no duplicate liability |
| 03H | V3 HTTP/DI/schema handoff, full compatibility/DB/broker/security acceptance và V3-04 handoff | Prerequisite gates đạt và evidence thật; public go-live vẫn off |

Mỗi slice nối API/DI/tests cần thiết để review được; 03H là integration consolidation, không để wire runtime đến cuối. Thứ tự chi tiết/dependencies: [execution checklist](v3-03-execution-checklist.md).

## 6. Model/schema targets và file manifest

Tên target đã được chốt trong [03-00 manifest](../operations/v3-03-execution-baseline.md#6-exact-file-manifest-theo-slice); đây không phải claim file/table đã có:

- Surveys owns `CaptureAttempt`, `CaptureAttemptHistory`, `CaptureInputManifest`, `CaptureInputApproval`, deposit obligation/funding projection, cancellation decision/return obligation; exact ownership của media execution vẫn ở Missions. Order aggregate root serialization bảo vệ attempts; các module không tự tăng index.
- Missions reuse `DroneMission`, drone/reservation/preflight/media/GNSS assets, thêm attempt linkage; unique execution Mission/attempt, FK cùng Tenant/Farm/Order. Cấm arbitrary client AttemptId cross-link.
- Database owns capture execution UoW/coordinator, shared table mapping, funding/cancel transaction adapters và V5 context query. Surveys và Missions không reference trực tiếp domain của nhau chỉ để reuse enum.
- IntegrationContracts owns V5 port/snapshots và reviewed event families. Existing contracts V3/V4/V1 không sửa meaning.
- API owns versioned controllers/DTOs; old route dispatch/guards chống V3 bypass. FE/mobile handoff có stage/reason/expected version/idempotency/online retry; không hiển thị technical implementation cho người dùng.

Required DB constraints: unique `(OrderId,AttemptIndex)`, unique execution Mission binding, one active successor/operation receipt theo lifecycle, same-owner composite FKs, immutable manifest/approval IDs+fingerprints, valid predecessor/order lineage, positive whole VND obligations, provider/business receipt dedup, reservation overlap constraints và one current state-history predecessor dưới lock. Inventory constraints đã có trước khi tạo mới.

Migration rollout: expand nullable scoped fields/tables → compatibility readers/guards → explicit disposition/backfill của những rows chứng minh được → constraints tightening sau checks. Không gán Attempt 1/fake Paid/proof cho historical Missions để qua FK. Legacy artifacts chưa classified giữ legacy; V3 rows thiếu commercial snapshot/config bắt buộc quarantine/fail closed. Down không được xóa durable money/evidence facts đã có; prefer roll-forward và test refusal khi populated.

Pre-03B compatibility bridge (2026-11-13): SurveyOrderConfiguration Ignore DepositObligationId và OrderStateHistoryConfiguration Ignore TransitionSequence để EF conventions không thêm cột chưa tồn tại vào current SELECT. Hai properties hiện chỉ là domain state, chưa persist qua EF. 03B bắt buộc bỏ Ignore, thêm explicit column mapping/migration/constraints và cập nhật V3PrePersistenceModelCompatibilityTests theo schema mới; không để bridge làm dữ liệu bị bỏ qua sau khi expand migration đã có. Xem [remediation evidence](v3-03-execution-checklist.md#10-evidence-bypass-và-pre-03b-ef-compatibility-remediation--2026-11-13).

## 7. Acceptance và rollback — không chỉ handler mocks

| ID nội bộ | Scenario cần chạy | Mapping |
|---|---|---|
| 03-T01 | First/Follow-up × MAP/COMBO: no deposit, pending/partial/invalid credit, sufficient deposit với balance/count chưa có | V3-T05/06 |
| 03-T02 | Duplicate/changed payload callback, provider event ordering, stale checkout, cross-scope routing, payment after cancel/reschedule | V3-T07/26/34 |
| 03-T03 | Concurrent appointment confirm/reschedule/revoke; old version và lost acknowledgement replay | V3-T08/28 |
| 03-T04 | Start vs cancel/assignment revoke/funding reversal/drone maintenance/preflight invalidation; overlap reservation | V3-T08/28 |
| 03-T05 | Fault after operation claim/reservation/Attempt/Mission/Order/history/Outbox → full rollback; retry cannot double-start | V3-T08 |
| 03-T06 | Two new-attempt/Re-flight commands với same/different keys → one allowed active successor; no new charge | V3-T08 |
| 03-T07 | Parent cross-Order, old upload/job callback và generic recover/reset endpoint → reject/no mutation of frozen attempt | V3-T08/21/33 |
| 03-T08 | Resume upload, checksum conflict, cross-attempt media/GNSS, finalize twice, reapproved manifest, revoked Manager | V3-T14/21/28 |
| 03-T09 | Approved input can reach processing seam before terminal Accepted; manifest/hash mismatch cannot | V3-T14 |
| 03-T10 | Before-capture cancel/weather/no-show/free allowance; service-failure full return; customer after acceptable capture retain≤deposit | V3-T17/26 |
| 03-T11 | Missing quality proof, active refund reservations, previous completed refunds, late actual receipt after cancel → no invented retention/no duplicate liability | V3-T17/25/26 |
| 03-T12 | Legacy completed/mapped/unmapped Orders và old routes/enum/envelopes after upgrade; V3 không lọt legacy consumer | V3-T33/34 |
| 03-T13 | Public flags off, missing operational config/release proof, current catalogue/policy đổi sau Order creation | V3-T04/05/28 |

Unit tests chứng minh policy arithmetic/transitions/history; adapter/component tests chứng minh DI/read paths, không substitute PostgreSQL. DB tests chạy real PostgreSQL/PostGIS với actual UoW/queries và barriers cho races; broker tests verify commit-before-ACK/dedup/late events; API tests verify all actors/routes. Không gọi test fake DbConnection là “migration passed”, không gọi fail-before-write là “rollback proved”.

Lệnh dự kiến khi implementation (chưa chạy trong phiên lập kế hoạch): `dotnet build backend/AgriDrone.sln --no-restore`; focused tests của slice rồi UnitTests/ArchitectureTests; IntegrationTests có explicit fixture/config và filter phase; `dotnet ef migrations script` cho exact inspected predecessor→new expand migrations. Không chạy toàn bộ suite phụ thuộc dịch vụ ngoài nếu chưa xác minh test DB/broker names và isolation.

## 8. Definition of Done và bước đầu tiên

V3-03 chỉ accepted khi deposit/appointment/capture/Re-flight/cancel invariants đi qua real paths, all relevant Legacy routes/consumers được isolate, critical DB/race/rollback gates đạt, source/manifest/histories/receipt facts bảo toàn và handoff V3-04 không cần fake boundary/input/release. Public intake/checkout production vẫn off đến end-to-end readiness của các phase sau.

**03-00 source baseline Accepted, 03A-1 Domain/History Implemented (52 unit tests passed), và 03A-2 Operational/Funding/Cancellation Contracts Implemented (11 unit tests passed; 854/854 UnitTests và 88/88 ArchitectureTests passed); bước kế tiếp là 03B-1.** 03A hoàn thành toàn bộ domain CaptureAttempt, history, immutable manifests/approvals, deposit obligation, funding receipts/allocations, cancellation decisions, refund obligations và retention facts, kèm V5 contracts và execution coordinator port; chưa thay controllers hay migrations. Chỉ sang schema/flows khi domain contracts được verify; không hỏi lại các business policies đã chốt để trì hoãn công việc.

V3-04 nhận exact attempt/Mission, approved immutable input manifest, operational/source versions và locked first/follow-up mapping basis. V3-06 nhận funding facts và release-proof adapter seam. V3-08 nhận canonical cancellation/return obligations với evidence/cap basis; không dựng lại hoặc thanh toán lần hai từ events lịch sử.
