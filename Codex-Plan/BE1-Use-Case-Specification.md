# BE1 — Core Business Use Cases theo v3

Approved specification: **2026-11-10**. Implementation: **RebaselineRequired; tài liệu không thay code**.

Nguồn chuẩn: [proposal](Be-Plan-v3.md) → [Business Rules](Business-Rules-v3.md) → [ADR 0018](../docs/adr/0018-v3-paid-map-combo-business-policy.md). [Implementation plan](BE1-Core-Business-Implementation-Plan.md) là backlog, không ghi đè phạm vi nghiệp vụ.

## 1. Phạm vi và phân công

BE1 sở hữu business decisions trong .NET: requests/onboarding/Order, service/deposit snapshots, appointment/capture policy, invoice, Baseline/identity lifecycle, human review, release authorization, claim/refund/correction và định nghĩa financial report. BE2 triển khai Mission/media/AI adapters, gateway plumbing, pending consumers, Owner queries/notifications/export adapters theo policy BE1. Không bắt buộc thêm project Payments riêng khi existing Surveys + SePay đã cung cấp seam.

Actors: Public Applicant, SystemAdmin, assigned SystemManager và TenantOwner. System actors không mượn TenantMembership; resolve assignment server-side. AI/gateway/background workers là supporting systems. ExistingTenantNewFarm tái sử dụng Tenant/Owner, ExistingFarm Follow-up tái sử dụng cả Farm.

```text
MAP/COMBO request → Admin approval + snapshot Order → appointment + deposit
→ safe capture → processing → Manager review + finalized count/invoice
→ balance settlement → atomic publication/unlock/notice/revenue
→ Owner history, claims, corrections, audited refunds và financial reports
```

## 2. Danh mục use case

Giữ UC00–UC12 để truy nguyên tài liệu cũ; UC10 được retire khỏi MVP, không tái dùng ID cho nghiệp vụ khác.

| ID | Use case | Owner |
|---|---|---|
| BE1-UC00 | Foundation, policy versions, tenant isolation và compatibility | BE1 |
| BE1-UC01 | Identity, onboarding invitation, actor context | BE1 |
| BE1-UC02 | Primary Manager assignment/reassignment | BE1 |
| BE1-UC03 | MAP/COMBO catalogue, shared deposit và price versions | BE1 |
| BE1-UC04 | Request intake, public tracking và Admin review | BE1 |
| BE1-UC05 | Atomic approval, first/follow-up Order và snapshots | BE1 |
| BE1-UC06 | Appointment, deposit, verified count/invoice, balance và cancellation | BE1 policy; BE2 gateway |
| BE1-UC07 | FarmBoundary/scope và capture operational context | BE1 policy; BE2 operations |
| BE1-UC08 | Initial Baseline, survey map snapshots và pole identity | BE1 |
| BE1-UC09 | Combo review, Disease Zones và sourced guidance | BE1; BE2 pending ingestion |
| BE1-UC10 | Harvest Readiness | Excluded; compatibility read-only nếu còn dữ liệu |
| BE1-UC11 | Released portal/profile/history/comparison và plant-change reports | BE1 lifecycle; BE2 read models |
| BE1-UC12 | Work queues, durable notice, audit, online Manager field work | BE1 business intent; BE2 infrastructure |
| BE1-UC13 | Settlement-gated atomic result release | BE1; BE2 delivery/queries |
| BE1-UC14 | Claims và negotiated refunds | BE1; BE2 bank-proof storage adapters |
| BE1-UC15 | Withdrawal và reviewed correction versions | BE1 |
| BE1-UC16 | Monthly service revenue/cash/dashboard/Excel | BE1 formulas; BE2 query/export |

## 3. UC00–UC02 — Foundation và access

- Public signup không tự tạo User/Tenant/Farm/Order. Admin approval tạo resource set đúng request kind, invitation không chứa password được tạo thay khách.
- Một Farm tối đa một active primary Manager; reassign giữ history và revoke write access cũ. Retried command rechecks current authorization.
- Policy v3 khác event V3/V4. Historical service/money snapshots không backfill bằng giá demo; legacy Orders phải có explicit disposition.
- Acceptance: cross-tenant enumeration/download/cache denied, assignment race/conflict handled, retry không nhân đôi provisioning/audit/Outbox.

## 4. UC03 — Catalogue và shared deposit

Admin quản lý MAP/COMBO price versions và một shared deposit policy. Public xem active service/rate/deposit và nội dung suspected-disease limitation; giá cuối phụ thuộc reviewed billable count.

- Demo: MAP=1.000, COMBO=2.000 VND/trụ; deposit=100.000 VND. Không hardcode UI/domain; chưa apply seed trong đợt tài liệu.
- Order resolve cấu hình hợp lệ lúc creation, không chờ publication mới snapshot giá.
- Không disease-only, Follow-up tier, free Baseline catalogue hoặc Harvest option.
- Class/guidance catalog immutable/versioned; không severity hoặc expert-diagnosis wording.
- Acceptance: no effective-window overlap; catalogue change không sửa snapshots; missing config rollback approval; only configured 2–3 suspected classes.

## 5. UC04–UC05 — Request, tracking, onboarding và Order

1. Public Applicant gửi NewCustomer MAP/COMBO request với contact/location/farm/service/evidence tham khảo; nhận unguessable tracking code chỉ lộ status/decision tối thiểu.
2. Existing Owner có thể request Farm mới hoặc paid Follow-up cho own released Farm/Baseline. Không tạo trùng Tenant/Farm và không bắt service cùng previous Order.
3. Admin review supported crop/location, feasibility, manager availability và recorded reason; approval không là permission để bỏ safety/preflight.
4. Atomic approval tạo đúng Tenant/Farm/Order/assignment/invitation và price/deposit/policy snapshots, audit/Outbox. Existing flows không tạo Owner account mới.
5. Follow-up lưu BaselineVersionId; mapping/disease comparison basis xác định riêng theo compatible evidence, không blanket same-service restriction.

Acceptance: request retry/idempotency, tracking privacy/rate limiting, approval concurrency và failure injection rollback; first/follow-up snapshots immutable; unreleased initial Baseline không nhận Follow-up.

## 6. UC06 — Appointment, capture funding và settlement

1. Assigned Manager đề xuất appointment; Owner confirm/reschedule; first Farm chưa có approved boundary vẫn đặt lịch được.
2. Owner initiate deposit; server xác định số tiền. Provider evidence được verify/dedup mới credit. Mọi first/follow-up capture cần deposit và operational checks.
3. Manager capture; processing/review xác nhận in-boundary billable poles; chốt invoice theo unit price snapshot. Balance trước flight không là prerequisite.
4. TenantOwner trả RemainingDue qua existing gateway. Approved + Due=0 kích hoạt UC13; callback đến trước/sau approval đều không cần manual approve lần hai.
5. Excess tạo refund obligation, Due=0 không đợi excess refund completed mới release.
6. Cancellation áp dụng BR-CAN-01–06: trước capture hoàn đủ; customer đổi ý sau capture đạt giữ tối đa cọc và hoàn tiền vượt cọc; service không thể bàn giao hoàn đủ; weather/no-show có reschedule policy.

Acceptance: deposit credited once, provider mismatch/retry/out-of-order giữ receipts thật; invoice revision invalidates stale checkout; zero poles exceptional review; no client Confirm; canceled Order Due=0 nhưng refund obligation còn; complaint refund sau release không tạo lại receivable.

## 7. UC07–UC08 — Boundary, Baseline và persistent profiles

- First capture có thể tạo orthophoto để mark boundary sau đó; không đặt gate cần polygon sinh từ chính imagery chưa capture. Follow-up tham chiếu boundary/Baseline versions đã chốt.
- Near/outside-boundary/uncertain matches/new/missing candidates giữ original geometry và NeedsReview. Outside không billable; Manager decision không tự gán qua Farm khác.
- Approved first paid map tạo internal Baseline + verified initial profiles; Owner visibility vẫn locked. First Combo có thể dùng internal approved map cho disease processing từ cùng imagery.
- Follow-up append dated mapping observations/survey map snapshot; không supersede Baseline mặc định. New/retire/replace sau Baseline cần later-survey evidence và approval; relocation giữ profile ID.
- Acceptance: one active profile/pole under concurrency; no automatic retirement; immutable map/version provenance; corrections qua UC15, không destructive overwrite.

## 8. UC09 — Combo review

AI outputs chỉ pending candidates với model/dataset/annotation/evidence provenance; Manager review/correct suspected findings, boundary exceptions, Disease Zones và cited guidance. Chỉ Combo có screening; Map không bị chặn bởi disease job.

- Source observation immutable; review revision/fingerprint khóa đúng input set.
- Unresolved blocking reviews không approve; zero accepted findings giữ preliminary limitation, không Healthy inference.
- Guidance có source/version, không free-form prescription/severity; confusing labels như sunburn/scarring không tự đổi thành bệnh.
- Acceptance: stale revision và wrong assignment fail; history giữ rejected/corrected originals; first Combo không cần second commercially charged flight hoặc already customer-published map.

## 9. UC11–UC12 — Portal, changes và online field work

- Owner xem released Baseline, từng paid Follow-up, mapping/Combo comparison và per-pole history; mọi result source kiểm tra paid release + ownership + withdrawal, kể cả cached timeline/download/presigned media.
- Mapping comparison across MAP/COMBO nếu compatible; disease comparison chỉ compatible Combo. Map-only hiển thị NotAssessed, không clear disease history.
- Owner plant-change report chỉ pending, Manager apply với later-survey evidence; giữ inactive/replaced profile history.
- Mobile online-only: checklist/notes/uploads, interrupted upload retry/resume, operation dedup và version conflict. Không offline cache/task completion queue.
- Durable in-app unlock notice thuộc UC13 transaction; infrastructure email/push dispatch async, không reset claim deadline. Work queues có claims, refunds, failed release và correction tasks.

Acceptance: existing released history không bị khóa bởi Order mới pending; revoked assignment chặn mutation/upload token mới; URLs/exports không leak; retry không duplicate notes; no offline requirement in UI/acceptance.

## 10. UC13 — Atomic release

Trigger là approval hoặc financial confirmation sau cùng. Server khóa Order/result/invoice/payment context, kiểm tra frozen approved revision, finalized Total, Due=0 và current authorization; commit một lần:

1. Immutable publication và Baseline/survey/profile visibility.
2. PublishedAt=UnlockedAt=NoticeCreatedAt, durable in-app notice và revision-specific claim deadline.
3. One original service-value entry/Order, Order completion, audit/Outbox và completion receipt.

No unresolved review, no stale invoice, no async-only notification timestamp. Release failure rollback toàn bộ; duplicate trigger trả existing receipt. ApprovedAt có thể trước release; không lưu cờ client-controlled `isPaid`/`isUnlocked` để bypass.

Acceptance: concurrent approval/callback release một lần; fault giữa publication/notice/revenue rollback; overpayment doesn't block; media/read-model projection chỉ expose persisted release proof.

## 11. UC14 — Claim và refund

1. Owner claim đúng released revision, evidence có ngày, server ReceiveAt trước deadlineExclusive. D+7 calendar rule dùng UTC+7; overdue request bị từ chối có reason, claim đúng hạn tiếp tục xử lý quá deadline.
2. Manager xác minh original imagery/GNSS/AI/review/report versions và đề nghị corrective action.
3. Admin proposal amount/reason, Owner accept exact proposal version. Đổi proposal cần acceptance mới.
4. Reserve dưới Order lock: completed+reserved refunds không vượt receipts. Overpayment reserve cũng tính vào cap.
5. Admin chuyển khoản, lưu proof/reference/completed time; failed/uncertain transfer cần reconciliation trước retry. Không coi uploaded screenshot đơn lẻ là đủ để client tự Confirm.

Acceptance: duplicate claims/negotiations/transfers idempotent; all-Order cap; pending không trừ report cash/revenue; owner đổi bank details có authorization/audit; finance export không lộ bank info. Overpayment/cancellation refund không cần claim/window.

## 12. UC15 — Withdrawal và correction

- Report/Baseline sai được withdraw có reason và reviewed replacement; giữ original versions, evidence và dependent Follow-up references.
- Substantive replacement có notice/window mới riêng cho revision; cosmetic edit không reset. Old claims vẫn xử lý, refund cap vẫn toàn Order.
- Correction/Re-flight không charge service thêm, không tạo sale lần hai, không đổi original invoice ngầm. Financial compensation ghi explicit refund.
- Acceptance: historical order renders exact basis; withdrawn result không còn là current download/cache; unchanged pole giữ ID; replacement approval/payment/window race không double release.

## 13. UC16 — Revenue Dashboard và Excel

Admin filter month/year/service. Shared query implements BR-FIN-01–05: one-time published service value, completed service refunds/net revenue, receipts/all cash refunds/net cash, deposit liabilities, cancellation retention và outstanding month-end balances.

Ví dụ invoice 60.000, nhận 100.000, refund thừa 40.000 → net service revenue 60.000, net cash 60.000; complaint refund thêm 20.000 → cả hai 40.000 nếu cùng kỳ. Cross-month postings dùng publication/transaction/refund completion dates thực tế, không current status hoặc flight date.

Acceptance: Dashboard=Excel, UTC+7 month boundaries, pending excluded, corrected report không sale trùng, refund tháng sau có thể làm revenue tháng đó âm; export Admin-only/audited và text không Excel formula injection.

## 14. Definition of Done

Mỗi UC cần rule traceability, authorized/idempotent transitions, immutable provenance, migration disposition cho legacy data, API/event contract review, meaningful concurrency/rollback/security checks và [v3 acceptance](../docs/testing/v3-business-acceptance.md). Status evidence cũ và v3 acceptance theo dõi riêng; documentation rewrite không đánh dấu implementation Done.
