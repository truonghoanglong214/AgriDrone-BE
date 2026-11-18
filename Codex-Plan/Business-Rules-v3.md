# AgriDrone — Business Rules v3

Status: **Approved business policy — implementation pending**. Decision date: **2026-11-10**. Source: [proposal v3](Be-Plan-v3.md) và bộ phương án chủ dự án chấp thuận cùng ngày. Đây là đặc tả mục tiêu; lần cập nhật tài liệu không thay đổi runtime.

## Lịch sử sửa đổi tài liệu (Document Revision History)

| Phiên bản | Ngày cập nhật | Người thực hiện | Nội dung sửa đổi chính & Lý do điều chỉnh |
|---|---|---|---|
| **v3.0** | 2026-11-10 | Product Owner / Tech Lead | Ban hành bộ Business Rules v3 chuẩn hóa: 2 gói trả phí MAP/COMBO, cọc trước bay, balance trước release, Follow-up trả phí, 1 Baseline duy nhất, Manager Mobile online-only. |
| **v3.1** | 2026-11-10 | System Architect | Bổ sung quy tắc bắt buộc: (1) `BR-CFG-01` (Zero Hardcoding): Không hardcode bất kỳ tham số tiền tệ, đơn giá, cọc, ngưỡng dung sai không gian, hạn khiếu nại hay version chính sách; tất cả là cấu hình động có version; (2) `BR-AUD-01` (Version Lineage & Immutable History): Bắt buộc lưu lịch sử bất biến cho mọi chỉnh sửa/thay đổi trạng thái quan trọng, cấm ghi đè hoặc xóa cứng. |
| **v3.2** | 2026-11-10 | System Architect | Chuẩn hóa mô hình Quản lý Lượt bay (CaptureAttempt / FlightAttempt Lifecycle): Cấm triệt để việc ghi đè hoặc xóa trắng dữ liệu chuyến bay cũ (`RecoverFailedFlight`). Mọi lần bay lại (Re-flight) bắt buộc sinh `CaptureAttempt` mới (1-N với Order), bảo toàn nguyên vẹn raw evidence, telemetry, checklist và media của lần bay cũ. Phân lập media theo attempt để bảo vệ AI/Metashape pipeline; bay lại chất lượng cùng Order không phát sinh thêm chi phí (`BR-CAP-01`, `BR-CAP-02`, `BR-CAP-03`). |
| **v3.3** | 2026-11-11 | Theo ủy quyền chủ dự án | Khóa snapshot cadence BR-CFG-02, projection/history BR-AUD-02 và làm rõ input-quality approval của BR-CAP-03 để không circular dependency; chốt V3-01 model/contracts/disposition và V3-02 plan, chưa sửa code. |

## 1. Actors, scope, configuration và ownership

| Rule | Quyết định |
|---|---|
| BR-ACT-01 | User actors: Public Applicant, SystemAdmin, SystemManager, TenantOwner. AI, payment gateway và worker là supporting systems. |
| BR-ACT-02 | TenantOwner chỉ truy cập Tenant/Farm của mình. Assigned SystemManager active mới được thực hiện field work/review; Admin assignment/reassignment có audit. |
| BR-ACT-03 | .NET là business source of truth. FastAPI xử lý ảnh/inference, chỉ trả artifacts và candidates có provenance; không tự tạo official Plant, thu tiền hoặc publish. |
| BR-ACT-04 | MVP giữ SePay cho payment integration. BE1/BE2 là phân công capability, không phải hai backend Java/.NET hoặc lý do tạo thêm database nghiệp vụ. |
| BR-SCP-01 | Hai service tokens mục tiêu là `MAP` và `COMBO`. Map có mapping/count/positions; Combo thêm 2–3 team-labelled suspected diseases, Disease Zones và cited guidance. |
| BR-SCP-02 | Không disease-only, severity, certified diagnosis, Harvest Readiness, spraying, workforce/yield, tenant-operated/autonomous flight hoặc offline mode. |
| BR-SCP-03 | Flights cần điều kiện an toàn và authorization áp dụng. Permit/operator certificate không là Capstone deliverable không có nghĩa bỏ yêu cầu vận hành áp dụng. Không dựng thêm administrative workflow không có yêu cầu. |
| BR-CFG-01 | **Zero Hardcoding**: Tuyệt đối không hardcode giá trị cấu hình nghiệp vụ (giá Map/Combo, tiền cọc, claim days, dung sai không gian, khoảng cách đệm, ngưỡng an toàn, dataset policy) dưới dạng const/static readonly/runtime literal fallback. Các tham số có version/effective windows và Admin quản lý; stable protocol identifiers, calculation algorithms, authorization/transition guards vẫn là implementation có kiểm thử, không thành arbitrary DB scripts. |
| BR-AUD-01 | **Version Lineage & Immutable History**: Mọi thay đổi dữ liệu hoặc trạng thái quan trọng (cấu hình, trạng thái đơn hàng, quyết định duyệt yêu cầu, ranh giới nông trại, bản đồ cơ sở, hồ sơ cây trồng, chốt số trụ, kết quả bệnh, khiếu nại, hoàn tiền) BẮT BUỘC phải lưu vết lịch sử bất biến. Bắt buộc có thông tin: `ActorId`, `TimestampUtc`, `OldState/OldValue`, `NewState/NewValue`, `ReasonCode`, `EvidenceReference`, và liên kết phiên bản tiền nhiệm (`PredecessorVersionId`). Cấm tuyệt đối xóa cứng (`hard delete`) hoặc ghi đè trực tiếp (`in-place overwrite`). |

## 2. Request, Order và immutable snapshots

| Rule | Quyết định |
|---|---|
| BR-ORD-01 | NewCustomer request chỉ provision Tenant/Farm/Order/assignment/invitation khi Admin approval thành công; submit không provision. Approval phải atomic và idempotent. |
| BR-ORD-02 | ExistingTenantNewFarm tái sử dụng Tenant/Owner; first survey của Farm mới vẫn là paid Order đầu tiên. |
| BR-ORD-03 | Follow-up tạo Order mới trong cùng Tenant/Farm, chọn Map hoặc Combo. Chỉ nhận khi Farm có Baseline đã bàn giao từ Order đầu tiên; không tạo trùng Tenant/Farm. |
| BR-ORD-04 | Khi tạo Order, resolve và snapshot service token, service-price version, PricePerPole, DepositAmount, deposit-policy version, VND và business-policy version. Missing/overlapping configuration làm creation fail trước mọi side effect. |
| BR-ORD-05 | Snapshot giá/cọc không đổi khi catalogue đổi. Follow-up lấy version đang hiệu lực lúc tạo Order mới, không có discount hoặc Follow-up price tier. |
| BR-ORD-06 | Estimated pole count là tham khảo. VerifiedBillablePoleCount chỉ chốt sau khảo sát/review, có source Order/Mission, boundary version, evidence, reviewer/time. Không lấy current inventory thay cho evidence của Order. |
| BR-ORD-07 | Một Order có nhiều Mission/capture attempts nếu cần. Không bắt buộc bay Baseline rồi thu đủ balance để mới được bay disease service. Các bước xử lý trên cùng imagery không tự sinh phí hoặc Order mới. |
| BR-ORD-08 | Follow-up snapshot BaselineVersionId lúc nhận Order; thay Baseline sau đó không sửa ngược reference. Mapping comparison và disease comparison có basis riêng, không dùng duy nhất PreviousCompatibleOrderId cho mọi nghĩa. |

## 3. Giá, tiền cọc và thu tiền

| Rule | Quyết định |
|---|---|
| BR-PRC-01 | Demo catalogue đã chốt: Map 1.000 VND/trụ, Combo 2.000 VND/trụ, cọc chung 100.000 VND/Order. Đây là demo data, chưa sửa seed/config và chưa xác định giá thương mại thực tế. |
| BR-PRC-02 | Giá/cọc có version và effective window; cọc là một cấu hình chung, không cấu hình độc lập cho Map/Combo/Follow-up. Giá/cọc phải dương và nguyên đồng VND; dùng decimal, không float. |
| BR-PRC-03 | Total = VerifiedBillablePoleCount × PricePerPoleSnapshot. Không có minimum service charge ngầm bằng tiền cọc. Verified billable poles phải ở trong approved FarmBoundary của khảo sát. |
| BR-PAY-01 | Mọi first/follow-up Order phải thanh toán cọc trước capture; appointment, assigned manager, drone/preflight/safety và authorization vẫn phải hợp lệ. Balance không là gate trước flight. |
| BR-PAY-02 | .NET quyết định purpose/amount/currency/reference; Owner không gửi giá hoặc tự Confirm payment. Chỉ webhook được xác thực hoặc Admin reconciliation có bằng chứng xác nhận tiền thực nhận. |
| BR-PAY-03 | Giao dịch và callback lưu append-only, dedup provider transaction/event ID và business key; duplicate không credit cọc hai lần. Thiếu/thừa/sai reference/currency phải được đối soát, không silently đổi invoice. |
| BR-PAY-04 | Trước publication: ConfirmedNetPaid = confirmed receipts − completed refunds; RemainingDue = max(Total − ConfirmedNetPaid, 0); Overpayment = max(ConfirmedNetPaid − Total, 0). Chỉ tiền đã nhận/hoàn thực tế mới tham gia công thức. |
| BR-PAY-05 | Nếu Total < cọc, RemainingDue=0 và phát sinh Overpayment refund, không cần claim. Review đạt + không còn phải thu cho phép release dù refund tiền thừa chưa hoàn tất. Số tiền đang giữ chỗ hoàn không được tạo nghĩa vụ hoàn trùng. |
| BR-PAY-06 | Sau publication, ComplaintRefund không đảo ngược payment allocation để tái tạo balance hoặc tự khóa kết quả. Full-payment evidence tại release là lịch sử bất biến; withdrawal vì kết quả sai là quyết định riêng. |
| BR-PAY-07 | Ví dụ: Map 60 trụ, cọc 100.000 → Total=60.000, Due=0, Overpayment=40.000. Mở kết quả sau review, hoàn 40.000 có chứng từ; giá trị dịch vụ vẫn 60.000. |
| BR-PAY-08 | Zero verified poles là exceptional review, không tự tính 1 trụ hoặc tự giữ cọc. Xác minh Farm không có trụ hay capture thất bại; nếu không có dịch vụ bàn giao/đơn dừng thì hoàn đủ, không publish kết quả giả để ghi nhận doanh thu. |
| BR-PRC-04 | Trước release, corrected count cần versioned review và revised invoice audit; không đổi đơn giá/cọc đã snapshot. Không mở hai checkout balance còn hiệu lực cho hai invoice revisions; tiền đến muộn phải đối soát theo receipts thật. |

## 4. Capture, hủy và Re-flight

| Tình huống | Rule và xử lý |
|---|---|
| Chưa capture, khách hủy | BR-CAN-01: hoàn toàn bộ tiền đã nhận, cancel balance nghĩa vụ còn lại. |
| Weather/safety/authorization chưa phù hợp | BR-CAN-02: đổi lịch giữ cọc; nếu khách không tiếp tục trước capture thì hoàn đủ. Không phạt vì điều kiện vận hành. |
| Khách no-show, chưa capture được | BR-CAN-03: một lần đổi lịch miễn phí; nếu hủy trước capture thì hoàn đủ. Lần tiếp theo cần Admin xử lý, không tự thêm no-show fee. |
| Đã capture đạt yêu cầu, khách đổi ý hủy | BR-CAN-04: giữ tối đa cọc snapshot từ tiền đã nhận, không thu balance chưa thanh toán; tiền nhận vượt cọc hoàn lại. Admin xác nhận evidence/reason và chủ thể yêu cầu hủy; không cho tự đánh dấu capture đạt. |
| Ảnh không đạt chất lượng / Bay lỗi | BR-CAP-01: quality Re-flight cùng Order, không thêm phí dịch vụ/cọc, có reason và approval, bảo toàn nguyên vẹn attempt cũ. Mỗi lần bay vẫn kiểm tra an toàn/authorization. |
| Đội không thể bàn giao và hai bên dừng | BR-CAN-05: hoàn toàn bộ receipts còn lại; không giữ cọc với lý do đã bay khi service không thể bàn giao. |

BR-CAN-06: trạng thái terminal Cancelled đóng balance, nhưng không xóa payments/refund obligations/audit. Tiền giữ do hủy là `CancellationRetention`, báo cáo riêng với giá trị Map/Combo và cọc đang giữ cho đơn đang thực hiện. Không ghi nhận nó như published service value.

BR-CAP-02: một capture mới cho Re-flight tham chiếu Order gốc, mang số hiệu Attempt mới (`AttemptIndex`) và lý do supersede capture attempt (`SupersededAttemptId`, `ReflightReason`); tuyệt đối không overwrite media/GNSS/job source hoặc reset xóa trường dữ liệu chuyến bay cũ (`StartedAt=null`, xóa checklist). Follow-up khách mua để quan sát thời điểm mới phải là Order mới, không dùng Re-flight để né thanh toán.

BR-CAP-03: Media/GNSS/checklist gắn từng AttemptId; MVP mỗi new Re-flight attempt có execution Mission riêng. Job chỉ đọc exact immutable attempt/media manifest đã có CaptureInputApproval sau upload/checksums. Input-quality acceptance độc lập terminal Accepted sau processing/review, không bắt pipeline chờ trạng thái do chính nó tạo. Không query toàn bộ ảnh Order hoặc trộn attempts ngầm.

## 5. Baseline, profile và observations

| Rule | Quyết định |
|---|---|
| BR-MAP-01 | Approved first paid survey map tạo Baseline nội bộ và một active PlantProfileId cho mỗi verified physical pole; phải truy nguyên source Order/Mission/boundary/capture/reviewer/version. Không có free Baseline service. |
| BR-MAP-02 | Internal Approved khác customer Published. Khi chưa release, Owner không xem Baseline/profile/mapping/health từ đơn này; pipeline Combo có thể dùng approved internal mapping để tiếp tục xử lý. Không yêu cầu customer-published Baseline trước khi xử lý first Combo. |
| BR-MAP-03 | Mỗi Farm tối đa một current approved Baseline pointer. Follow-up có survey map snapshot riêng; không supersede Baseline mặc định khi hoàn thành. |
| BR-MAP-04 | Mapping/Baseline sai phải withdrawal/correction → reviewed replacement version có predecessor/reason/evidence. Giữ old version và historical references; không rewrite capture provenance của Follow-up trước đó. |
| BR-PLT-01 | Match đã review giữ PlantProfileId; observations append-only, có observed/captured time, source Order/Mission, media/GNSS/model/review và map version. |
| BR-PLT-02 | Near/outside-boundary, uncertain/new/missing matches là NeedsReview. Không tự tạo/retire/replace profile hoặc tự kéo coordinate sang Farm khác. Outside approved boundary không tính phí. |
| BR-PLT-03 | Sau initial Baseline, new/retired/replaced profiles cần later-survey evidence và Manager approval. Owner report chỉ tạo pending; không trực tiếp đổi lifecycle. Missing một lần không tự retire. |
| BR-PLT-04 | Một physical pole tối đa một active profile; replacement tạo ID mới và link predecessor, giữ old inactive/retired history. Verified relocation do sai lệch GNSS giữ ID. |
| BR-HLT-01 | Chỉ Combo ghi disease screening observations sau review. Không severity/diagnosis hoặc free-form AI advice; guidance có source/version và team-label limitations. |
| BR-HLT-02 | Map-only tuyệt đối không clear disease history hoặc sinh Healthy. Combo không có accepted finding cũng không đồng nghĩa certified disease-free; absence/not-assessed phải diễn đạt riêng. |
| BR-CMP-01 | Mapping comparison cho Map↔Map, Map↔Combo, Combo↔Combo nếu boundary/identity/coverage phù hợp; lưu basis/reason/version. Disease comparison chỉ giữa các Combo tương thích về labels/model semantics/evidence. |
| BR-CMP-02 | API lưu Baseline basis, prior mapping basis và prior Combo-health basis riêng. Không so disease trên Map-only; dữ liệu không khảo sát hiển thị NotAssessed. Không chọn một future/newly corrected reference làm giả lịch sử đã publish. |

## 6. Review, publication, visibility và notification

BR-REL-01: Manager duyệt đúng immutable result revision với review fingerprint; unresolved blocking matches/boundary exceptions/required findings không cho approve. Combo cần mapping và disease review; Map không phải đợi disease processing.

BR-REL-02: Manager approval là authorization cho hệ thống release đúng revision khi tài chính đủ. Sau review đạt và finalized Total, hệ thống tự thử release khi có đủ tiền; không bắt Manager bấm duyệt lần hai khi payment callback đến.

```text
Approved revision + Finalized invoice + RemainingDue=0 + valid authorization/context
    → one transaction: immutable publication + visibility unlock + durable in-app notice
      + claim window + service-value entry + Order completion + audit + Outbox
```

BR-REL-03: `ApprovedAt` có thể trước `PublishedAt`. Trong MVP `PublishedAt=UnlockedAt=UnlockNotificationCreatedAt`, do server cấp trong transaction thành công. Email/push chỉ delivery bổ sung. Event consumer gửi notice sau đó không quyết định mốc claim. Transaction rollback không mở bất kỳ artifact nào.

BR-REL-04: cùng transaction recheck approved revision, invoice revision, payments và assignment, tránh approval/payment/correction race. Khóa/idempotency chống release và revenue trùng. Approval stale phải review lại, không release revision khác.

BR-REL-05: tất cả Owner-facing map/profile/timeline/comparison/report/download/media URLs đều kiểm tra ownership + released source + not withdrawn; cache không bypass. Farm/order status, invoice/payment và pending change/claim status có thể xem theo quyền. Trước release, invoice được hiển thị VerifiedBillablePoleCount, đơn giá, Total và Due để khách kiểm tra tiền; ngoại lệ billing metadata này không mở per-pole details, geometry, disease findings hoặc các result aggregates khác. Existing released history tiếp tục xem được khi Order mới còn locked.

BR-REL-06: withdrawal không xóa bản cũ; Owner xem trạng thái thu hồi và claim history, không tiếp tục tải erroneous report như current. Signed URL TTL/revocation và cache invalidation phải nằm trong implementation plan.

## 7. Claims, correction và refund

| Rule | Quyết định |
|---|---|
| BR-CLM-01 | Claim áp dụng cho fully paid/released Order, chứa dated evidence, loại lỗi và result/Baseline revision. Manager xác minh media, GNSS, AI originals và review history; Admin quyết định đề xuất refund, Owner chấp nhận. |
| BR-CLM-02 | Ngày thông báo D theo Asia/Ho_Chi_Minh không là ngày thứ nhất; nhận claim đến hết ngày D+7. Lưu deadlineExclusive = 00:00 ngày D+8 UTC+7, convert UTC; valid nếu server ReceivedAt < deadlineExclusive. |
| BR-CLM-03 | Ví dụ notice 14:00 10/11/2026 UTC+7 → nhận đến hết 17/11; deadlineExclusive là 00:00 18/11 UTC+7 (17:00Z 17/11). Tính calendar dates, không cộng 168 giờ. |
| BR-CLM-04 | In-app notice durable tại release là mốc chuẩn, không phải lần đọc hoặc email delivery. Retry/dedup notification không reset hạn. Claim gửi đúng hạn được xử lý đến khi đóng kể cả quá deadline. |
| BR-CLM-05 | Correction thực chất có Manager review tạo replacement publication/notice/window riêng cho phiên bản mới. Cosmetic edit không tạo window mới. Withdrawal không xóa claim đã nhận. Claim gắn đúng revision, không hồi sinh window của old revision. |
| BR-RFD-01 | Refund reasons: Overpayment, CancellationReturn, ServiceFailure, ComplaintRefund. Mọi refund trả bằng manual bank transfer có proof, actor/time/transaction reference và audit. Overpayment/cancellation không bị chặn bởi cửa sổ claim. |
| BR-RFD-02 | ComplaintRefund cần versioned Admin proposal + Owner acceptance đúng amount/currency/reason. Đổi đề xuất làm acceptance cũ mất hiệu lực; Manager không tự duyệt số tiền. Không tự tính severity-based/fixed percentage refund. |
| BR-RFD-03 | Sum(CompletedRefunds)+Sum(ReservedPendingRefundAmounts) ≤ confirmed receipts của Order. Reserve phần đã approve/Owner accept; exclude rejected/cancelled obligation. Kiểm tra dưới concurrency lock để hai Admin không hoàn vượt tiền. |
| BR-RFD-04 | Overpayment obligation được reserve ngay khi xác định hợp lệ, tránh claim dùng cùng tiền. Bank transfer initiation/failure không được ghi Completed; retry phải đối soát bank reference trước chuyển lại. |
| BR-RFD-05 | Completed refund immutable, không ghi đè proof/reference; sai phải append correction/reconciliation có audit. Complaint refund không charge lại, mở lại balance hoặc làm mất paid-at-release evidence. |
| BR-COR-01 | Correction/Re-flight do lỗi service cùng Order, không thu thêm phí hoặc tăng giá đơn đã bàn giao. Nếu count/report sai, giữ original invoice và corrected evidence; negotiated ComplaintRefund ghi riêng, không sửa doanh thu lịch sử ngầm. |

## 8. Revenue, cash và month-end

Các chỉ tiêu sau là định nghĩa báo cáo sản phẩm, không tuyên bố thay thế hệ thống kế toán. Mọi tháng/filter dùng Asia/Ho_Chi_Minh (UTC+7), truy vấn theo `[monthStartUtc, nextMonthStartUtc)`.

| Chỉ tiêu | BR-FIN-01: công thức và ngày ghi nhận |
|---|---|
| Published service value | Tổng Total của original service publication trong tháng; một lần/Order tại PublishedAt. Correction/republication không tạo sale lần hai. |
| Completed service refunds | Phần refund hoàn giá trị dịch vụ đã publication, thường ComplaintRefund; ghi tháng CompletedAt. |
| Net service revenue | Published service value − completed service refunds trong tháng. Refund của sale tháng trước có thể làm tháng hiện tại âm. |
| Cash receipts | Deposits + balance + các khoản nhận đã xác thực, theo transaction date; không pending, không cộng lại cọc khi thu balance. |
| Cash refunds | Tất cả completed bank refunds theo CompletedAt, bao gồm overpayment/cancellation/service refund. |
| Net cash | Cash receipts − cash refunds. |
| Cancellation retention | Tiền cọc thực giữ do khách hủy sau capture đạt; phân loại riêng, không published service value, không deposit liability cho active Order. |
| Deposits held | Phần cọc còn phân bổ vào dịch vụ của active unpublished Orders tại cuối tháng; exclude completed refunds, khoản đã phân loại refundable excess và cancellation retention. Nếu chưa chốt Total thì giữ theo cọc thực nhận; khi đã chốt phải tách excess thành liability riêng, không cộng hai lần. |
| Outstanding balances | AmountDue của active finalized-invoice Orders tại cutoff; cancelled/withdrawn fee waiver theo settlement không để balance giả. Pending/unpriced hiển thị riêng, không ước lượng thành receivable. |

BR-FIN-02: NFR7 có chữ “completed refunds” được làm rõ theo bản chất tiền: overpayment/cancellation return chưa từng là service value không được trừ lần nữa khỏi net service revenue; tất cả vẫn trừ net cash. Ví dụ invoice 60.000, nhận 100.000, trả thừa 40.000 → service revenue=60.000, net cash=60.000; refund complaint thêm 20.000 → revenue=40.000, net cash=40.000.

BR-FIN-03: approved/pending refund chưa làm giảm cash/revenue, nhưng reservation/refund payable hiển thị để đối soát. Không tính refund như tiền thu âm rồi lại trừ lần nữa.

BR-FIN-04: transaction time, publication time và effective month-end history phải lưu đủ để chạy báo cáo quá khứ, không dùng current status để thay số dư cuối tháng. Cancellation recognition tại thời điểm Admin chốt retention, không phải payment date. Service filter dùng snapshotted MAP/COMBO; legacy unknown hiển thị riêng.

BR-FIN-05: Admin dashboard và Excel dùng cùng query/filter/cutoff, có version công thức và audit export; chỉ Admin được xem/export. Excel chỉ dùng dữ liệu được phép, xử lý formula injection trong text, không lộ beneficiary bank details không cần thiết.

## 9. State model mục tiêu

Không gộp mọi trạng thái thành một enum Order dài. Các trục dưới đây là semantic target, tên runtime phải được thiết kế/migrate riêng.

| Trục | Trạng thái/điều kiện |
|---|---|
| Request | Submitted → UnderReview → Approved / Rejected / Cancelled |
| Fulfillment | AwaitingAppointment → AwaitingDeposit → ReadyForCapture → Capturing → Processing → PendingReview → ApprovedAwaitingSettlement → Completed; Cancelled chỉ qua cancellation policy |
| Capture attempt | Planned → PreflightReady → InFlight → Uploaded → Processing → Accepted / ReflightRequired / FlightFailed; 1-N với Order, sinh Attempt mới khi Re-flight, giữ nguyên toàn bộ attempt cũ và dữ liệu media/telemetry độc lập |
| Review | PendingReview → Approved; stale revision phải re-review |
| Customer publication version | NotReleased → Published; Published → Withdrawn / Superseded khi replacement được review/released |
| Payment obligation | Deposit / Balance, có provider attempts Pending / Confirmed / Failed; không gắn full payment vào preflight |
| Claim | Submitted → UnderVerification → AwaitingAdminDecision → AwaitingOwnerAcceptance / Rejected → RefundPending / Resolved → Closed |
| Refund | Proposed → Reserved → Processing → Completed; Rejected/Cancelled giải phóng reserve; Failed giữ nghĩa vụ cho reconciliation, không coi đã trả |

## 10. Integrity và acceptance

- Positive paths: first Map/Combo, Follow-up Map/Combo, free quality Re-flight, overpayment release, negotiated refund và replacement report.
- Negative paths: unpaid capture, unreviewed/unpaid result access qua bất kỳ projection/media URL nào, wrong tenant/assignment, out-of-boundary billing, duplicated payment/release/refund, stale approval, two active profiles.
- Race paths: approve/payment cùng lúc; invoice change/callback; Baseline correction/Follow-up; concurrent refund reservation/transfer; duplicate email/notice.
- Financial paths: cross-month receipts/refund/publication, refund excess không giảm service revenue, cancellation retention riêng, re-publication không ghi sale lần hai, dashboard=Excel.
- [Acceptance matrix](../docs/testing/v3-business-acceptance.md) và [traceability](../docs/domain/v3-traceability-matrix.md) là gate release. Evidence v2 không tự chứng minh v3 Done.

## 11. Thiết kế triển khai được chốt ngày 2026-11-11

[V3-01 locked design](../docs/domain/v3-01-locked-design.md) và [ADR 0019](../docs/adr/0019-v3-01-model-contract-and-migration-design.md) cụ thể hóa các invariant được bổ sung trong master plan. Không đổi các mức giá demo hoặc chính sách thương mại đã chốt, chưa sửa runtime.

- **BR-CFG-02:** Request pin indicative quote; Order approval atomically pin service/price/shared deposit/bundle/claim/cancellation snapshots. Invoice/release/claim/refund reuse pinned Order terms; actual deadline tính tại release từ notice date và ClaimPolicy snapshot, không lấy claim policy mới.
- **BR-AUD-02:** append-only history/decision/evidence authoritative; current state/pointer là projection update cùng transaction. Existing review entities được extend, không duplicate history tables chỉ vì tên ví dụ khác. No hard delete/version payload overwrite; EffectiveTo closure một lần phải có audit.

BR-CFG-01 và BR-CAP-03 được làm rõ tại định nghĩa gốc về phạm vi dynamic parameters và quality input acceptance; không tạo hai định nghĩa rule trùng ID.

V3-02 thực thi theo [kế hoạch A–F](../docs/plans/v3-02-catalogue-and-approval-implementation-plan.md). Public activation vẫn off cho đến required end-to-end gates; design lock không là code/DB rollout completion.
