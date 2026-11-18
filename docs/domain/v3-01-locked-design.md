# V3-01 — Thiết kế dữ liệu, policy, contracts và legacy disposition đã chốt

Decision date: **2026-11-11**. Status: **DesignLocked — SourceAuditComplete — RuntimeNotImplemented**. Chủ dự án giao chốt thiết kế và lập kế hoạch phase kế tiếp; phạm vi vẫn chỉ tài liệu. Không truy vấn database/broker/gateway, không sửa code/schema/config.

Nguồn: [Business Rules](../../Codex-Plan/Business-Rules-v3.md), [master plan và các invariant bổ sung](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md). Decision summary: [ADR 0019](../adr/0019-v3-01-model-contract-and-migration-design.md). Evidence code: [source audit](../operations/v3-01-source-audit.md). V3-02 implementation/DB gate: [checklist](../plans/v3-02-execution-checklist.md). Next phase plan: [V3-03](../plans/v3-03-capture-funding-and-cancellation-implementation-plan.md).

## 1. Quyết định kiến trúc

1. Giữ ASP.NET Core modular monolith, EF Core, PostgreSQL/PostGIS và existing SePay. BE1/BE2 là ownership capability. FastAPI chỉ xử lý ảnh/AI qua authenticated/versioned adapter; không ghi business tables.
2. Extend `Surveys`, `Farms`, `Plants`, `Missions` và cross-module adapters hiện có; không tạo backend/database mới, không generic dynamic rules engine.
3. Mọi giá trị nghiệp vụ có thể cấu hình có version, effective window, lineage và immutable snapshot. Không C# fallback tiền cọc, đơn giá, claim days, spatial tolerance, dataset policy hoặc no-show threshold.
4. Schema, stable protocol identifiers, thuật toán kiểm tra quyền, phép tính tiền và transition guards vẫn là implementation có kiểm thử. Zero-hardcoding không có nghĩa chạy script/quy tắc tùy ý trong DB hoặc biến RBAC thành cấu hình khách tự sửa.
5. Append-only decision/history là authoritative; current status/pointer có thể là projection được cập nhật atomic cùng history. Không overwrite evidence, không hard-delete transaction/version đã tham chiếu. Đóng EffectiveTo một lần có audit là metadata closure đã chốt, không đổi payload tiền/policy của version cũ.

## 2. Catalogue và policy model

Tên entity/table dưới đây là thiết kế mục tiêu đã chọn, chưa tồn tại trừ khi ghi rõ reuse. Dùng schema `survey` cho transactional commercial policy. UUID version ID và `VersionNumber` nghiệp vụ độc lập với xmin/optimistic concurrency token.

| Entity / table target | Quyết định |
|---|---|
| `SurveyService` / `survey.survey_services` — reuse | Giữ existing service IDs/code/history. Add v3 service-definition metadata và scope; MAP/COMBO là hai records mới, không rename legacy PLANT_HEALTH. |
| `SurveyServiceRevision` / `survey.survey_service_revisions` — new | Versioned Name/Description/MapsPoles/ScreensDisease/acceptance metadata; MAP maps=true/screens=false, COMBO maps=true/screens=true. Business capability mapping nằm trong approved data, không suy từ giá hoặc legacy enum. |
| `SurveyServicePrice` / `survey.survey_service_prices` — reuse/extend | Hai price streams theo service ID. Existing immutable amount/window/FK giữ; add explicit VersionNumber/PredecessorVersionId cho v3 versions. |
| `SurveyDepositPolicy` / `survey.survey_deposit_policies` — new | Một global stream cho shared fixed cọc, không per-service deposit. Fields: Amount/Currency/effective window/lineage. |
| `ClaimPolicy` / `survey.claim_policies` — new | CalendarWindowDays, TimeZoneId, ExcludeNoticeDate, deadline mode, substantive-replacement/cosmetic semantics. Demo window 7, VN calendar; không literal D+7 trong business code. |
| `CancellationPolicy` / `survey.cancellation_policies` — new | Before-capture return, retention basis after accepted capture, refundable excess, no-show reschedule allowance và service-failure handling; versioned parameters, validator giữ trong approved MVP semantics. |
| `SpatialPolicy` / `survey.spatial_policies` — new, implement V3-04 | Boundary tolerance/match/grid parameters và policy lineage; values chỉ từ evidence/calibration được approve, không tự bịa số demo để mở pipeline. |
| `BusinessPolicyVersion` / `survey.business_policy_versions` — new | Immutable commercial bundle: permitted service revisions, shared deposit/claim/cancellation versions, currency/pricing mode và policy fingerprint. Price stream resolved ở cùng transaction cho selected service. |
| `BusinessPolicyActivation` / `survey.business_policy_activation` — new | Guard/current active bundle và intake/capture/release enablement declarations. Mọi đổi activation có history, current pointer là projection. Technical readiness chưa đồng nghĩa được nhận tiền hoặc bật public flow. |

Common version fields: `Id`, logical stream key, `VersionNumber`, nullable first-version `PredecessorVersionId`, `EffectiveFrom`, `EffectiveTo`, `CreatedBy`, `CreatedAt`, `ReasonCode`, optional evidence reference, immutable payload fingerprint. Unique stream+VersionNumber; predecessor cùng stream; không cycles. Effective windows `[from,to)` không overlap; exactly one eligible version cho mỗi required dependency hoặc fail closed.

### Legacy enum và VND

- Existing `system.survey_service_type` chỉ chứa PlantHealth/HarvestReadiness. Giữ labels/ordinals và wire contracts cũ; không sửa meanings thành MAP/COMBO.
- Explicit immutable `ContractScope` phân biệt LEGACY/V3 trên affected business records. Missing required v3 snapshot/context không được tự coi legacy hoặc bật compatibility bypass; fail/quarantine và reconcile. Scope server-owned, không client input.
- Chọn **service code + versioned service revision** cho v3, không thêm MAP/COMBO vào legacy enum rồi cho old DTO nhận giá trị không hiểu. Legacy discriminator được nullable riêng cho new v3 service rows; ràng buộc v3 service definition phải đủ thay thế. Old queries luôn filter legacy scope trước khi project nonnullable enum; no null/unknown fallback.
- Không tạo bảng service song song và hai source of truth. Migration phải review mọi use của enum, seed, PostgreSQL mappings và FK trước khi cho phép nullable; legacy rows vẫn giữ discriminator.
- Reuse `numeric(18,2)` để không phá dữ liệu cũ; v3 validates nguyên đồng VND, amount>0. Constraints nguyên đồng áp scoped v3 records, không reject legacy fractions bằng rewrite lịch sử.
- Giá demo 1.000/2.000 và cọc 100.000 là versioned seed/admin data có provenance, không EF model fallback/domain const. Dataset/spatial thresholds không được seed bằng evidence giả.

## 3. Snapshot cadence — resolve đúng một lần cho mỗi nghĩa vụ

| Thời điểm | Resolve/pin | Không làm |
|---|---|---|
| Public catalogue / Request submit | Immutable indicative quote snapshot: service revision, offered rate/deposit/policy IDs, evaluated time; Request revision history | Không lock giá thương mại cho Order chưa tạo; submit không provision |
| Admin approval → Order creation | Active policy bundle + selected service revision/current price + shared deposit + claim/cancellation versions, in same approval transaction | Không lấy request quote cũ làm guaranteed price, không side effects trước config validation |
| Capture job creation | Operational config/checklist/selected imagery manifests và spatial/model/dataset/annotation versions áp dụng cho capture/job | Không re-resolve unit price/deposit/claim policy của Order |
| Mapping/Combo review + invoice | Exact source/review revisions; count/evidence; calculate theo Order price snapshot | Không lấy catalogue giá mới hoặc current Farm inventory thay billable evidence |
| Publication/unlock | Original Order claim-policy snapshot + actual NoticeCreatedAt; compute exclusive deadline, store calculation input/result | Không lấy claim policy mới để rút/ngầm kéo dài window của Order cũ; không compute deadline tại Request |
| Claim verification/proposal/refund | Exact released revision/window, original Order cancellation/claim terms, verified receipts/reservations và accepted proposal version | Không lấy policy đang active để sửa nghĩa vụ đã ký; no reset window on retry |
| Substantive replacement | Same commercial policy snapshot của Order + new revision notice date; store new window | Không bill lại, không dùng latest config để thay quyền claim của giao dịch cũ |

Order commercial snapshot là immutable 1:1 record, không chỉ JSON phụ thuộc mutable catalogue. Lưu FK và resolved amounts/flags/fingerprint. Request quote và Order contract có thể khác; Owner thấy đúng snapshot Order trước khi trả cọc. Nếu config thay giữa request và approval, audit cả quote và accepted snapshot, không silent mutation request history.

### Atomic resolver và lock order

- Tái dùng `SurveyApprovalDbContext`/UoW cho approvals. Không resolve commercial config qua separate `SurveysDbContext` connection ngoài approval transaction.
- Approval/capture-stage resolvers lấy shared lock trên singleton activation guard; config publish/activate lấy exclusive lock cùng guard trước khi close/open affected windows. Sau đó lock version rows theo stable stream/UUID order, rồi resolve với một server timestamp lấy **sau khi có guard lock**.
- Các config publish operations phải theo cùng thứ tự lock và commit old-window closure + new version + activation/history atomic. DB exclusion/uniqueness constraints là backstop, không chỉ application `Any()`.
- Approval khóa Request/idempotency theo seam hiện có rồi guard; config writer không quay sang lock Request/Order. Baseline/release eligibility read khóa trong cùng transaction theo documented order; rollback cả config snapshot/provisioning khi bất kỳ dependency fail.
- Exact request/key replay rechecks authorization và trả original saved response/snapshots; không resolve current config lần nữa. Contract/API version thuộc command fingerprint/scoping, tránh cross-version key collision.

## 4. Order và state/history model

| Entity/table target | Nội dung |
|---|---|
| `SurveyOrder` / existing `survey.survey_orders` | Reuse identity/Tenant/Farm/Request. Add business-policy scope, First/FollowUp intent, nullable legacy fields và explicit baseline basis; no paid-default for mapped Farm. |
| `SurveyOrderCommercialSnapshot` / `survey.order_commercial_snapshots` | Unique OrderId; service revision/code/capabilities, price/deposit/bundle/claim/cancellation IDs + resolved money/policy inputs; server CreatedAt/fingerprint. Initial billable count/Total chưa có. |
| `OrderStateHistory` / `survey.order_state_history` | Append-only transition with event ID, Order revision, old/new state, actor/system source, UTC time, reason/evidence/correlation/causation and predecessor history ID. |
| Existing `SurveyRequestReview` / `survey.survey_request_reviews` | Reuse decision/checklist evidence; extend predecessor/revision where needed, không tạo một review-history table trùng chỉ vì tên plan ví dụ khác. |
| `SurveyRequestQuoteSnapshot` / `survey.request_quote_snapshots` | Request revision + indicative configuration/source time/fingerprint; no commercial commitment. |
| `LegacyDispositionRecord` / `survey.legacy_dispositions` | Append-only assessed artifact IDs, source policy, chosen handling/evidence/reviewer/time, money obligations và migration/cutover receipt. |

Các trục capture/review/payment/publication độc lập. V3-02 creates Order ở AwaitingAppointment với deposit obligation chưa paid; không tạo `ReadyForPaidService`, `ConfirmedPoleCount`, fabricated final price hoặc fake review/release. Existing `RequiresBaselineMapping` không dùng làm skip-deposit switch; v3 intent/basis quyết định first/follow-up.

Current status/optimistic token được update cùng append-only history; nguyên tắc no-overwrite bảo vệ business evidence/history, không yêu cầu full event-sourcing hoặc version-copy cả object graph cho mỗi read.

## 5. CaptureAttempt — chốt seam ngay, implement V3-03

- Order 1–N `CaptureAttempt`; MVP mỗi attempt có một `DroneMission` execution, `MissionId` unique. Không reuse/reset failed Mission cho Re-flight. `AttemptIndex` tăng dưới Order lock, unique `(OrderId,AttemptIndex)`.
- New attempt lưu `SupersededAttemptId`, reason, approval, schedule/assignment/preflight reference và immutable history. Failed/aborted/uploaded/processing events cũ giữ nguyên; stale preflight thay bằng revision/history, không xóa checklist proof.
- Media/GNSS/flight logs gắn `AttemptId`. Job input manifest pin exact attempt/media IDs/checksums. Không load “mọi ảnh của Order” hoặc folder có cả lần bay hỏng.
- **Không tạo vòng lặp chất lượng:** `CaptureInputApproval` là quyết định quality chấp nhận tập ảnh đầu vào sau upload/checksums; `Accepted` terminal của attempt là kết quả sau processing/review. Pipeline chỉ cần approved input manifest, không chờ terminal Accepted do chính pipeline tạo ra.
- State target được làm rõ tại [ADR 0020 / 03-00 ngày 2026-11-13](../adr/0020-v3-capture-execution-and-funding-boundaries.md): Planned → PreflightReady → InFlight → AwaitingUpload → Uploaded → Processing → Accepted / ReflightRequired / FlightFailed; thêm Cancelled chỉ cho attempt chưa execution. Input approval độc lập, append-only. Early obvious capture failure có thể ra ReflightRequired trước full processing, sau khi flight đã kết thúc an toàn; không đổi terminal parent để recover.
- Một job dùng một input manifest đã duyệt; combine imagery từ nhiều captures nếu sau này cần phải là explicit reviewed manifest revision, không implicit mixing. MVP không tự combine attempts.

## 6. Baseline, survey snapshots, invoices và review revisions

| Target | Quyết định |
|---|---|
| `FarmBaseMapVersion` — reuse | Initial approved internal Baseline hoặc reviewed Baseline correction; add predecessor/reason/status history. Không create/supersede Baseline chỉ vì Follow-up mới. |
| `FarmBaselineReference` — new in Farms | Một current approved pointer/Farm và append-only pointer decisions; delivered proof mới làm Follow-up eligible. Historical references vẫn exact version. |
| `SurveyMapSnapshot` — new in Farms | Unique immutable versioned mapping output per Order/result revision, pin Baseline + boundary + input/review manifest. |
| `Plant` / profile + `PlantChangeEvent` — reuse | Current identity projection + immutable changes; existing profile ID giữ khi reviewed match/relocation. After initial Baseline, new/retire/replace cần later-survey evidence. |
| `SurveyInvoiceRevision` / `survey.invoice_revisions` — new | Versioned count/rate snapshot linkage/Total/evidence/finalized status/predecessor; after release correction không sửa original sale hoặc mở thêm balance. |
| Existing `SurveyResult`/health review foundations | Reuse frozen source/review/fingerprint; new result revisions/predecessor support MAP/COMBO và first internal-map context, giữ old V4 rows immutable. |

V3-02 implementation seam: intake và approval dùng chung `V3FollowUpBaselineEligibilityReader`. `IV3BaselineDeliveryProofReader` thuộc Database đọc durable release/withdrawal evidence qua DbContext được truyền vào; approval bắt buộc cùng connection/transaction và khóa các facts được dùng để quyết định. Adapter production hiện trả unavailable vì V3-06 chưa cung cấp release store, nên Follow-up bị từ chối. Không suy ra delivery từ `SurveyOrder.Completed`, `SurveyResult.Published` hoặc activation flags. Khi nối V3-06, proof phải đúng Tenant/Farm/Baseline/source Order, có release ID, financially settled tại release và chưa withdrawn. Legacy giữ query/policy cũ; không có automatic adoption.

Không dùng global current map count tính invoice của Follow-up. Một Farm chỉ một active profile/physical pole dưới concurrency constraints; initial v3 Baseline trên Farm có legacy profiles phải match/review reuse ID, không tạo duplicate identities để đổi policy version.

## 7. Release, claims, refunds và finance seams

| Target | Invariant |
|---|---|
| `SurveyReleaseReceipt` / `survey.release_receipts` | Unique result revision release; exact approval/invoice/payment-policy/source hashes; snapshot paid-at-release proof và PublishedAt=UnlockedAt=NoticeCreatedAt. |
| `SurveyClaimWindow` / `survey.claim_windows` | Unique publication; stored claim-policy version/inputs/local date/zone/deadlineExclusive; immutable calculation, không reset retry. |
| Durable notification record — reuse notification storage | Insert cùng release UoW và authorized recipient snapshot; Outbox email là delivery bổ sung. |
| `SurveyClaim`, verification/proposal/acceptance revisions | Exact publication/window/evidence, Admin proposal + Owner acceptance; history immutable. |
| `SurveyRefundObligation` + reservation/completion receipts | Explicit reason; receipt cap toàn Order, active reservation under lock; manual bank transfer proof, no blind transfer retry. |
| `SurveyFinancialEntry` / `survey.financial_entries` | Typed immutable receipts/refund/retention/service recognition facts; unique original service recognition per Order, corrections no second sale. |

Không dựng một general accounting subsystem. Reuse existing `SurveyPayment`/`PaymentEvent` verified transport facts; allocations/sale/refund liabilities là separate domain decisions. Complaint refund không xóa paid-at-release evidence hoặc tái tạo receivable. Exact physical schemas/indexes cho các phase V3-06/08/09 được triển khai tại phase tương ứng, không create toàn bộ tables rỗng ở V3-02.

## 8. Contracts và HTTP cutover đã chọn

Business policy version không là wire version. Các tên/routing dưới đây **đã chọn cho target design, chưa tồn tại/deploy**; trước code phase phải tạo typed schemas/validators theo fields dưới đây, không sửa old released records.

| Family target | Schema / routing intent | Consumer scope |
|---|---|---|
| `SurveyOrderCaptureContextV5` | Internal synchronous port revision; no queue | Deposit/attempt/input/baseline/authorization; replace target assumptions của existing V4, giữ V4 legacy |
| `SurveyMappingCandidatesReadyV1` | schemaVersion 1; `survey.mapping-candidates-ready.v1` | MAP/COMBO pending mapping, first or Follow-up |
| `ComboAnalysisReadyV1` | schemaVersion 1; `survey.combo-analysis-ready.v1` | Combo-only pending disease/zones/guidance, approved internal map allowed |
| `SurveyResultReleasedV1` | schemaVersion 1; `survey.result-released.v1` | Persisted exact release/notice/window/recognition proof; delivery/projections |
| `SurveyResultWithdrawnV1` | schemaVersion 1; `survey.result-withdrawn.v1` | Withdrawn/replacement links and cache/access invalidation |

Không ép mapping/health làm một event vì first Combo cần approved internal mapping trước disease matching; hai pending families share source/attempt/policy identity. Inbox+business dedup theo job/handoff/revision/fingerprint, không chỉ MessageId. Strict schema validation trước writes, audit+Inbox atomic, ACK sau commit.

HTTP chọn **additive `/api/v3/...`** cho catalogue/admin config/request/approval/order DTO mới. Không gửi MAP/COMBO hoặc nullable service legacy discriminator qua old enum DTO. Old `/api/...` snapshots không sửa; giữ legacy historical reads, old pending writes theo approved disposition, gateway callback endpoint cũ giữ nguyên và dispatch qua persisted payment→Order policy scope.

Idempotency fingerprint/scoping phân biệt v3 command với legacy nhưng unique request→approved Order constraint vẫn toàn hệ thống. Public tracking v3 dùng token hash-at-rest; token không URL/log, response không contact/financial/result PII.

Public v3 activation vẫn **disabled** sau V3-02 cho đến capture/release/visibility/claims/refund và required rollout acceptance đã đạt. Có internal test fixtures cho isolated phase, không nhận deposit tiền thật cho flow chưa complete.

## 9. Legacy disposition — không tự đổi dữ liệu cũ

Chưa inventory database thực tế. Các nhóm và action dưới đây là kế hoạch đã chọn; số lượng/artifact IDs/cutoff/money references phải được read-only preflight xác minh trước migration/cutover.

| Nhóm | Default action |
|---|---|
| Completed/published legacy Orders | Read-only legacy policy, giữ tiền/map/review/event hashes. Không fabricate v3 unlock notice/window hoặc recognize sale lần hai. |
| Pending request chưa Order | Không auto-convert PlantHealth→COMBO. Admin reissue/review thành v3 request với explicit service/quote/customer acknowledgement, link predecessor; old request closed with reason. |
| Active legacy Order chưa tiền/chưa capture | Giữ original policy tới recorded disposition; ưu tiên cancel/rebook bằng v3 sau acknowledgement, preserve request/history, không đổi snapshots tại chỗ. |
| Active legacy đã nhận tiền/capture/review | Không auto-migrate/rebook hoặc reset flight. Admin quyết định finish theo existing terms hoặc cancel/settle/refund có evidence; receipts và nghĩa vụ không bị copy thành tiền mới. |
| Legacy Baseline/profiles | Không automatic Follow-up eligibility. Verified adoption cần original approved paid-map/delivery/identity evidence và explicit disposition; thiếu evidence thì cần first paid v3 baseline, reviewed matching reuse existing profile IDs. |
| Jobs/events/queues legacy in flight | Drain/replay bằng old validators/schema/receipts; new v3 channels riêng. Không đổi payload trên hàng đợi để pass schema mới. |
| Missing provenance, disputed/uncertain money/failed transfer | Quarantine case-level writes, Admin reconciliation/audit; giữ relevant payment callbacks/evidence access. Không backfill fake actors/payments/thresholds. |

Compatibility scope gắn immutable Order policy/disposition, không dựa current catalogue. Config change không kéo active legacy Orders vào new policy; không global fallback nếu old snapshot thiếu.

## 10. Migration/rollout sequence

1. Read-only schema/data/broker inventory: duplicates/current enums/triggers/FKs/policy windows/legacy Order categories and finance facts. Không chạy SQL trong design phase này.
2. Expand scoped columns/constraints/new policy tables; old rows giữ legacy markers/provenance. Thêm version lineage ở records mới; legacy predecessor unknown không bịa.
3. Seed demo/config records có creator/reason, publish new versioned bundle/intake disabled; verify config consistency/overlap constraints.
4. Implement/test v3 catalogue/approval behind activation, then V3-03–09 adapters/processing/release/claims/finance. Compatible new consumers trước producers, separate shadow queue nếu dùng.
5. Review explicit legacy disposition và actual DB/broker evidence; bật public v3 chỉ khi end-to-end accepted. Disable new legacy intake tại cutover nhưng giữ callbacks/reconciliation cho tiền cũ.
6. Drain/replay/retention gates trước contract cleanup. Không destructive migration/down khi đã có financial/publication facts; ưu tiên roll-forward.

## 11. V3-01 exit record

- Đã chốt service/config/snapshot/lineage/attempt/Baseline/invoice/release/finance seams, HTTP/event families và legacy handling.
- Đã đọc source và chỉ rõ gaps trong [audit](../operations/v3-01-source-audit.md); chưa xác nhận live DB inventory hoặc chạy runtime checks.
- V3-01 **DesignLocked**, không đánh implementation/migration Done. Live preflight/typed schema validation là gates tương lai, không là lý do để tiếp tục hỏi lại business decisions đã giao chốt.
- Bước kế: **V3-02A — catalogue/policy persistence foundation**, theo [kế hoạch chi tiết V3-02](../plans/v3-02-catalogue-and-approval-implementation-plan.md). Chưa sửa code trong phiên này.

Cập nhật tiến độ 2026-11-13, không sửa exit record lịch sử ở trên: V3-02 có implementation nhưng còn DB acceptance pending. [V3-03 technical plan](../plans/v3-03-capture-funding-and-cancellation-implementation-plan.md) và [execution checklist](../plans/v3-03-execution-checklist.md) đã sẵn sàng; implementation V3-03 chưa bắt đầu. Không thay policy/snapshot cadence hoặc biến Completed/Published thành release proof.
