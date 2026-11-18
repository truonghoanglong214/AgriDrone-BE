# ADR 0020 — V3 capture execution, trạng thái, funding và cancellation

Date: **2026-11-13**. Status: **Accepted for implementation design / 03-00** theo yêu cầu chủ dự án. Chưa implementation, migration hoặc runtime acceptance. [Execution baseline/source inventory](../operations/v3-03-execution-baseline.md), [V3-03 plan](../plans/v3-03-capture-funding-and-cancellation-implementation-plan.md), [locked business rules](../../Codex-Plan/Business-Rules-v3.md).

## Context

V3-02 tạo Order có commercial snapshot và Pending deposit nhưng vẫn dùng `AwaitingBaselineAppointment`. Legacy operation purposes/payment gates và unique `(SurveyOrderId,Purpose)` của Mission không diễn đạt V3 CaptureAttempt/Re-flight. Start, appointment, callback, assignment và drone writers hiện không có một cross-module lock protocol chung. Callback có nhánh bỏ qua succeeded event khi Payment đã Paid; V3 cần phân biệt duplicate event với actual receipt mới. Scope isolation và durable cancellation facts phải được thiết kế trước khi sửa từng handler.

## 1. Một Order status, scope rõ ràng; không shadow lifecycle

Chọn **mở rộng additive `SurveyOrderStatus` cho V3**, dùng cùng `SurveyOrder.Status` và `OrderStateHistory`. Không tạo một current-status table/field V3 cạnh tranh với Status cũ. Tất cả labels/numeric values hiện có giữ nguyên; thêm members ở cuối, PostgreSQL labels thêm trong migration mới. Read/mutation strategy chọn bằng persisted Order.Scope, không bằng tên status, service enum hoặc URL.

Các target labels mới chỉ hợp lệ cho V3: `AwaitingAppointment`, `AwaitingDeposit`, `ReadyForCapture`, `Capturing`, `AwaitingCaptureUpload`, `AwaitingInputReview`, `AwaitingProcessing`, `Processing`, `AwaitingReflightDecision`, `AwaitingSettlement`. Tái sử dụng `Completed`/`Cancelled` với V3-specific transition rules; không đổi meaning cho legacy rows.

| Sự kiện có bằng chứng | V3 Order status |
|---|---|
| New Order, hoặc appointment cần xác nhận lại | AwaitingAppointment |
| Appointment confirmed, deposit chưa đủ | AwaitingDeposit |
| Appointment confirmed, funding đủ, Order chưa bị chặn | ReadyForCapture; đây là commercial readiness, không phải authorization cho start |
| Atomic execution start thành công | Capturing |
| Flight kết thúc hợp lệ, còn chờ upload/checksums | AwaitingCaptureUpload |
| Immutable uploaded manifest đủ điều kiện, chờ input-quality decision | AwaitingInputReview |
| Exact manifest có CaptureInputApproval, processing handoff đã commit | AwaitingProcessing |
| Processing service nhận đúng job/source revision | Processing; adapter thuộc V3-04/05 |
| Flight failed hoặc quality decision yêu cầu bay lại | AwaitingReflightDecision |
| Approved Re-flight tạo successor | AwaitingAppointment/AwaitingDeposit/ReadyForCapture từ current appointment/funding facts; không thêm charge |
| Reviewed result + invoice đã hoàn tất | AwaitingSettlement; adapter V3-05/06 |
| Atomic settlement/release receipt | Completed; chỉ V3-06 được thực hiện |
| Cancellation quyết định cuối, không còn flight cần bảo đảm an toàn | Cancelled; Due=0, giữ payment/return obligations |

`CancelRequested` là pending decision/fact, **không** status terminal và không tự giải phóng drone. Nó chặn new start/new attempt cho đến quyết định hợp lệ. Reschedule/funding reversal chỉ hạ commercial readiness của attempt chưa start; không rewrite trạng thái lịch sử của flight đã bắt đầu.

`SurveyOrder.Version` hiện là PostgreSQL row-version/xmin, không phải business sequence. Giữ token này cho optimistic concurrency; new V3 `OrderStateHistory` thêm `TransitionSequence` và unique `(OrderId,TransitionSequence)` cho nonnull sequence. Allocate sequence dưới Order lock, predecessor trỏ history thật; giữ nguyên historical rows có sequence null. Existing `(OrderId,OrderVersion)` là index thường, không thay thế sequence uniqueness. Lưu expected/resulting DB tokens đúng nghĩa, không giả xmin tăng đúng một hoặc ghi một token chưa flush như lịch sử transition đã commit.

### Các V3 rows đang mang status legacy

- Order mới sau cutover của V3-03 khởi tạo AwaitingAppointment.
- Chỉ đổi row `Scope=V3`, `AwaitingBaselineAppointment` khi xác minh không appointment/payment/Mission/attempt/processing decision nào ngoài initial approval, commercial snapshot hợp lệ và initial history khớp. Chuyển bằng reviewed migration disposition hoặc command append transition history; **không UPDATE bulk chỉ theo status**.
- Các V3 rows có operational/financial activity đi qua reconciliation/disposition riêng trước khi activate; chưa chứng minh được thì quarantine. Legacy rows giữ nguyên.
- Old APIs phải filter Legacy khi serialize enum/status hoặc deny V3 mutation trước domain action. Unknown labels ở old clients không được giải quyết bằng cách fake legacy status cho V3.

## 2. CaptureAttempt ownership, status và Mission binding

Surveys sở hữu `CaptureAttempt`, `CaptureAttemptHistory`, input manifest/approval và Order transition decisions. Missions sở hữu execution Mission, drone/schedule, media/GNSS/checklist/flight logs. Database coordinator là nơi duy nhất mutate graph liên module; module không reference domain của module khác để gọi vòng.

Chốt `CaptureAttemptStatus`: `Planned`, `PreflightReady`, `InFlight`, **`AwaitingUpload`**, `Uploaded`, `Processing`, `Accepted`, `ReflightRequired`, `FlightFailed`, **`Cancelled`**. Hai technical states bổ sung làm rõ target V3-01: AwaitingUpload biểu diễn flight đã kết thúc nhưng ảnh chưa đủ; Cancelled chỉ đóng attempt chưa execution. Chúng không thêm fee/policy và không đổi legacy MissionStatus.

| Transition | Điều kiện |
|---|---|
| Planned → PreflightReady | Current preflight/appointment/drone context được xác nhận, immutable proof IDs được pin |
| PreflightReady → Planned | Invalidation trước start; append history/reason, giữ mọi checklist revision cũ |
| PreflightReady → InFlight | Chỉ atomic start coordinator với verified funding/authorization/reservation/current versions |
| InFlight → AwaitingUpload | Verified flight-completed receipt, EndedAt và safety/drone disposition; không có client-derived upload proof |
| InFlight → FlightFailed | Verified failure/incident evidence; không mặc định drone đã hạ cánh hoặc available |
| AwaitingUpload → Uploaded | Exact attempt manifest/object versions/checksums/GNSS requirements được verify |
| Uploaded → Processing | Exact CaptureInputApproval + job/source receipt; không yêu cầu Accepted trước đó |
| Processing → Accepted | Exact processing/review acceptance receipt từ V3-04/05 |
| AwaitingUpload/Uploaded/Processing → ReflightRequired | Authorized quality decision và source evidence, flight đã kết thúc; không đóng attempt đang airborne để cấp chuyến mới |
| Planned/PreflightReady → Cancelled | Order/schedule cancellation trước execution; không xóa proof/media/history |

Accepted/ReflightRequired/FlightFailed/Cancelled terminal, không recover/reset. Approval bay lại từ một terminal parent là quyết định mới; **không** đổi parent FlightFailed thành trạng thái khác để tạo successor. CaptureInputApproval độc lập terminal acceptance, và Order cancellation sau flight không đổi mất nguyên nhân/technical outcome của attempt cũ.

Canonical physical binding: **`mission.drone_missions.capture_attempt_id` nullable cho Legacy, nonnull cho V3 capture**, unique nonnull FK tới CaptureAttempt, kèm composite ownership constraints Tenant/Farm/Order. `CaptureAttempt.MissionId` trong read DTO/context là join qua binding này, không tạo reciprocal required FK gây insert cycle. Domain Planned creation không cần Mission đã được insert; execution methods phải nhận/validate nonempty bound Mission ID. Attempt+Mission được create cùng prepare transaction; không commit một prepared V3 attempt thiếu execution Mission.

Mỗi Order chỉ một active attempt; terminal parent chỉ có một approved successor trong MVP. Order lock cấp AttemptIndex; unique `(OrderId,AttemptIndex)` và nonnull predecessor uniqueness chống đổi Idempotency-Key để tạo trùng. Attempt.Version là logical transition counter dùng concurrency token/history sequence; không lấy domain counter làm PostgreSQL xmin. Same operation/exact fingerprint replay trả record cũ.

Quality Re-flight của phase 03 chỉ nhận parent FlightFailed/ReflightRequired và Order còn pre-release/không cancelled, có approved reason/evidence. Accepted/Cancelled không được dùng làm parent qua endpoint này; post-release correction đi V3-06/08. Một successor thất bại có thể là parent của successor tiếp theo; không tạo nhánh thứ hai từ cùng parent bằng key khác.

### Ràng buộc Mission cũ phải thay có scope

`uq_drone_missions_order_purpose` hiện áp mọi nonnull SurveyOrderId: không thể giữ nguyên cho V3 Re-flight. Expand thêm nullable attempt link trước; sau inventory thay unique legacy index bằng filter `survey_order_id IS NOT NULL AND capture_attempt_id IS NULL`; thêm unique `capture_attempt_id IS NOT NULL`. Enforce V3 capture binding từ parent scope bằng transaction adapter + DB ownership guard/constraint trigger, không chỉ tin client ID. Không tự gán legacy Mission thành Attempt 1. Cross-Legacy/V3 drone schedule exclusion tiếp tục áp dụng; không mở một reservation pool riêng cho V3 để né overlap.

Giữ unique `(FarmId,MissionCode)`; V3 mission-code generator phải gồm stable attempt identity/index, không reuse code chỉ dựa Order/purpose của PrepareMissionSet cũ. Partial unique active-attempt/Order gồm Planned/PreflightReady/InFlight/AwaitingUpload/Uploaded/Processing; successor được insert sau khi parent terminal update đã flush trong **cùng** transaction. Không trông chờ EF tự sắp insert/update tránh partial unique conflict, và không commit parent riêng trước successor. Fault injection phải chứng minh rollback sau parent flush vẫn giữ đúng dữ liệu cũ.

## 3. Funding, cancellation và canonical identities

- Thêm `SurveyDepositObligation` (one/Order) lấy amount/currency/policy IDs từ immutable commercial snapshot. Obligation không phụ thuộc RequiresBaselineMapping; không tăng khi reschedule/Re-flight. `PaymentPurpose.OrderDeposit` và `SurveyAppointmentPurpose.Capture` là additive new labels, không rename BaselineDeposit/PaidService/FinalBalance.
- `SurveyPayment` tiếp tục là checkout/payment attempt; `PaymentEvent` là immutable provider-event evidence. **Paid checkout không phải unique cash receipt.** V3 lưu verified `SurveyFundingReceipt` dedup `(Provider,ProviderTransactionId)` cùng allocation vào đúng Order/deposit obligation; duplicate event không credit hai lần, distinct actual receipt vẫn được giữ kể cả checkout đã Paid.
- V3 thêm nullable deposit-obligation linkage cho checkout. Legacy active-order/purpose index giữ semantics cho legacy rows; V3 enforce một active checkout trên obligation, không dùng unique Paid checkout để từ chối actual receipts mới/partial/late. Callback verify transport trước transaction; reference chỉ giúp lookup, amount/currency/allocation quyết định server-side. Unmatched money có reconciliation fact, không tự tạo paid Order.
- Pinned `CancellationPolicyId` + version là nguồn terms. Flags chưa copy vào snapshot phải đọc exact immutable policy row, không current/effective policy và không sửa fingerprint cũ. Thiếu row/provenance thì NotReady/reconciliation, không business fallback.
- Canonical `SurveyCancellationDecision` có Order/version, requester/decision actor, policy ID/version, reason, acceptable-capture/InputApproval proof khi áp retention, receipt/refund/reservation basis IDs, fingerprint và predecessor.
- Canonical **`SurveyRefundObligation`** dùng chung cho CancellationReturn/ServiceFailure (03), Overpayment (06), ComplaintRefund (08). Unique origin `(OrderId,OriginKind,OriginDecisionId)`; additional receipt sau cancellation có incremental obligation gắn receipt ID riêng, không sửa nghĩa vụ cũ. 03 tạo liability, 08 bổ sung reservation/execution/completion trên cùng obligation identity. Không tạo thêm CancellationRefund table cạnh tranh.
- `CancellationRetention` là classification fact gắn final cancellation decision; không là published service revenue và không phải cash receipt mới. Không phát completed-refund fact khi chưa có chứng từ bank transfer.
- Refund-exposure adapter phải đọc đầy đủ completed refund/active reservation facts dưới cùng Order/funding locks, gồm những facts do adapters/phase sau quản lý. Không có bằng chứng cho số tiền đang giữ/đã hoàn hoặc transfer uncertain thì reconciliation hold; không mặc định F/A=0 chỉ vì port chưa được nối. Không dựng toàn bộ transfer tables/engine ở 03-00/03A-1.

Với `R=confirmed receipts`, `F=completed refunds`, `A=active refund reservations`, `D=pinned deposit`, trước hết yêu cầu `R>=F+A`. `U=R-F-A`. Full-return cancellation: new unreserved return=U, retention=0. Acceptable-capture customer cancellation: retention tối đa min(D,U), new unreserved return=U-retention; preserve existing reserved obligations. Decision lưu exact basis; nếu funds đang reserved sai mục đích thì Admin reconcile trước khi chốt, không lặng lẽ đổi reservation. Late receipt không mở lại balance hoặc tăng retention đã chốt.

R/F/A/D phải cùng Order/currency và có verified allocation/proof. Receipt sai currency/reference vẫn giữ raw/verified transport fact để reconciliation nhưng chưa tính vào funding của Order; không tự đổi currency, round mismatch hoặc suy allocation từ số tiền trùng deposit.

Post-release correction/Re-flight/ComplaintRefund chưa mở ở V3-03; V3-06/08 dùng historical paid/release proof, không thu thêm deposit vì completed complaint refund. First capture và pre-release quality Re-flight không chờ invoice/balance hoặc bản đồ tạo từ chính chuyến bay đó.

## 4. Transaction boundary và total lock order

Chọn specialized `SurveyCaptureExecutionDbContext`/UoW trong Database cho V3 prepare/start/end/failure/Re-flight/cancel transitions liên module. Không commit Surveys/Missions/Identity DbContexts riêng rồi trông chờ event sửa lại Order. Các commands đơn module được giữ UoW riêng **chỉ khi** lấy đúng ordered shared-resource guards trong cùng physical transaction.

Isolation target READ COMMITTED + explicit locks + unique/exclusion constraints; server time evaluation sau khi có relevant guards. Fixed total order, UUIDs tăng dần trong mỗi group/table:

| Rank | Rows/guards | Lock rule |
|---|---|---|
| 0 | Activation singleton và versioned operational/config rows khi cần current config | Guard SHARE cho capture; config publisher UPDATE. Pinned commercial-only reads/callback cash recording không cần current activation. Config writers không quay xuống lock Order. |
| 1 | Tenant → User → TenantMembership → ManagerProfile → Farm → FarmManagerAssignment | Read/authorize SHARE; relevant deactivation/revoke/archive/availability writer UPDATE. Chọn current reference IDs qua discovery read, lock theo thứ tự rồi revalidate; reference đổi thì retry transaction, không lock thêm lower-rank row muộn. |
| 2 | Tất cả existing Orders liên quan, gồm pinned baseline source Order khi phải đọc proof | UPDATE cho Order bị mutate, SHARE cho source; IDs sorted. Single-Order callbacks có thể bắt đầu tại rank này sau nonlocking Payment→Order lookup. |
| 3 | Operation/idempotency receipt | Claim/replay với actor/API-version/action/key/fingerprint, unique business identities là backstop |
| 4 | Current appointment/revisions | SHARE để start; UPDATE khi confirm/reschedule/invalidate |
| 5 | Deposit obligation → checkout/payment → funding receipt/allocation → cancellation/refund exposure/obligation facts | Relevant rows protected tới commit; all writers/reconcile/refund reservation tuân cùng subset order |
| 6 | Pinned baseline/current-pointer và release/withdrawal evidence nếu cần | Exact same transaction, source Order đã được khóa rank 2; missing proof fail closed |
| 7 | CaptureAttempt(s), parent trước successor theo sorted existing IDs; histories append sau mutation | UPDATE current/parent khi cấp successor; Order lock bảo vệ absence/current index |
| 8 | Execution Mission(s) | UPDATE mutation, không lấy scope/funding từ client |
| 9 | Drone(s) → booking/reservation facts | IDs sorted gồm old/new Drone khi đổi lịch; exclusion across scopes; flight end/failure không tự giải phóng tài nguyên chưa có safety disposition |
| 10 | Preflight decision revisions → safety receipts → input manifest/approval/telemetry receipt facts | Exact revisions protected; versions/config cần đọc đã pin ở rank 0 |

Writer chỉ lấy subset cần thiết nhưng giữ relative order; không join `FOR UPDATE payment,order` rồi giả định PostgreSQL luôn lock Order trước. Scope lookup không lock không được dùng làm authorization/funding truth. Batch/multi-Order commands phải discovery đủ IDs và lock sorted, không acquire thêm Order muộn sau Drone.

Assignment/profile/drone maintenance writers áp dụng cho shared resources kể cả khi entry route không versioned: mutation rank 1 hoặc 9 sẽ serialize với start locks; nếu cần invalidation các Orders thì lock rank 1→Orders→... trong cùng transaction. Legacy handlers vẫn giữ business rules cũ, nhưng không được bypass shared drone/authorization locking khi cạnh tranh V3.

Start transaction ghi current authorization/funding refs, consume reservation, transition Attempt/Mission/Order, histories, audit, durable receipt và Outbox cùng commit. Failure ở bất kỳ checkpoint rollback cả operation claim. Cancellation thắng thì new start fail; start thắng thì cancel thấy InFlight, chỉ lưu request và giữ resources đến safe termination. Không network call, object upload hoặc SePay checkout HTTP trong SQL transaction: lưu intent trước, external operation idempotent sau, reconcile kết quả bằng receipt.

Capture start dùng một server `StartedAt`/evaluation time lấy **sau khi đã lấy đủ relevant guards**, rồi recheck appointment window/safety validity trước transition. Không dùng timestamp từ readiness response hoặc timestamp trước một lock wait dài để cho phép start sau khi cửa sổ đã hết hạn. Hết scheduled window không tự chứng minh flight đã kết thúc: Drone.InUse/physical flight-disposition vẫn là guard dù range reservation cũ đã hết giờ.

## 5. Ports, events và old-route compatibility

- `SurveyOrderCaptureContextV5`/`ISurveyOrderCaptureContextV5Query` là read-only readiness/context; readiness response không là start permission. `ISurveyCaptureExecutionCoordinator` điều phối authoritative mutations, implementation Database. Context query không giả paid state hoặc default capture radius.
- Target events mới `SurveyCaptureStartedV1`, `SurveyCaptureEndedV1`, `SurveyCaptureInputApprovedV1`, `SurveyCaptureReflightCreatedV1`, `SurveyOrderCancelledV1` có separate descriptors/strict validators/consumer queues. Payload pin Order/Attempt/Mission/manifest/policy/revision/fingerprint, không secrets/PII/raw signed URLs. Names là target chưa declared; implementation phải đăng ký cùng contracts/tests, consumers trước producers.
- Legacy BaselineMissionStartedV1/PaidMissionStartedV1 không được xử lý V3 như legacy; V3 producer không phát old semantic event. Incoming old event trỏ V3 được scope-guard và ghi permanent rejection/operational alert, không infinite retry hoặc advance Order. New events không reapply synchronous business transition đã committed; chỉ project/notify idempotently.
- Old appointment/payment/prepare/start/recover/reset/result-publish routes phải select Legacy strategy hoặc reject V3 có error rõ ràng. SePay callback URL duy nhất **`POST /api/integrations/payments/sepay/ipn`** giữ nguyên; dispatch theo persisted Payment→Order scope, không URL/client scope.
- New V3 route families/DTOs được triển khai cùng slices; old status/selected-service projection phải filter Legacy trước khi serialize. Không expose `CaptureAttempt` qua generic `TransitionMission` để lách coordinator.

## 6. Migration và triển khai theo lát

03A-1 chỉ new CaptureAttempt/status/history domain + focused unit tests, không EF/enum DB/controller/migration. 03A-2 hiện thực các contracts/policies/design còn lại. 03B mới expand schema/constraints/index changes và scoped disposition. Consumers/guards mới phải sẵn sàng trước khi producer V3 bắt đầu ghi labels/rows mới. Quarantine V3 in-flight dirty rows chưa classified; không gán fake Attempt 1/proof.

Down chỉ được phép với empty expand artifacts khi chứng minh được; populated money/evidence/history ưu tiên roll-forward và test refusal. 02A-2 DB acceptance/02D-3/02F-1 vẫn pending. 03-00 Accepted là **design/source baseline**, không ký start race, upgrade, rollback hoặc go-live. V3 Follow-up release-proof default unavailable và public activation giữ off.

## Consequences

Một canonical lifecycle/history/funding/obligation set giảm nguy cơ sửa riêng intake/start/callback rồi làm lệch các nhánh khác. Đổi lại V3-03 phải cập nhật shared Identity/Missions resource writers và scoped old-route guards, không chỉ thêm CaptureAttempt table. Critical checkpoints cần real PostgreSQL/PostGIS tests; fixture unavailable không được thay bằng fake proof hoặc mock-only acceptance.
