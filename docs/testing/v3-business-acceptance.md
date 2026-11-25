# V3 — Business acceptance matrix

Status: **Planned checks, not executed**, 2026-11-10. Đây là test specification; không tạo/sửa/chạy test code trong lần cập nhật tài liệu. [Rules](../../Codex-Plan/Business-Rules-v3.md), [traceability](../domain/v3-traceability-matrix.md), [implementation plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md).

## Acceptance scenarios

| ID | Setup/action | Required outcome |
|---|---|---|
| V3-T01 | Active demo catalogue, overlapping/missing versions | MAP=1.000, COMBO=2.000, common deposit=100.000; no disease-only/Harvest; invalid config cannot create Order |
| V3-T02 | Public request + tracking, guess tokens, submit retries | No provisioning before approval; same request replay; tracking minimal/no enumeration/PII leak |
| V3-T03 | Concurrent Admin approval and injected failure after each resource write | Exactly correct Tenant/Farm/Order/assignment/invitation + creation-time price/deposit/policy snapshots or full rollback |
| V3-T04 | Existing owner Farm/new Farm, released vs unreleased Baseline, MAP↔COMBO Follow-up | Correct resource reuse, new paid Order/snapshots; unreleased first Baseline not bookable; no duplicate Tenant/Farm |
| V3-T05 | Change rates/deposit/claim/cancellation policies after Order creation, verify count/release later | Existing snapshots unchanged; new Order gets new versions; verified count × pinned rate, deadline uses original ClaimPolicy plus actual notice date |
| V3-T06 | First/follow-up MAP/COMBO capture with missing deposit vs unpaid balance | Missing deposit blocks; confirmed deposit + valid operations allows capture without balance/final count |
| V3-T07 | Duplicate/invalid/out-of-order SePay webhook, wrong ref/currency, stale checkout | Verify transport, no fake credit/double deposit; actual receipts reconciled, no client Confirm |
| V3-T08 | Start vs revoke/reschedule/assignment/drone race; Re-flight | Atomic fail or correct start/receipt, no double booking/charge; new attempt/new execution Mission keeps old timestamps/checklist/media, each Re-flight rechecks safety |
| V3-T09 | MAP 60 verified poles, deposit 100.000 | Total=60.000, Due=0, refundable excess=40.000; approved result releases before refund completion; reserve excess once |
| V3-T10 | Manager approve then money confirmation; reverse arrival; both concurrent | Same exact revision released once, one notice/window/original recognition; no second Manager publish required |
| V3-T11 | Inject failure at publication, visibility, notice, window, recognition, completion, Outbox writes | Entire release transaction rolls back; no partially unlocked content/duplicate sale after retry |
| V3-T12 | Stale approved fingerprint/invoice/source revision, late output, corrected checkout | Cannot release mismatched revision; reconcile money without accepting stale result |
| V3-T13 | First MAP imagery → boundary review → verified internal Baseline/profiles before payment | No circular pre-capture orthophoto requirement; initial identity review gated; Owner content locked until release |
| V3-T14 | First COMBO and FastAPI processing retry/tampered source | Internal approved map usable before Owner release; no compulsory second charged flight; only separately input-approved attempt manifest processed without waiting terminal Accepted; wrong manifest/hash/context rejected |
| V3-T15 | Follow-up drift/row-grid match, new/missing/relocated/replaced poles | Stable IDs for accepted matches/relocation; no automatic retire/new profile; later-survey evidence and review for lifecycle; own survey snapshot, Baseline unchanged |
| V3-T16 | Compare MAP→COMBO, COMBO→MAP, compatible/incompatible Combo history | Mapping compatible across services; disease only compatible Combo; NotAssessed on MAP; store basis/reason/revision |
| V3-T17 | Before-capture cancel, weather, no-show, acceptable capture customer cancel, team failure, and cancellation after a full/partial deposit reversal | Refund/reschedule rules BR-CAN; after acceptable capture retain at most deposit/return excess, no remaining balance; failure returns available receipts; reversed cash is excluded from the refund cap and unresolved reversal linkage blocks refund reservation; no auto no-show fees |
| V3-T18 | Boundary near/outside cases, two reviewers create/replace same pole | NeedsReview/original geometry preserved, outside not billable; one active profile/pole under concurrency |
| V3-T19 | Combo ambiguous/non-disease labels, no accepted finding, guidance mismatch | No severity/diagnosis/Healthy fallback; only configured suspected set and cited guidance; exact reviewed source retained |
| V3-T20 | MAP after previous Combo with suspected disease, pending Owner change report | Disease history not cleared; report not mutate profile; unreleased new observations not exposed as Owner latest |
| V3-T21 | Interrupted online upload/notes retry, checksum mismatch, revoked assignment | Resume/retry safely, no duplicate/lost provenance; no offline cache/mutation requirement; revoked caller denied |
| V3-T22 | Notice 14:00 10/11/2026 UTC+7, receive just before/at 00:00 18/11 | Before boundary accepted, at boundary overdue; deadline UTC=17:00Z 17/11; no 168-hour/first-read semantics |
| V3-T23 | Email/consumer retries, unread notice, claim in progress after expiry, substantive vs cosmetic correction | Retries/read don't reset window; timely claim processed after expiry; new substantive revision gets own window, cosmetic not |
| V3-T24 | Manager verification, Admin changes proposal after Owner accepted old version | Old acceptance cannot authorize changed amount; exact new proposal requires new acceptance; evidence/audit retained |
| V3-T25 | Multiple concurrent refunds including 40.000 excess reserve against 100.000 receipts | completed + active reserved ≤100.000; no duplicate transfer/refund; excess not spendable twice for complaint |
| V3-T26 | Bank transfer processing/failure/uncertain result/retry and cancellation refund | Only verified completion posts cash outflow; uncertain transfer reconciled before retry; canceled Order Due=0, obligation persists |
| V3-T27 | Withdraw/replace Baseline/report after Follow-up; complaint refund after release | Old references preserved, current content withdrawn/invalidation; new version/window without new service charge/sale; refund not reopen balance |
| V3-T28 | Cross-tenant/manager-revoked access, locked result through tiles/profile/timeline/latest/cache/download/media URL | Every surface denies unauthorized/unreleased result content; authorized invoice billable count/rate/Total/Due still visible before release, old released history accessible; URL TTL/revocation limits documented/tested |
| V3-T29 | Same month service Total=60.000, receipt=100.000, excess refund=40.000 | service value/net revenue=60.000; receipts=100.000, cash refunds=40.000, net cash=60.000; pending refund not yet cash out |
| V3-T30 | Unpublished cancelled Order retains 100.000 after acceptable capture | service value=0, cancellation retention=100.000, active held deposits=0, Due=0; receipts reflected once in cash |
| V3-T31 | Deposit Oct, publication/balance Nov, complaint refund Dec, corrected report Jan | Service value only Nov, receipts at respective dates, service refund only Dec; Jan correction no sale; as-of balances preserved |
| V3-T32 | Month-end UTC+7 boundary/service filters/Admin Excel vs dashboard | `[start,nextMonth)` correct UTC conversion, pending excluded, matching totals/formula/filter/cutoff; export denied to non-Admin and text formula-safe |
| V3-T33 | Legacy V4/mapping payload/hash replay vs new MAP/COMBO/internal-map semantics | Old golden JSON unchanged; new meaning uses reviewed versioned contract; strict mismatch rejected, Inbox/business dedup/ACK transaction verified |
| V3-T34 | Legacy upgrade inventory, active old Orders/missing provenance/old queues | Explicit disposition, no fake COMBO/payment/notice/profile evidence, no historic migration rewrites; bounded retry/drain/replay/retention evidence |
| V3-T35 | Pilot performance/research runs on documented source-grouped split/hardware | Provisional p95≤2s/review≤30min/count error≤10%/macro-F1≥0.70 measured, RQ1 match/ID-switch and RQ2 class metrics, three seeds, no specialist diagnosis claim |
| V3-T36 | V3 Manager sai farm hoặc assignment bị thu hồi gọi `GET/POST /result-review*` hoặc `/invoice*` | Trả `403 Forbidden` ở Rank 1 trước khi đọc review/receipt hoặc thực hiện replay |
| V3-T37 | V3 Owner sai tenant hoặc gọi `GET /api/v3/tenant-owner/survey-orders/{orderId}/invoice` trước finalize | Trả `404 NotFound` đồng nhất; không lộ tồn tại Order/invoice, không lộ dữ liệu trụ/geometry/findings |
| V3-T38 | Manager gọi `GET /result-review` sau khi attempt đã chuyển `Accepted` sau freeze | Trả `200 OK` kèm exact `CurrentApprovedResultRevisionId`, `CurrentApprovedResultFingerprint`, `OrderVersion` và `ExpectedFreezeTokens` để finalize sau reload |
| V3-T39 | Manager đọc invoice trước và sau finalize, đọc khi reversal cũ hoặc mới thiếu liên kết, hoặc gửi token stale khi freeze/finalize/correction | Trước finalize trả `409 Invoice.NotFinalized`. Funding hợp lệ trả `200` với net đúng. Reversal thiếu cash hoặc liên kết receipt trả `409 InvoiceFinalization.RefundReconciliationHold`. Stale mutation trả `409` với `errorCode` ProblemDetails. |
| V3-T40 | Manager gửi lại cùng `OperationId` + payload giống hệt vs khác payload (`combo-reviews`, `freeze`, `finalize`, `corrections`) | Replay hợp lệ trả `200 OK` giữ receipt gốc với `isReplay = true`; khác payload hoặc khác Order trả `409 Operation.Conflict` |
| V3-T41 | Correction đổi số trụ (với `ExpectedPlantProfileId` và `ReplacementComboReview` nếu COMBO); Owner đọc lại `GET /invoice` | Metadata Owner trả `InvoiceRevisionNumber` mới, count/total/due cập nhật; token revision cũ bị invalid và không lộ COMBO findings/items |
| V3-T42 | Tenant đọc pre-release qua base map compare/analytics, plant-health-map và lifecycle, gồm trụ mới V3 và trụ Legacy bị V3 di dời hoặc thay thế. Thử sai tenant, Manager hết assignment, Manager còn assignment và trụ Legacy đủ quyền. | Tenant không thấy dữ liệu V3 trước release proof. Lifecycle trả `404 Plant.NotFound` cho trụ bị giữ hoặc sai quyền, và `200` cho Manager đúng assignment hoặc người đọc trụ Legacy đủ quyền. |
| V3-T43 | Gọi writer legacy (`ConfirmSurveyOrderPoleCount`, `PlantHealthReviewService.ApproveResult/PublishResult`, `ReconcileSurveyPayment`) cho Order V3 | Bị từ chối (`SurveyOrder.InvalidTransition` / `SurveyOrder.ScopeInvalid` / `OrderNotReady`) không đổi trạng thái V3 |
| V3-T44 | Login tài khoản hệ thống, tài khoản một tenant và tài khoản nhiều tenant qua select-tenant. Reload ứng dụng và để access JWT hết hạn trong lúc gọi API bảo vệ. | Chỉ nhận cookie khi có `session`. Refresh giữ đúng ngữ cảnh, cấp access JWT mới sau reload hoặc expiry. FE tiếp tục trang hiện tại và các request an toàn chờ refresh được gửi lại tối đa một lần. |
| V3-T45 | Gửi hai refresh cùng token, cho cả hai đọc cùng thế hệ trước khi khóa. Sau khi refresh thành công, dùng token cũ từ một request mới. Kiểm lỗi chèn token mới sau khi đã đánh dấu token cũ. | PostgreSQL lưu đúng một token kế tiếp. Request tranh chấp trả `409` và giữ nguyên cookie. Tái sử dụng token cũ trả `401` và thu hồi phiên. Khi chèn lỗi, transaction rollback và token cũ vẫn dùng được. |
| V3-T46 | Refresh tại biên hết hạn, sau logout, đổi/reset mật khẩu, khóa tài khoản, mất membership hoặc mất vai trò hệ thống. Thử logout lặp, refresh đua với thu hồi và chọn tenant bằng selection token cấp trước khi đổi/reset mật khẩu. | Cookie thiếu hoặc phiên mất hiệu lực trả `401`. JWT mới chỉ chứa quyền còn hiệu lực trong scope của phiên. Logout lặp trả `204` và xóa cookie. Phiên đã thu hồi và selection token cũ đều bị từ chối. |
| V3-T47 | Chạy FE web trên domain dự kiến với origin được phép và origin khác. Kiểm cookie, cache header, log và request refresh/logout thiếu marker header hoặc từ origin ngoài allowlist. | Trình duyệt gửi cookie ở luồng được phép. Origin sai hoặc thiếu marker ở refresh/logout trả `403` nếu request đến handler. Cookie có thuộc tính đã chốt, phản hồi phiên dùng `no-store`, refresh token chỉ xuất hiện trong cookie và token được loại khỏi log. Client login cũ vẫn nhận JSON hiện có. |
| V3-T48 | Mở nhiều tab và để JWT hết hạn khi có request đọc và mutation. Gây lỗi mạng hoặc `5xx` ở refresh rồi khôi phục. | FE phối hợp để dùng chung lần refresh, giữ nguyên operation ID khi retry mutation có idempotency, giữ trạng thái khi mạng gián đoạn và tránh vòng lặp `401`. |

### Ca tranh chấp bổ sung cho V3-T46

- Cho login web đọc phiên bản xác thực cũ, rồi commit đổi mật khẩu trước khi login lấy khóa user. Login trả `401` và không ghi phiên hoặc cookie mới.
- Cho quên mật khẩu và đặt lại mật khẩu chạy đồng thời trên một user. Kiểm thứ tự khóa user rồi reset token. Nếu quên mật khẩu thắng, reset token trước đó bị thu hồi. Nếu reset thắng, token đã tiêu thụ giữ nguyên trạng thái và yêu cầu mới có thể cấp token riêng. Email gửi sau commit của yêu cầu cấp token.
- Cho reset mật khẩu và chọn tenant chờ khóa đến đúng mốc hết hạn hoặc trễ hơn. Cả hai trả `401`, giữ mật khẩu và không cấp JWT hay cookie mới. Kiểm chọn tenant với cả hai chế độ có cookie và chỉ dùng access token.

## Verification strategy

- Unit/policy checks cho calendar math, money classification và transitions; không mirror implementation methods đơn thuần.
- PostgreSQL/PostGIS integration cho uniqueness, transactions, as-of finance, row locks/refund cap và boundary geometry.
- Broker/worker integration cho strict contracts, Inbox/Outbox commit-before-ACK, retry/DLQ/source ordering.
- API/security/E2E cho real first/follow-up/reflight/release/claim/correction/export và all-surface visibility.
- Fault injection ở provisioning/release/refund boundaries, không chỉ happy-path mocks.
- No new test execution is claimed here. Historical test counts remain in dated evidence reports. Future release must record actual commands/results/runtime versions and unresolved limitations.
