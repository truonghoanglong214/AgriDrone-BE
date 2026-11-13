# BE1 — V3 Business Rebaseline và .NET Implementation Plan

Ngày cập nhật: **2026-11-13**. Business policy **Approved**; V3-01 **DesignLocked / SourceAuditComplete**; V3-02 **Implementation present / DB acceptance pending**; V3-03 **03A-1 domain remediation verified, 03A-2 contracts implemented; next 03B-1 persistence/expand migration**. Đây là kế hoạch mục tiêu; xem checklist từng phase cho bằng chứng triển khai. PostgreSQL acceptance còn pending, Follow-up production chờ release proof V3-06 và public activation vẫn tắt.

Sources: [proposal v3](Be-Plan-v3.md), [approved rules](Business-Rules-v3.md), [BE1 use cases](BE1-Use-Case-Specification.md), [BE2 use cases](BE2-Use-Case-Specification.md), [ADR 0018](../docs/adr/0018-v3-paid-map-combo-business-policy.md).

## Lịch sử sửa đổi tài liệu (Document Revision History)

| Phiên bản | Ngày cập nhật | Người thực hiện | Nội dung sửa đổi chính & Lý do điều chỉnh |
|---|---|---|---|
| **v3.0** | 2026-11-10 | Product Owner / Tech Lead | Phê duyệt V3 Business Rebaseline: 2 gói thương mại MAP/COMBO, cọc chung trước bay, số dư thanh toán trước release, Follow-up trả phí trên Baseline bàn giao, 1 Baseline duy nhất, Manager Mobile online-only. |
| **v3.1** | 2026-11-10 | System Architect | Khóa chặt hai nguyên tắc kiến trúc bắt buộc: (1) **Zero-Hardcoding Invariant**: Tuyệt đối không hardcode bất kỳ tham số tiền tệ, đơn giá, cọc, ngưỡng dung sai không gian, hạn khiếu nại hay version chính sách trong mã nguồn; tất cả phải là cấu hình động có phiên bản (`Versioned Configuration`) và được snapshot nguyên tử vào giao dịch; (2) **Full Version Lineage & Immutable History Invariant**: Mọi chỉnh sửa dữ liệu và trạng thái quan trọng (cấu hình, trạng thái đơn, quyết định duyệt, bản đồ, định danh cây, số trụ, giám định bệnh, khiếu nại, hoàn tiền) bắt buộc lưu vết lịch sử bất biến với người sửa, thời điểm, lý do, bằng chứng và liên kết phiên bản tiền nhiệm (`PredecessorVersionId`), cấm ghi đè hoặc xóa cứng. |
| **v3.2** | 2026-11-10 | System Architect | Chuẩn hóa mô hình Quản lý Lượt bay (CaptureAttempt / FlightAttempt Lifecycle): Cấm triệt để việc ghi đè hoặc xóa trắng dữ liệu chuyến bay cũ (`RecoverFailedFlight`). Mọi lần bay lại (Re-flight) bắt buộc sinh `CaptureAttempt` mới (1-N với Order), bảo toàn nguyên vẹn raw evidence, telemetry, checklist và media của lần bay cũ. Phân lập media theo attempt để bảo vệ AI/Metashape pipeline; bay lại chất lượng cùng Order không phát sinh thêm chi phí. |
| **v3.3** | 2026-11-11 | Thiết kế theo ủy quyền chủ dự án | Chốt V3-01: model/policy snapshot cadence/lineage, input approval khác terminal Accepted, additive v3 HTTP/event contracts và legacy disposition. Lập V3-02A–F với file manifest/DB-concurrency acceptance. Chỉ tài liệu, chưa implementation hoặc live DB inventory. |
| **v3.4** | 2026-11-13 | Lập kế hoạch theo yêu cầu chủ dự án | Lập V3-03 A–H/19 work items: all-Order deposit, appointment, atomic capture start, new attempt/Mission cho quality Re-flight, attempt-scoped input approval và durable cancellation obligations. Cập nhật đúng V3-02 DB acceptance pending; giữ Follow-up release-proof unavailable và public activation off. Không sửa code trong phiên lập kế hoạch này. |
| **v3.5** | 2026-11-13 | Thực hiện 03-00 theo yêu cầu chủ dự án | Hoàn thành execution baseline và ADR 0020: W01–W26 writer inventory, additive Order/attempt states, canonical Mission binding, total lock order, funding/cancellation IDs và exact next-slice manifest. Chỉ source/design acceptance; không chạy code/DB tests hoặc thay backend. |

## 1. Kết quả cần đạt

```text
MAP/COMBO request → approval + price/deposit snapshots → appointment + deposit
→ safe authorized capture → processing/review → verified count/finalized invoice
→ remaining balance → atomic publish/unlock/notice/service value
→ Baseline/Follow-up/profile history → claims/correction/refund → revenue/Excel
```

First survey và Follow-up đều paid Order. Quality Re-flight cùng Order không thêm phí. Demo rates Map/Combo 1.000/2.000 VND/trụ, common deposit 100.000 VND, cấu hình có version động. Online-only Manager Mobile. .NET owns business state; FastAPI owns imagery/AI processing; existing SePay retained.

## 2. Hai nguyên tắc kiến trúc bắt buộc (Architectural Invariants)

### 2.1. Nguyên tắc Zero-Hardcoding & Snapshot cấu hình nguyên tử
- **Cấm tuyệt đối hardcode**: Không sử dụng hằng số `const`, `static readonly`, literal code hay hardcoded switch/enum mapping cho bất kỳ tham số nghiệp vụ nào (đơn giá Map/Combo, mức tiền cọc, thời hạn khiếu nại $D+7$, khoảng cách đệm ranh giới, ngưỡng dung sai spatial bucketing, tỷ lệ giữ cọc khi hủy, v.v.). Các giá trị 1.000 VND, 2.000 VND, 100.000 VND chỉ là dữ liệu cấu hình demo ban đầu (initial seed data), không phải logic cố định của runtime.
- **Cấu hình động có phiên bản (Versioned Dynamic Configuration)**: Toàn bộ tham số nghiệp vụ phải được quản lý thành các thực thể cấu hình trong cơ sở dữ liệu (`SurveyDepositPolicy`, `SurveyServicePrice`, `ClaimPolicy`, `SpatialPolicy`) với các thuộc tính: `Id`, `Version`, `EffectiveFrom`, `EffectiveTo`, `CreatedBy`, `CreatedAt`. Quản trị viên (SystemAdmin) cập nhật tham số thông qua việc ban hành phiên bản cấu hình mới, đóng phiên bản cũ bằng `EffectiveTo = ServerTimeUtc`, bảo toàn 100% dữ liệu lịch sử cấu hình.
- **Snapshot nguyên tử bất biến tại thời điểm phát sinh (Atomic Snapshots)**: Resolve cấu hình do giao dịch đó sở hữu theo [snapshot cadence đã chốt](../docs/domain/v3-01-locked-design.md). Request lưu indicative quote; Order creation pin commercial price/deposit/bundle/claim/cancellation versions. Capture/job pin operational/spatial/model input policy. Invoice, release, claim và refund dùng lại terms của Order; chỉ release mới tính deadline từ notice date thực tế và ClaimPolicy snapshot. Không resolve current price/claim policy ở mỗi bước để đổi giao dịch cũ. Mọi snapshot có ID/value/fingerprint bất biến trong cùng transaction với business facts.
- **Phạm vi zero-hardcoding**: cấm runtime fallback cho tunable business parameters. Schema/protocol identifiers, thuật toán tính tiền/calendar, authorization và transition guards vẫn là implementation có kiểm thử; không xây arbitrary script/rules engine trong DB. Các trạng thái/pointer current là projection được update atomic với append-only history, không bắt full event sourcing.

### 2.2. Nguyên tắc Lưu vết lịch sử toàn vẹn (Full Version Lineage & Immutable History)
- **Cấm ghi đè và cấm xóa cứng (No In-place Overwrite & No Hard-delete)**: Không xóa bản ghi hay cập nhật đè làm mất dữ liệu lịch sử. Mọi thay đổi dữ liệu hoặc trạng thái quan trọng phải được lưu vết append-only hoặc sinh phiên bản mới:
  1. *Trạng thái giao dịch (Order, Request, Payment, Claim)*: Mọi chuyển đổi trạng thái phải lưu vào bảng lịch sử giao dịch (`OrderStateHistory`, `SurveyRequestReviewHistory`, `PaymentAuditHistory`, `RefundAuditHistory`), lưu rõ `ActorId`, `ActorRole`, `ChangedAtUtc`, `OldState`, `NewState`, `ReasonCode`, `ReasonDescription`, `EvidenceReference`, `CorrelationId`.
  2. *Dữ liệu không gian và bản đồ*: First Baseline hoặc reviewed Baseline correction tạo `FarmBaseMapVersion` có lineage/reason; Follow-up tạo `SurveyMapSnapshot` riêng, không tự sinh/supersede current Baseline. Correction giữ `PredecessorVersionId`/withdrawal decision; mọi old source references và geometry được giữ để đối soát.
  3. *Định danh cây trồng (Plant Profiles)*: Mỗi trụ có một `PlantProfileId` duy nhất. Mọi biến động qua các đợt khảo sát (tạo mới, dịch chuyển vị trí do GNSS, thay thế, hủy kích hoạt) bắt buộc phát sinh bản ghi `PlantChangeEvent` bất biến lưu rõ nguồn ảnh/khảo sát, tọa độ cũ/mới, lý do, bằng chứng và Manager phê duyệt.
  4. *Hóa đơn và số lượng trụ*: Nếu có điều chỉnh số lượng trụ sau review hoặc sau giải quyết khiếu nại, phát sinh `InvoiceRevision` mới, không ghi đè hóa đơn cũ.
  5. *Kết quả phát hiện bệnh (Combo)*: Mọi điều chỉnh nhãn bệnh, vùng bệnh hoặc hướng dẫn xử lý sau khiếu nại phải sinh `SurveyResultRevision` mới, phiên bản cũ được gắn cờ `Withdrawn` làm căn cứ giải quyết tranh chấp.
  6. *Lượt bay và Nhiệm vụ bay (Capture Attempts & Quality Re-flights)*: Bãi bỏ hành vi gọi `RecoverFailedFlight` để reset/xóa timestamps hoặc checklist. Order 1-N CaptureAttempt; MVP mỗi attempt có execution Mission riêng. Re-flight tạo AttemptIndex mới/parent reason/approval, giữ nguyên telemetry/checklist/media cũ. Metashape/AI pin exact attempt/media manifest đã có `CaptureInputApproval`; input-quality approval khác terminal `Accepted` sau processing/review để tránh circular dependency. Không trộn ảnh các attempts bằng query toàn Order.

## 3. Baseline hiện có và khoảng cách với v3

Các source paths dưới đây đã được đọc trong audit; trạng thái chỉ mô tả baseline hiện tại, không khẳng định đã sửa.

| Thành phần hiện có | Tái dùng | Delta v3 |
|---|---|---|
| Surveys/Domain/Catalogue/SurveyServiceType.cs và SurveyServiceMvpPolicy.cs | Versioned catalogue infrastructure | Hiện PlantHealth-only; thiết kế MAP/COMBO, không reinterpret service cũ; zero hardcoding |
| Surveys/Domain/Orders/SurveyOrder.cs | Order transitions, snapshots, concurrency | Xóa hằng số DefaultBaselineDepositAmountVnd (5.000); cọc dùng chung có version; snapshot đơn giá và cọc lúc duyệt |
| Surveys/Domain/Orders/SurveyOrderReadinessPolicy.cs | Authoritative operational evaluation | Thay full-paid gate bằng deposit gate; settlement là release gate; mọi chuyển đổi lưu OrderStateHistory |
| Surveys/Application/Features/Approvals | Atomic onboarding/idempotency | Resolve price/deposit versions trong approval transaction; Follow-up same Farm + released Baseline, cross-service mapping |
| Surveys/Application/Features/Orders/ConfirmSurveyOrderPoleCount | Count/pricing transaction seam | Count từ reviewed evidence của Order thay current inventory mặc định; unit price đã snapshot trước đó; lưu InvoiceRevision |
| Database/Mapping/FarmBaseMapPublicationOrchestrator.cs | Review batch, provenance, profile mapping | Tách internal approved Baseline, survey snapshots và Owner publication; không supersede Baseline mỗi Follow-up; lưu FarmBaseMapVersion lineage |
| Database/Health/PlantHealthReviewService.* | V4 pending/review/frozen approval/audit | Combo target context, first internal map; separate approval from finance-gated release; audit đầy đủ reviewer/reason |
| Database/Health/PlantHealthReviewService.Publication.cs | Atomic exact-revision publication receipt | Hiện manual publish + Complete Order; thêm settlement/notice/window/revenue gates theo new policy; release revision bất biến |
| Missions/Domain/Missions/DroneMission.cs và RescheduleOrderMission | Missions/Flight orchestration | Bãi bỏ RecoverFailedFlight xóa trắng dữ liệu; chuyển sang mô hình CaptureAttempt đa phiên bản (1-N); attempt-scoped media bảo vệ AI pipeline |
| Integrations/AgriDrone.Integrations.SePay | QR/reference/webhook adapter | Giữ transport, sửa application allocations/obligations trong phase code tương lai |
| backend/services/metashape-service | FastAPI photogrammetry/tiling baseline | Formalize authenticated/versioned job boundary, durable .NET source receipt/retry/reconciliation |
| FarmBaseMapVersion, Plant mapping/change reports | Version history/stable IDs | One Baseline pointer + each Order snapshots; PlantChangeEvent bất biến cho mọi biến động cây; Map disease neutrality |
| Claims/refund workflow/revenue/unlock | Một số payment refund/adjustment seams | Thêm explicit aggregates/read models/contracts theo phase; lưu lịch sử đề xuất, chấp nhận và chứng từ chuyển khoản |

Paths tương đối với `backend/src` trừ khi ghi đầy đủ. Bảng trên mô tả baseline tại checkpoint rebaseline; đối chiếu checklist V3-02/V3-03 và source hiện tại để biết delta đã triển khai. Runtime Step 7 còn dùng được mô tả trong [Plant Health contracts](../docs/contracts/plant-health-v4/README.md), không phải v3 acceptance.

## 4. Status và dependency roadmap

| Phase | Scope | Dependency | Status v3 / exit gate |
|---|---|---|---|
| V3-00 | Business rules, traceability, documentation inventory | Quyết định chủ dự án | Documentation complete khi audit links/coverage pass; không runtime acceptance |
| V3-01 | Policy/context/contracts và legacy disposition | V3-00 | **DesignLocked / SourceAuditComplete 2026-11-11**; chưa runtime/live DB inventory; xem locked design/ADR 0019 |
| V3-02 | Catalogue/shared deposit + request/approval snapshots | V3-01 | **Implementation present / DB acceptance pending**; xem execution checklist, public activation off |
| V3-03 | Appointment/deposit/capture/Re-flight/cancellation | V3-02, BE2 readiness port | **03A-1 domain verified, 03A-2 contracts implemented; next 03B-1**; persistence, API, atomic start và DB acceptance còn pending; xem execution checklist |
| V3-04 | Mapping/Baseline/survey snapshots/profile identity | V3-01, V3-03 source capture | Planned; first approved internal map, Follow-up không overwrite Baseline, PlantChangeEvent lineage |
| V3-05 | Combo pending/review và invoice finalization | V3-04, processing contract | Planned; complete reviewed result + verified count, no severity, InvoiceRevision |
| V3-06 | Settlement, overpayment, atomic release và notice | V3-02/04/05 | Planned; published=unlocked=notice, one sale/Order, rollback/idempotency |
| V3-07 | Owner portal/media/timeline/comparison | V3-06 release proof | Planned; all result surfaces enforce release/ownership/withdrawal |
| V3-08 | Claims/refund/correction/cancellation settlement | V3-06, storage/reconciliation | Planned; 7-day windows, negotiated refund/cap/reservation, immutable corrections/withdrawal lineage |
| V3-09 | Revenue/cash/dashboard/Excel | V3-06/08 ledger facts | Planned; monthly as-of and Dashboard=Excel |
| V3-10 | Final migration/cutover, acceptance/research/demo | Relevant previous phases | NotStarted; no runtime release without evidence |

Phases phản ánh dependency, không cấm phát triển độc lập adapters sau khi ports đã review. Không trì hoãn invariant/security tests tới cuối; meaningful phase checks cần trước integration. Comprehensive E2E chạy ở V3-10.

## 5. V3-01 — Policy model, contracts và migration design

- Tách ServiceType MAP/COMBO, survey intent First/Follow-up, capture role/attempt và commercial Order khỏi legacy MissionPurpose. Không dùng service enum làm tất cả các trục.
- Quản lý cấu hình động: Loại bỏ toàn bộ enum-dependent rates, hằng số cọc hay hardcoded policy logic. Mọi tham số đều có Entity cấu hình với phiên bản (`PolicyVersion`), hiệu lực theo thời gian `[EffectiveFrom, EffectiveTo)`.
- Review context references: Tenant/Farm/Order, business-policy version, Baseline/boundary/internal capture-map/survey snapshot versions, invoice/review revisions, price/deposit versions và receipt IDs.
- Frozen approved revision có source fingerprint; target release receipt có paid-at-release proof/notice/deadline/recognition identity.
- [Target contract](../docs/contracts/survey-business-v3.md) đã chốt family names/routing intent theo ADR 0019, chưa declared/deployed. Released Mapping V3/Health V4 giữ nguyên; breaking enum/context requires new schema and queue/cutover plan.
- Inventory all old Orders/payments/maps/queues; phân loại completed historical, active unmapped, active mapped, missing provenance, outstanding refunds. Không suy đoán COMBO/free-paid evidence từ legacy service name.

**Đã chốt thiết kế 2026-11-11:** [locked model/contracts/disposition](../docs/domain/v3-01-locked-design.md), [ADR 0019](../docs/adr/0019-v3-01-model-contract-and-migration-design.md), [source audit/gaps](../docs/operations/v3-01-source-audit.md). New HTTP contracts dùng `/api/v3/...`; legacy enum/payload/money giữ nguyên. Claim/cancellation snapshot pin tại Order creation; later stages không re-policy. Live inventory, generated SQL/schema validation và contract tests vẫn là gates tương lai, không đánh migration/runtime Done. Đợt này không tạo/chạy SQL.

## 6. V3-02 — Catalogue và atomic request approval

Implementation areas: Surveys catalogue/requests/approvals, Database approval transaction adapters, public/admin contracts.

Kế hoạch thực thi: [V3-02 catalogue và atomic approval](../docs/plans/v3-02-catalogue-and-approval-implementation-plan.md), chia A persistence/policies → B publish/catalogue → C requests/tracking → D same-transaction snapshot/approval → E API/DI → F migration/testing/handoff. Implementation hiện có; public intake/checkout vẫn off đến full-flow acceptance.

[Checklist thực thi V3-02](../docs/plans/v3-02-execution-checklist.md) theo dõi 13 work items và evidence theo đợt. 02A-2 database acceptance, 02D-3 và 02F-1 vẫn pending; unit/component pass không thay PostgreSQL upgrade/locking/rollback. Follow-up production fail closed đến khi có durable release-proof adapter của V3-06, không suy từ Completed/Published.

- Versioned shared deposit policy (`SurveyDepositPolicy`) và hai price streams (`MAP`, `COMBO`); activation validation chống overlapping windows và nonpositive/noninteger VND. Cấm hardcode mức cọc hay đơn giá trong code.
- Public tracking code non-enumerable, minimal status/decision, rate limiting. Existing owner requests áp Tenant/Farm predicates server-side.
- Approval transaction resolve price/deposit snapshot tại creation, đúng request kind provision set, assignment/invitation/audit/Outbox/idempotency receipt cùng commit. Mọi quyết định duyệt lưu `SurveyRequestReviewHistory` kèm `ActorId`, `TimestampUtc`, `Decision`, `ReasonCode`.
- Follow-up chỉ trên delivered Baseline; same Farm, MAP hoặc COMBO, không duplicate Tenant/Farm hoặc bắt cùng previous service.
- Seed demo giá (1.000, 2.000, 100.000) tách khỏi runtime constant khi triển khai code tương lai; lưu lịch sử thay đổi cấu hình catalogue, không sửa money snapshots quá khứ.

Exit: snapshot/catalogue race, approval rollback/idempotency, invalid configs và tracking privacy có checks thực tế.

## 7. V3-03 — Capture, funding, Re-flight và cancellation

Implementation areas: Surveys appointment/payment/readiness/cancellation policy, Missions planning/start, cross-module start coordinator.

Kế hoạch chi tiết: [V3-03 capture/funding/cancellation A–H](../docs/plans/v3-03-capture-funding-and-cancellation-implementation-plan.md), [execution checklist 19 work items](../docs/plans/v3-03-execution-checklist.md). Bắt đầu **03-00 inventory/status/lock ownership → 03A-1 CaptureAttempt domain/history**; 03A-1 chưa đổi schema, callback hoặc Mission recovery. Domain/contracts có thể phát triển trên isolated fixtures khi V3-02 DB acceptance còn pending, nhưng không ký integration acceptance/go-live thay prerequisite.

**03-00 đã hoàn thành ngày 2026-11-13:** [execution baseline + W01–W26/source hashes/file manifest](../docs/operations/v3-03-execution-baseline.md), [ADR 0020 status/lock/funding/obligation decisions](../docs/adr/0020-v3-capture-execution-and-funding-boundaries.md). Source/design acceptance, không runtime acceptance; bước tiếp là 03A-1. CaptureAttempt dùng thêm AwaitingUpload và Cancelled trước execution để làm rõ trạng thái, không thay commercial policy.

- Deposit applied cho mọi Order; balance không nằm trong capture gate. Appointment-first, first capture không cần orthophoto/boundary chưa sinh ra.
- Source-based readiness không nhận derived payment/approval state từ client. Atomic start rechecks Order/appointment/assignment/deposit/drone reservations/preflight/safety/authorization.
- Mọi chuyển đổi trạng thái đơn hàng (từ `AwaitingAppointment` đến `Completed` hoặc `Cancelled`) bắt buộc ghi nhận vào bảng lịch sử `OrderStateHistory` lưu vết `OldStatus`, `NewStatus`, `ActorId`, `Reason`, `Evidence`.
- **Mô hình hóa Lượt bay (CaptureAttempt Lifecycle)**: `Planned` → `PreflightReady` → `InFlight` → `AwaitingUpload` → `Uploaded` → `Processing` → `Accepted` HOẶC `ReflightRequired` / `FlightFailed`; `Cancelled` chỉ trước execution, theo ADR 0020. Cấm triệt để việc ghi đè (no in-place overwrite) hay gọi `RecoverFailedFlight` để xóa trắng dữ liệu chuyến bay cũ (`StartedAt=null`, `EndedAt=null`, xóa câu trả lời checklist).
- **Chính sách Bay lại chất lượng (Quality Re-flight)**: Lượt bay mới tham chiếu cùng `SurveyOrderId`, cấp `AttemptIndex` mới, trỏ `SupersededAttemptId` về lượt bay cũ kèm `ReflightReason` (ảnh mờ, lỗi thiết bị, thời tiết xấu). Không tạo price/deposit mới; không nhân đôi chi phí hóa đơn.
- **Phân lập Media theo Lượt bay (Attempt-Scoped Media)**: Media/GNSS gắn AttemptId; job pin exact immutable input manifest có quality approval sau upload/checksums. Không đòi terminal Accepted trước pipeline, không trộn ảnh nhiều lần bay ngầm. Accepted terminal chỉ ghi sau processing/review theo state machine.
- Cancellation transitions có captured-quality evidence và reason. Before capture full return; customer hủy sau acceptable capture giữ tối đa deposit/return excess; team failure full return. No-show một free reschedule, weather không phạt.
- Cancel terminal closes remaining receivable, giữ refund obligation và auditable retention classification.

Exit: start/revoke race, reservation overlap, duplicate attempt/charge, cancellation refund cap, missing flight evidence checks và attempt-scoped media isolation checks.

## 8. V3-04/05 — Mapping, identity, Combo và count/invoice

Implementation areas: Farms/Plants mapping review, Surveys result/approval/invoice, Missions/FastAPI orchestration, Database publication adapters.

- First approved paid mapping có internal Baseline/profile records; Owner still locked. Matching/disease processing trên internal approved map không yêu cầu prior customer publication/full balance hoặc paid second flight.
- Baseline pointer khác per-Order survey map snapshot. Follow-up giữ `BaselineVersionId` và historical comparison basis; no automatic baseline overwrite. Bản đồ cũ được quản lý version lineage rõ ràng (`FarmBaseMapVersion`), trỏ `PredecessorVersionId`.
- VerifiedBillablePoleCount lấy current Order reviewed in-boundary evidence. Boundary exceptions phải resolve trước count, original geometry không overwrite; no current inventory shortcut.
- Quản lý định danh cây trồng (Plant Identity): First Baseline tạo 1 `PlantProfileId` duy nhất cho mỗi trụ vật lý. Mọi biến động qua các đợt khảo sát (tạo mới, dịch chuyển toạ độ do GNSS, thay thế, hủy kích hoạt) BẮT BUỘC phát sinh bản ghi `PlantChangeEvent` bất biến, lưu toạ độ cũ/mới, nguồn ảnh, lý do và quyết định duyệt của Manager. Cấm tự động thay đổi lifecycle profile.
- Combo no-severity suspected observations/zones/guidance giữ V4 review foundations; MAP ghi NotAssessed/no health clearance. Negative Combo không được gọi certified healthy.
- Invoice finalization dùng already-snapshotted rate. Count correction trước release versioned; sinh `InvoiceRevision` mới, không ghi đè hóa đơn cũ; invalidate old checkout obligation, reconcile late receipts. Zero poles exceptional review, không artificial minimum charge.

Exit: first MAP/COMBO, cross-service Follow-up, identity/row-grid uncertainty/boundary cases và immutable source/review revision checks.

## 9. V3-06 — Atomic settlement/release

New seams dự kiến: release coordinator, release receipt/window, invoice/payment reconciliation, refund excess reservation và service-value journal. Tên class/table cụ thể chốt trong implementation ADR, không mở thêm module chỉ để giống diagram cũ.

Transaction bắt buộc:

1. Lock Order/current approved result/invoice/payment context; replay trả exact existing receipt sau current access check.
2. Recheck approved fingerprint, finalized Total, Due=0, assignment/context và not cancelled/withdrawn/stale.
3. Commit immutable publication/visibility, durable in-app notice, PublishedAt=UnlockedAt=NoticeCreatedAt, deadlineExclusive (tính từ cấu hình hạn khiếu nại động `ClaimPolicyVersion`), one original service-value entry/Order, completion/audit/Outbox.

Release triggered từ cả approval và money confirmation; không manual publish lần hai sau webhook. Refund excess reservation không block nếu Due=0. Post-release complaint refund không reopen receivable. Asynchronous email consumers không là nguồn notice timestamp hoặc quyền access.

Exit: failure injection giữa mỗi write rolls back; approval/callback/invoice races, double revenue/window/notice và stale revision checks. No release claims từ compile-only evidence.

## 10. V3-07 — Owner delivery và online Manager workflow

- Shared authorization/release policy cho map/tiles/profiles/timeline/latest health/comparison/report/media/export URLs; DB predicates trước result materialization/cache access.
- Pending new Order không lộ qua current/latest aggregate projection của Farm; old released history vẫn available.
- Separate mapping/Combo comparison basis with NotAssessed và stored reasons. Withdrawn versions không còn current downloads, vẫn có history/claim context hợp lệ.
- Online-only Manager checklist/notes/media retry/resume; no offline cache/mutation queue implementation. Recheck revoked assignment, version conflicts, object token TTL và exact visibility cache invalidation.
- FE/mobile contracts dùng server-returned prices/statuses/due/deadlines; target endpoints không được quảng cáo live trước implementation.

Exit: all access surfaces/tenant isolation/signed URL/cache invalidation and interrupted-upload safety checks, không chỉ controller result endpoint.

## 11. V3-08 — Claim/refund/correction

- Claim window revision-specific: notice D → deadlineExclusive D+8 00:00 VN (tính động theo cấu hình `ClaimPolicyVersion`, ví dụ 7 ngày lịch); server ReceiveAt decides, email retry/read không reset. Timely claim xử lý tiếp sau expiry.
- Manager evidence verification, Admin proposal, Owner exact version acceptance; substantive corrected publication/new window, cosmetic no reset. Giữ nguyên toàn bộ lịch sử tranh chấp (`ClaimStateHistory`, `ClaimEvidenceAudit`).
- Khi hiệu chỉnh kết quả giám định hoặc bản đồ sau khiếu nại: Phát sinh phiên bản mới (`SurveyResultRevision`, `FarmBaseMapVersion`), gắn cờ phiên bản cũ là `Withdrawn` với `WithdrawalReason` và `PredecessorVersionId`. Tuyệt đối không xóa hoặc ghi đè kết quả bị lỗi.
- All-Order refund cap + concurrent reservation cho Overpayment/CancellationReturn/ServiceFailure/ComplaintRefund; pending/failed không Completed. Transfer retry đối soát bank reference để tránh double transfer.
- Bank-proof storage authorization/audit; no client-controlled completion, no automatic gateway refund giả định. Retention protects evidence khi open claim/correction.
- Corrections không charge thêm hoặc rewrite original invoice/service revenue; repayment/liability changes explicit ledger facts, no false arrears after complaint refund.

Exit: deadline boundary, owner/admin concurrency, repeated refunds/changed proposal/bank-proof retry, correction access/history and one active Baseline/profile checks.

## 12. V3-09 — Financial reporting

- Shared reporting query theo BR-FIN-01–05 và snapshot MAP/COMBO. Recognition key một original sale/Order; corrections no second sale.
- Net service revenue chỉ trừ refunds hoàn published service value; net cash trừ all completed refunds. Overpayment/cancellation return chưa từng là revenue không trừ sale lần nữa.
- Report separate receipts, refunds by reason, deposit liabilities, refundable excess, retained cancellation deposits, outstanding finalized balances, pending/unpriced items và legacy/unclassified reconciliation.
- UTC+7 `[start,nextMonth)` conversion, historical as-of events cho month end, not current status arithmetic. Service filter/Excel/dashboard cùng query/formula version; Admin-only export/audit/formula-safe text.

Exit: fixtures arithmetic across months/service/revisions, reconciliation receipts vs allocations/refunds/liabilities; same report fingerprint/filters for dashboard and Excel.

## 13. Transaction boundaries và compatibility

| Boundary | Atomic facts |
|---|---|
| Request approval | Request state + appropriate Tenant/Farm/Order/assignment/invitation + price/deposit/policy snapshots + idempotency/audit/Outbox |
| Capture start | Current Order/appointment/assignment/deposit/drone reservation + Mission/CaptureAttempt transition/receipt/audit/Outbox + OrderStateHistory |
| Quality Re-flight | Đóng băng Attempt cũ (ReflightRequired/FlightFailed) + sinh CaptureAttempt mới kèm SupersededAttemptId/ReflightReason + không đổi invoice/deposit |
| Pending ingestion | Inbox receipt + full candidate/source validation + pending records/audit; no half-import ACK |
| Manager approval | Exact reviewed revisions/internal mapping/profile lifecycle decisions and frozen approval receipt; no premature Owner publication |
| Release | Publication + visibility + paid proof + notice/window + original recognition + Order completion + audit/Outbox |
| Refund reserve | Receipt cap and all active reservations under lock + proposal/acceptance/obligation/audit |
| Refund completion | Verified transfer proof + immutable completion + reason-classified reporting facts/audit; bank network outside SQL transaction |
| Correction | Withdrawal/replacement relations/review and revision-specific visibility/window; no historical evidence overwrite; PredecessorVersionId linkage |

Không distributed transaction với gateway/FastAPI/bank/email. Persist intents/receipts, bounded retries/reconciliation/compensation. Released contracts giữ nguyên; không sửa historical migration, published JSON fixtures hoặc Swagger snapshots để làm chúng giống target.

## 14. V3-10 — Acceptance, migration và release

- Áp [acceptance matrix](../docs/testing/v3-business-acceptance.md) và [traceability](../docs/domain/v3-traceability-matrix.md). Test meaningful invariants/races/rollback/DB/broker/media access; no tests chỉ mirror implementation.
- Future migration sequence: inventory/read-only preflight → expand policy/references/ledger fields → explicit legacy disposition/backfill evidence thật → compatible consumers → producer cutover → accepted API/UI → drain/replay/retention → reviewed contract cleanup.
- Không rewrite old enum/money/source IDs; không “backfill” approval/payment/notice timestamps giả để mở v3 Orders. Roll-forward ưu tiên khi đã có financial/publication facts.
- Research deliverables giữ RQ1 GNSS vs GNSS+grid/ID switches, RQ2 team-labelled class metrics, split by original flight/source trước augmentation, three-seed comparable runs, no expert diagnosis claim.
- Pilot targets provisional: p95 read API ≤2s, review session ≤30min, pole count error ≤10%, macro-F1 ≥0.70; đo trên dataset/hardware đã công bố, không tự đánh pass từ chưa đo.
- Demo first MAP/COMBO → follow-up → quality re-flight → excess/complaint refund → corrected report → monthly report/Excel, bảo vệ raw evidence.

## 15. Documentation acceptance và tracking

V3-00 chỉ hoàn tất khi canonical rules/plans/ADR/glossary/contracts/API intent/handoff/diagrams/test matrix được liên kết đúng, legacy artifacts được gắn status, và diff không chứa source/config/migration changes. Runtime capabilities V3-01–10 vẫn Planned/NotStarted cho đến evidence cụ thể.

Historical completion reports không sửa nội dung test counts/run dates. [Rebaseline report](../docs/operations/v3-documentation-rebaseline-report.md) liệt kê phạm vi thay đổi và phần chưa được thực hiện. Không chạy build/test/migrations hay gateway requests chỉ để sửa tài liệu.
